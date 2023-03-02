using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommsNet.Exceptions;
using CommsNet.Structures;
using MessagePack;

namespace CommsNet {
    /// <summary>
    ///     Provides support for remote method execution.
    /// </summary>
    public abstract class ServiceManager {
        public delegate void ConnectionClosedRemotelyDelegate(Guid sessionIdentity);

        public delegate void LogDelegate(string message);

        public delegate void NewConnectionEstablishedDelegate(Guid sessionIdentity, DuplexConnection connection);

        public delegate void SessionEncounteredErrorDelegate(Guid sessionIdentity, Exception ex);

        protected ConnectionManager _connectionManager;
        protected DuplexConnection _connectionToServer;

        protected ConcurrentDictionary<Guid, (Transmission Header, object[] Content)> _responses = new ConcurrentDictionary<Guid, (Transmission Header, object[] Content)>();

        /// <summary>
        ///     Inheritance constructor
        /// </summary>
        protected ServiceManager() { }

        /// <summary>
        ///     Reference to Task responsible for periodical cleanup of expired transmissions. Assigned by calling
        ///     <see cref="StartServer" /> or <see cref="ConnectToServer" /> methods.
        /// </summary>
        public Task ResponsesCleanupTask { get; private set; }

        /// <summary>
        ///     Timeout time for waiting for response from remote execution. Default value: 15 seconds.
        /// </summary>
        public TimeSpan ExecuteRemoteResponseTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        ///     Leg entry was generated.
        /// </summary>
        public event LogDelegate Log;

        /// <summary>
        ///     New connection was established.
        /// </summary>
        public event NewConnectionEstablishedDelegate NewConnectionEstablished;

        /// <summary>
        ///     Session encountered an error.
        /// </summary>
        public event SessionEncounteredErrorDelegate SessionEncounteredError;

        /// <summary>
        ///     Connection closed from the other side.
        /// </summary>
        public event ConnectionClosedRemotelyDelegate ConnectionClosedRemotely;

        /// <summary>
        ///     Connect to the server.
        /// </summary>
        /// <param name="host">                  Server's host name or IP address. </param>
        /// <param name="port">                  Server's port number. </param>
        /// <param name="localPort">
        ///     Port used on local machine for receiving transmissions. Default value (0) means that
        ///     same port number as remote one will be used.
        /// </param>
        public async void ConnectToServer(string host, int port, int localPort = 0) {
            _connectionManager = new ConnectionManager { AddListenerToEstablishedConnections = true };
            _connectionToServer = await _connectionManager.ConnectAsync(host, port, localPort);
            _connectionToServer.Log += message => Log?.Invoke(message);
            _connectionManager.NewConnectionEstablished += OnNewConnectionEstablished;
            _connectionManager.DataReceived += OnDataReceived;
            _connectionManager.SessionEncounteredError += OnSessionEncounteredError;
            _connectionManager.ConnectionClosedRemotely += OnConnectionClosedRemotely;

            ResponsesCleanupTask = Task.Run(ClearResponseCollection);
        }

        private void OnConnectionClosedRemotely(Guid sessionidentity) {
            ConnectionClosedRemotely?.Invoke(sessionidentity);
        }

        /// <summary>
        ///     Disconnects from the server.
        /// </summary>
        public void DisconnectFromServer() {
            _connectionManager?.Stop();
        }

        /// <summary>
        ///     Start server.
        /// </summary>
        /// <param name="port">                  Server's port number. </param>
        public void StartServer(int port) {
            _connectionManager = new ConnectionManager { AddListenerToEstablishedConnections = true };
            _connectionManager.Start(port);

            _connectionManager.NewConnectionEstablished += OnNewConnectionEstablished;
            _connectionManager.DataReceived += OnDataReceived;
            _connectionManager.SessionEncounteredError += OnSessionEncounteredError;
            _connectionManager.ConnectionClosedRemotely += OnConnectionClosedRemotely;

            ResponsesCleanupTask = Task.Run(ClearResponseCollection);
        }

        /// <summary>
        ///     Disconnects all clients and stops the server.
        /// </summary>
        public void StopServer() {
            _connectionManager.Stop();

            _connectionManager.NewConnectionEstablished -= OnNewConnectionEstablished;
            _connectionManager.DataReceived -= OnDataReceived;
            _connectionManager.SessionEncounteredError -= OnSessionEncounteredError;
            _connectionManager.ConnectionClosedRemotely -= OnConnectionClosedRemotely;
        }

