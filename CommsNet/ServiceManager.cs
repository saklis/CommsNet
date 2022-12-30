﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        /// <param name="maxTransmissionLength"> Maximum length of transmission. </param>
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

        protected async Task RemoteCallAsync(Guid sessionIdentity, string methodName, params object[] arguments) {
            await Task.Run(async () => {
                               Guid OpIdentity = Guid.NewGuid();
                               Transmission newTransmission = new Transmission {
                                                                                   MethodName = methodName,
                                                                                   Identity = OpIdentity,
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

                               Log?.Invoke($"CN: Message built. HeaderLength size: {headerLenBytes.Length}; Header size: {headerBytes.Length}; Content size: {contentBytes.Length};");

                               if (sessionIdentity != default) {
                                   await _connectionManager[sessionIdentity].SendAsync(message);
                                   Log?.Invoke($"CN: Message sent. Session:{sessionIdentity}; OpId: {OpIdentity}; Method: {methodName};");
                               }
                               else {
                                   await _connectionToServer.SendAsync(message);
                                   Log?.Invoke($"CN: Message sent. OpId: {OpIdentity}; Method: {methodName};");
                               }
                           });
        }

        protected async Task<RType> RemoteCallAsync<RType>(Guid sessionIdentity, string methodName,
                                                           params object[] arguments) {
            return await Task.Run(async () => {
                                      Guid OpIdentity = Guid.NewGuid();
                                      Transmission newTransmission = new Transmission {
                                                                                          MethodName = methodName,
                                                                                          Identity = OpIdentity,
                                                                                          SessionIdentity = sessionIdentity,
                                                                                          Type = (int)TransmissionType.Request,
                                                                                          HasContent = true,
                                                                                          HasReturn = true,
                                                                                          ExpirationTime = DateTime.Now.AddMilliseconds(ExecuteRemoteResponseTimeout.TotalMilliseconds * 10)
                                                                                      };
                                      byte[] headerBytes = MessagePackSerializer.Serialize(newTransmission, MessagePackSerializerOptions.Standard.WithAllowAssemblyVersionMismatch(true).WithOmitAssemblyVersion(true));
                                      byte[] headerLenBytes = BitConverter.GetBytes(headerBytes.Length);
                                      byte[] contentBytes = MessagePackSerializer.Serialize(arguments, MessagePackSerializerOptions.Standard.WithAllowAssemblyVersionMismatch(true).WithOmitAssemblyVersion(true));

                                      byte[] message = new byte[headerLenBytes.Length + headerBytes.Length + contentBytes.Length];
                                      Array.Copy(headerLenBytes, message, headerLenBytes.Length);
                                      Array.Copy(headerBytes, 0, message, headerLenBytes.Length, headerBytes.Length);
                                      Array.Copy(contentBytes, 0, message, headerLenBytes.Length + headerBytes.Length,
                                                 contentBytes.Length);

                                      Log?.Invoke($"CN: Message built. HeaderLength size: {headerLenBytes.Length}; Header size: {headerBytes.Length}; Content size: {contentBytes.Length};");

                                      if (sessionIdentity != default) {
                                          await _connectionManager[sessionIdentity].SendAsync(message);
                                          Log?.Invoke($"CN: Message sent. Session:{sessionIdentity}; OpId: {OpIdentity}; Method: {methodName};");
                                      }
                                      else {
                                          await _connectionToServer.SendAsync(message);
                                          Log?.Invoke($"CN: Message sent. OpId: {OpIdentity}; Method: {methodName};");
                                      }

                                      RType result = await Task.Run(() => {
                                                                        DateTime timeout = DateTime.Now + ExecuteRemoteResponseTimeout;

                                                                        while (DateTime.Now < timeout) {
                                                                            if (_responses.ContainsKey(OpIdentity)) {
                                                                                Log?.Invoke($"CN: Response for {methodName} [OpId:{OpIdentity}] found.");
                                                                                if (_responses.TryRemove(OpIdentity, out (Transmission Header, object[] Content) response)) {
                                                                                    switch (response.Content.Length) {
                                                                                        case 0:
                                                                                            throw new ArgumentException($"Response to {methodName} [OpId:{OpIdentity}] call received, but expected return value is missing.");
                                                                                        case 1:
                                                                                            return (RType)response.Content[0];
                                                                                        default:
                                                                                            throw new ArgumentException($"Response to {methodName} [OpId:{OpIdentity}] returned incorrect number of values. Result count: {response.Content.Length}");
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
                Log?.Invoke($"CN: New transmission of size {data.Length} received.");
                // split received data into 3 arrays for deserialization
                byte[] headerLenBytes = new byte[sizeof(int)];
                Array.Copy(data, headerLenBytes, sizeof(int));

                // > length of the header array
                int headerLen = BitConverter.ToInt32(headerLenBytes, 0);

                Log?.Invoke($"CN: Declared header size: {headerLen}");

                byte[] headerBytes = new byte[headerLen];
                Array.Copy(data, sizeof(int), headerBytes, 0, headerLen);

                Log?.Invoke($"CN: Header size: {headerBytes.Length}");

                byte[] contentBytes = new byte[data.Length - (sizeof(int) + headerLen)];
                Array.Copy(data, sizeof(int) + headerLen, contentBytes, 0, contentBytes.Length);

                Log?.Invoke($"CN: Content size: {contentBytes.Length}");

                // > deserialize header
                Transmission transmission = MessagePackSerializer.Deserialize<Transmission>(new ReadOnlyMemory<byte>(headerBytes), MessagePackSerializerOptions.Standard.WithAllowAssemblyVersionMismatch(true).WithOmitAssemblyVersion(true));

                Log?.Invoke($"CN: Header deserialized. Type: {transmission.Type}; Method: {transmission.MethodName}; Has content? {transmission.HasContent}; Has Return? {transmission.HasReturn}");

                // > deserialize content
                object[] content = null;
                MethodInfo method = GetType().GetMethod(transmission.MethodName);
                if (method == null) {
                    throw new MissingMethodException($"Method '{transmission.MethodName}' does not exists in type '{GetType().Name}'.");
                }

                // deserialize content. Call MessagePackSerializer.Deserialize<>() with generic
                // argument based on content's type. 
                content = MessagePackSerializer.Deserialize<object[]>(contentBytes, MessagePackSerializerOptions.Standard.WithAllowAssemblyVersionMismatch(true).WithOmitAssemblyVersion(true));

                transmission.SessionIdentity = identifier;

                switch (transmission.Type) {
                    case (int)TransmissionType.Request:
                        object[] parameters = content.Append(transmission).ToArray();

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
                                Log?.Invoke($"CN: Response to method '{method.Name}' sent. Bytes count sent: {responseMessage.Length}");
                            }
                            catch (Exception ex) {
                                SessionEncounteredError?.Invoke(identifier, ex);
                            }
                        }
                        else {
                            try {
                                // invoke local implementation of method named in the transmission
                                await method.InvokeVoidAsync(this, parameters);
                            }
                            catch (Exception ex) {
                                SessionEncounteredError?.Invoke(identifier, ex);
                            }
                        }

                        break;
                    case (int)TransmissionType.Response:
                        _responses.TryAdd(transmission.Identity, (transmission, content));
                        Log?.Invoke($"CN: Transmission added to responses dictionary for processing. OpId: {transmission.Identity}; Method: {transmission.MethodName};");
                        break;
                }
            }
            catch (Exception ex) {
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
        /// <param name="sessionidentity"> Session identity. </param>
        /// <param name="ex">              Inner exception. </param>
        private void OnSessionEncounteredError(Guid sessionidentity, Exception ex) {
            SessionEncounteredError?.Invoke(sessionidentity, ex);
        }
    }
}