        /// <summary>
        ///     Periodically checks if any of the transmissions are expired and tries to removed them
        ///     from collection.
        /// </summary>
        protected async Task ClearResponseCollection() {
            List<Guid> guidsToRemove = new List<Guid>();
            while (true) {
                await Task.Delay((int)(ExecuteRemoteResponseTimeout.TotalMilliseconds * 10));

                guidsToRemove.Clear();
                foreach (KeyValuePair<Guid, (Transmission Header, object[] Content)> response in _responses) {
                    if (response.Value.Header.ExpirationTime < DateTime.Now) {
                        guidsToRemove.Add(response.Key);
                    }
                }

                foreach (Guid guid in guidsToRemove) {
                    if (!_responses.TryRemove(guid, out (Transmission Header, object[] Content) _)) {
                        throw new ArgumentException($"Unable to remove expired Transmission [guid:{guid}] from responses dictionary.");
                    }
                }
            }
        }

        /// <summary>
        ///     Calls a void method remotely.
        /// </summary>
        /// <param name="sessionIdentity">
        ///     Session GUID of the client that should execute the method. If called from the client
        ///     'default' or Guid.Empty should be passed.
        /// </param>
        /// <param name="methodName">Name of the method.</param>
        /// <param name="arguments">Method's arguments.</param>
        protected async Task RemoteCallAsync(Guid sessionIdentity, string methodName, params object[] arguments) {
            await Task.Run(async () => {
                               Guid opIdentity = Guid.NewGuid();
                               Transmission newTransmission = new Transmission {
                                                                                   MethodName = methodName,
                                                                                   Identity = opIdentity,
                                                                                   SessionIdentity = sessionIdentity,
                                                                                   Type = (int)TransmissionType.Request,
                                                                                   HasContent = true,
                                                                                   HasReturn = false,
                                                                                   ExpirationTime = DateTime.Now.AddMilliseconds(ExecuteRemoteResponseTimeout.TotalMilliseconds * 10)
                                                                               };
                               byte[] headerBytes = MessagePackSerializer.Serialize(newTransmission, MessagePackSerializerOptions.Standard.WithAllowAssemblyVersionMismatch(true).WithOmitAssemblyVersion(true));
                               byte[] headerLenBytes = BitConverter.GetBytes(headerBytes.Length);
                               byte[] contentBytes = MessagePackSerializer.Serialize(arguments, MessagePackSerializerOptions.Standard.WithAllowAssemblyVersionMismatch(true).WithOmitAssemblyVersion(true));

                               byte[] message = new byte[headerLenBytes.Length + headerBytes.Length + contentBytes.Length];
                               Array.Copy(headerLenBytes, message, headerLenBytes.Length);
                               Array.Copy(headerBytes, 0, message, headerLenBytes.Length, headerBytes.Length);
                               Array.Copy(contentBytes, 0, message, headerLenBytes.Length + headerBytes.Length, contentBytes.Length);

                               Log?.Invoke($"CN: Message built. HeaderLength size: {headerLenBytes.Length.ToString()}; Header size: {headerBytes.Length.ToString()}; Content size: {contentBytes.Length.ToString()};");

                               if (sessionIdentity != default) {
                                   await _connectionManager[sessionIdentity].SendAsync(message);
                                   Log?.Invoke($"CN: Message sent. Session:{sessionIdentity.ToString()}; OpId: {opIdentity.ToString()}; Method: {methodName};");
                               } else {
                                   await _connectionToServer.SendAsync(message);
                                   Log?.Invoke($"CN: Message sent. OpId: {opIdentity.ToString()}; Method: {methodName};");
                               }
                           });
        }

        /// <summary>
        ///     Calls a method remotely.
        /// </summary>
        /// <param name="sessionIdentity">
        ///     Session GUID of the client that should execute the method. If called from the client
        ///     'default' or Guid.Empty should be passed.
        /// </param>
        /// <param name="methodName">Name of the method.</param>
        /// <param name="arguments">Method's arguments.</param>
        /// <typeparam name="RType">Type of value that will be returned from remote call.</typeparam>
        /// <returns>Value returned by remote call.</returns>
        /// <exception cref="ArgumentException">Thrown when response returned different number of values than expected.</exception>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when response could not be removed from awaiting responses
        ///     collection.
        /// </exception>
        /// <exception cref="ResponseTimeoutException">
        ///     Called when response did not arrive in time. Timeout is set through
        ///     <see cref="ExecuteRemoteResponseTimeout" /> property.
        /// </exception>
        protected async Task<RType> RemoteCallAsync<RType>(Guid sessionIdentity, string methodName,
                                                           params object[] arguments) {
            return await Task.Run(async () => {
                                      Guid opIdentity = Guid.NewGuid();
                                      Transmission newTransmission = new Transmission {
                                                                                          MethodName = methodName,
                                                                                          Identity = opIdentity,
                                                                                          SessionIdentity = sessionIdentity,
                                                                                          Type = (int)TransmissionType.Request,
                                                                                          HasContent = true,
                                                                                          HasReturn = true,
                                                                                          ExpirationTime = DateTime.Now.AddMilliseconds(ExecuteRemoteResponseTimeout.TotalMilliseconds * 10)
                                                                                      };
                                      byte[] headerBytes = MessagePackSerializer.Serialize(newTransmission, MessagePackSerializerOptions.Standard.WithAllowAssemblyVersionMismatch(true).WithOmitAssemblyVersion(true));
                                      byte[] headerLenBytes = BitConverter.GetBytes(headerBytes.Length);
                                      //byte[] contentBytes = MessagePackSerializer.Serialize(arguments, MessagePackSerializerOptions.Standard.WithAllowAssemblyVersionMismatch(true).WithOmitAssemblyVersion(true));
                                      byte[] contentBytes = MessagePackSerializer.Typeless.Serialize(arguments, MessagePackSerializerOptions.Standard.WithAllowAssemblyVersionMismatch(true).WithOmitAssemblyVersion(true));

                                      byte[] message = new byte[headerLenBytes.Length + headerBytes.Length + contentBytes.Length];
                                      Array.Copy(headerLenBytes, message, headerLenBytes.Length);
                                      Array.Copy(headerBytes, 0, message, headerLenBytes.Length, headerBytes.Length);
                                      Array.Copy(contentBytes, 0, message, headerLenBytes.Length + headerBytes.Length,
                                                 contentBytes.Length);

                                      Log?.Invoke($"CN: Message built. HeaderLength size: {headerLenBytes.Length.ToString()}; Header size: {headerBytes.Length.ToString()}; Content size: {contentBytes.Length.ToString()};");

                                      if (sessionIdentity != default) {
                                          await _connectionManager[sessionIdentity].SendAsync(message);
                                          Log?.Invoke($"CN: Message sent. Session:{sessionIdentity.ToString()}; OpId: {opIdentity.ToString()}; Method: {methodName};");
                                      } else {
                                          await _connectionToServer.SendAsync(message);
                                          Log?.Invoke($"CN: Message sent. OpId: {opIdentity.ToString()}; Method: {methodName};");
                                      }

                                      RType result = await Task.Run(() => {
                                                                        DateTime timeout = DateTime.Now + ExecuteRemoteResponseTimeout;

                                                                        while (DateTime.Now < timeout) {
                                                                            if (_responses.ContainsKey(opIdentity)) {
                                                                                Log?.Invoke($"CN: Response for {methodName} [OpId:{opIdentity.ToString()}] found.");
                                                                                if (_responses.TryRemove(opIdentity, out (Transmission Header, object[] Content) response)) {
                                                                                    switch (response.Content.Length) {
                                                                                        case 0:
                                                                                            throw new ArgumentException($"Response to {methodName} [OpId:{opIdentity.ToString()}] call received, but expected return value is missing.");
                                                                                        case 1:
                                                                                            if (response.Content[0].GetType() == typeof(RType)) {
                                                                                                return (RType)response.Content[0];
                                                                                            }

                                                                                            try {
                                                                                                return (RType)TypeDescriptor.GetConverter(typeof(RType)).ConvertFromInvariantString((string)response.Content[0]);
                                                                                            } catch (InvalidCastException e) {
                                                                                                throw new NotSupportedException($"Type '{typeof(RType)}' is not supported. Please file issue on project's GitHub to add support to that type.", e);
                                                                                            }

                                                                                        default:
                                                                                            throw new ArgumentException($"Response to {methodName} [OpId:{opIdentity.ToString()}] returned incorrect number of values. Result count: {response.Content.Length.ToString()}");
                                                                                    }
                                                                                }

                                                                                throw new InvalidOperationException("Unable to remove Transmission from responses dictionary.");
                                                                            }
                                                                        }

                                                                        throw new ResponseTimeoutException($"Server did not respond in time to method '{methodName}' Current timeout value: {ExecuteRemoteResponseTimeout}", newTransmission);
                                                                    });
                                      return result;
                                  });
        }

        /// <summary>
        ///     Handles data sent by external source.
        /// </summary>
        /// <param name="identifier"> ServiceManager identifier. </param>
        /// <param name="data">       Data received. </param>
        protected async void OnDataReceived(Guid identifier, byte[] data) {
            try {
                Log?.Invoke($"CN: New transmission of size {data.Length.ToString()} received.");
                // split received data into 3 arrays for deserialization
                byte[] headerLenBytes = new byte[sizeof(int)];
                Array.Copy(data, headerLenBytes, sizeof(int));

                // > length of the header array
                int headerLen = BitConverter.ToInt32(headerLenBytes, 0);

                Log?.Invoke($"CN: Declared header size: {headerLen.ToString()}");

                byte[] headerBytes = new byte[headerLen];
                Array.Copy(data, sizeof(int), headerBytes, 0, headerLen);

                Log?.Invoke($"CN: Header size: {headerBytes.Length.ToString()}");

                byte[] contentBytes = new byte[data.Length - (sizeof(int) + headerLen)];
                Array.Copy(data, sizeof(int) + headerLen, contentBytes, 0, contentBytes.Length);

                Log?.Invoke($"CN: Content size: {contentBytes.Length.ToString()}");

                // > deserialize header
                Transmission transmission = MessagePackSerializer.Deserialize<Transmission>(new ReadOnlyMemory<byte>(headerBytes), MessagePackSerializerOptions.Standard.WithAllowAssemblyVersionMismatch(true).WithOmitAssemblyVersion(true));

                Log?.Invoke($"CN: Header deserialized. Type: {transmission.Type.ToString()}; Method: {transmission.MethodName}; Has content? {transmission.HasContent.ToString()}; Has Return? {transmission.HasReturn.ToString()}");

                // > deserialize content
                object[] content = null;
                MethodInfo method = GetType().GetMethod(transmission.MethodName);
                if (method == null) {
                    throw new MissingMethodException($"Method '{transmission.MethodName}' does not exists in type '{GetType().Name}'.");
                }

                // deserialize content. Call MessagePackSerializer.Deserialize<>() with generic argument based on content's type. 
                content = MessagePackSerializer.Deserialize<object[]>(contentBytes, MessagePackSerializerOptions.Standard.WithAllowAssemblyVersionMismatch(true).WithOmitAssemblyVersion(true));

                transmission.SessionIdentity = identifier;

                switch (transmission.Type) {
                    case (int)TransmissionType.Request:
                        // match declare parameter types with type deserialized by MSP
                        ParameterInfo[] parameterInfos = method.GetParameters();
                        object[] parameters = new object[parameterInfos.Length - 1]; // last one is an Transmission object. Skip it
                        for (int i = 0; i < parameters.Length; i++) {
                            if (parameterInfos[i].ParameterType == content[i].GetType()) {
                                parameters[i] = content[i]; // if type match, just assign parameter's value
                            } else if (content[i] is object[] values) { // if types don't match and deserialized type is a object[], then it's likely a composite type deserialized into an array
                                // try creating instance of type and then assign it's properties using Key attributes
                                object instance = Activator.CreateInstance(parameterInfos[i].ParameterType);
                                foreach (PropertyInfo property in instance.GetType().GetProperties()) {
                                    KeyAttribute keyAttribute = property.GetCustomAttribute<KeyAttribute>();
                                    if (keyAttribute != null) {
                                        property.SetValue(instance, values[(int)keyAttribute.IntKey]);
                                    }
                                }

                                parameters[i] = instance;
                            }
                        }

                        parameters = parameters.Append(transmission).ToArray();

                        // InvokeAsync() and InvokeVoidAsync() are an extension methods
                        // defined in Structures.Extensions
                        if (transmission.HasReturn) {
                            try {
                                // invoke local implementation of method named in the transmission
                                object[] result = { await method.InvokeAsync(this, parameters) };

                                Log?.Invoke($"CN: Method '{method.Name} invoked. Result is of type {result.GetType()}");

                                Transmission responseTransmission = new Transmission {
                                                                                         MethodName = transmission.MethodName,
                                                                                         Identity = transmission.Identity,
                                                                                         SessionIdentity = transmission.SessionIdentity,
                                                                                         Type = (int)TransmissionType.Response,
                                                                                         HasContent = true,
                                                                                         HasReturn = false
                                                                                     };

                                byte[] responseHeaderBytes = MessagePackSerializer.Serialize(responseTransmission,
                                                                                             MessagePackSerializerOptions.Standard.WithAllowAssemblyVersionMismatch(true)
                                                                                                                         .WithOmitAssemblyVersion(true));
                                byte[] responseHeaderLenBytes = BitConverter.GetBytes(responseHeaderBytes.Length);
                                byte[] responseContentBytes =
                                    MessagePackSerializer.Serialize(result, MessagePackSerializerOptions.Standard.WithAllowAssemblyVersionMismatch(true).WithOmitAssemblyVersion(true));

                                byte[] responseMessage = new byte[responseHeaderLenBytes.Length + responseHeaderBytes.Length + responseContentBytes.Length];
                                Array.Copy(responseHeaderLenBytes, responseMessage, responseHeaderLenBytes.Length);
                                Array.Copy(responseHeaderBytes, 0, responseMessage, responseHeaderLenBytes.Length, responseHeaderBytes.Length);
                                Array.Copy(responseContentBytes, 0, responseMessage, responseHeaderLenBytes.Length + responseHeaderBytes.Length, responseContentBytes.Length);
                                Log?.Invoke("CN: Response serialized.");

                                _connectionManager.SendAsync(transmission.SessionIdentity, responseMessage, new CancellationTokenSource(ExecuteRemoteResponseTimeout).Token);
                                Log?.Invoke($"CN: Response to method '{method.Name}' sent. Bytes count sent: {responseMessage.Length.ToString()}");
                            } catch (Exception ex) {
                                SessionEncounteredError?.Invoke(identifier, ex);
                            }
                        } else {
                            try {
                                // invoke local implementation of method named in the transmission
                                await method.InvokeVoidAsync(this, parameters);
                            } catch (Exception ex) {
                                SessionEncounteredError?.Invoke(identifier, ex);
                            }
                        }

                        break;
                    case (int)TransmissionType.Response:
                        // if declare return type and deserialized data don't match, and deserialized data is an object[],
                        // then it's likely a composite type deserialized into an array.
                        if (method.ReturnType.GenericTypeArguments[0] != content.GetType() && content is object[]) {
                            object[] values = content[0] as object[]; // this extra array nesting is a result of having same mechanism for sending method's arguments (of which there can be meny)
                            object instance = Activator.CreateInstance(method.ReturnType.GenericTypeArguments[0]);
                            foreach (PropertyInfo property in instance.GetType().GetProperties()) {
                                KeyAttribute keyAttribute = property.GetCustomAttribute<KeyAttribute>();
                                if (keyAttribute != null) {
                                    property.SetValue(instance, values[(int)keyAttribute.IntKey]);
                                }
                            }

                            content[0] = instance;
                        }

                        _responses.TryAdd(transmission.Identity, (transmission, content));
                        Log?.Invoke($"CN: Transmission added to responses dictionary for processing. OpId: {transmission.Identity.ToString()}; Method: {transmission.MethodName};");
                        break;
                }
            } catch (Exception ex) {
                SessionEncounteredError?.Invoke(identifier, ex);
            }
        }

        /// <summary>
        ///     Handle new connections.
        /// </summary>
        /// <param name="identifier"> Connections identifier. </param>
        /// <param name="connection"> Connection object. </param>
        private void OnNewConnectionEstablished(Guid identifier, DuplexConnection connection) {
            NewConnectionEstablished?.Invoke(identifier, connection);
        }

        /// <summary>
        ///     Session reported an error and was closed.
        /// </summary>
        /// <param name="sessionIdentity"> Session identity. </param>
        /// <param name="ex">              Inner exception. </param>
        private void OnSessionEncounteredError(Guid sessionIdentity, Exception ex) {
            SessionEncounteredError?.Invoke(sessionIdentity, ex);
        }
    }
}