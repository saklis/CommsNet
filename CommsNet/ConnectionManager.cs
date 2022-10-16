using CommsNet.Structures;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace CommsNet
{
    /// <summary>
    ///     Handle multiple connections and sessions.
    /// </summary>
    public class ConnectionManager
    {
        public delegate void DataReceivedDelegate(Guid identifier, byte[] data);

        public delegate void NewConnectionEstablishedDelegate(Guid identifier, DuplexConnection connection);

        public delegate void SessionEncounteredErrorDelegate(Guid sessionIdentity, Exception ex);

        protected CancellationTokenSource _cancellationTokens = new CancellationTokenSource();

        /// <summary>
        ///     Collection of connections. Key is a session identity.
        /// </summary>
        internal ConcurrentDictionary<Guid, InternalSession> _connections = new ConcurrentDictionary<Guid, InternalSession>();

        protected TcpListener _tcpListener;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ConnectionManager" /> class.
        /// </summary>
        public ConnectionManager() => NewConnectionReceived += OnNewConnectionReceived;

        /// <summary>
        ///     Should all connection have listener assigned to them. If true,
        ///     <see
        ///         cref="DataReceived" />
        ///     event will be raised whenever new data is received.
        /// </summary>
        public bool AddListenerToEstablishedConnections { get; set; } = false;

        /// <summary>
        ///     Server's name or IP address. Empty if this instance of ConnectionManager is a server.
        /// </summary>
        public string Host { get; protected set; }

        /// <summary>
        ///     Server's Port.
        /// </summary>
        public int Port { get; protected set; }

        /// <summary>
        ///     Get <see cref="DuplexConnection" /> object with provided session identity.
        /// </summary>
        /// <param name="key"> ServiceManager identity. </param>
        /// <returns> Connection object. </returns>
        public DuplexConnection this[Guid key]
        {
            get
            {
                if (_connections.ContainsKey(key))
                {
                    return _connections[key].Connection;
                }

                return null;
            }
        }

        /// <summary>
        ///     New data was received from remote client.
        /// </summary>
        public event DataReceivedDelegate DataReceived;

        /// <summary>
        ///     New connection was accepted.
        /// </summary>
        public event NewConnectionEstablishedDelegate NewConnectionEstablished;

        /// <summary>
        ///     Session was close remotely or because of an error.
        /// </summary>
        public event SessionEncounteredErrorDelegate SessionEncounteredError;

        /// <summary>
        ///     Internal event invoked when new connection appeared for the first time.
        /// </summary>
        protected event NewConnectionReceivedDelegate NewConnectionReceived;

        /// <summary>
        ///     Close provided session and remove it from connection list.
        /// </summary>
        /// <param name="sessionIdentity"> Session identity of session to be closed. </param>
        public void CloseSession(Guid sessionIdentity)
        {
            if (_connections.ContainsKey(sessionIdentity))
            {
                _connections[sessionIdentity].Connection.Disconnect();
                _connections.TryRemove(sessionIdentity, out _);
            }
        }

        /// <summary>
        ///     Connect to the server.
        /// </summary>
        /// <param name="host">      Server's name or IP address. </param>
        /// <param name="port">      Server's port number. </param>
        /// <param name="backendType">Backend type that will be used in underlying DuplexConnections.</param>
        /// <param name="localPort">
        ///     Port used on local machine for receiving transmissions. Default value (0) means that
        ///     same port number as remote one will be used.
        /// </param>
        /// <returns> Connection object. </returns>
        public async Task<DuplexConnection> ConnectAsync(string host, int port, int localPort = 0)
        {
            Host = host;
            Port = port;

            if (localPort == 0)
            {
                localPort = Port;
            }

            DuplexConnection connection;

            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, localPort);
            TcpClient tcpClient = new TcpClient(localEndPoint);
            await tcpClient.ConnectAsync(Host, Port);

            connection = new DuplexConnection(tcpClient);

            Guid newGuid = Guid.NewGuid();
            InternalSession session = new InternalSession(newGuid, connection);
            if (AddListenerToEstablishedConnections)
            {
                session.AddListener(OnDataReceived);
            }
            session.ErrorEncountered += OnSessionErrorEncountered;

            if (_connections.TryAdd(newGuid, session))
            {
                NewConnectionEstablished?.Invoke(newGuid, connection);
            }

            return connection;
        }

        /// <summary>
        ///     SendAsync data to client.
        /// </summary>
        /// <param name="sessionIdentity"> Client's session identity. </param>
        /// <param name="data">            Data to sent. </param>
        /// <param name="token">           Cancellation token. </param>
        public async void SendAsync(Guid sessionIdentity, byte[] data, CancellationToken token = default(CancellationToken))
        {
            if (token == default(CancellationToken))
            {
                token = _cancellationTokens.Token;
            }

            await this[sessionIdentity].SendAsync(data, token);
        }

        /// <summary>
        ///     Start listening to new connections on provided port.
        /// </summary>
        /// <param name="port"> Port number. </param>
        public void Start(int port)
        {
            Port = port;

            CancellationToken token = _cancellationTokens.Token;

            _tcpListener = new TcpListener(IPAddress.Any, Port);
            Task task = new Task(async () => await ConnectionListener(token), token, TaskCreationOptions.AttachedToParent | TaskCreationOptions.LongRunning);
            task.Start();
        }

        /// <summary>
        ///     Stop listening to new connections or to incoming transmissions.
        /// </summary>
        public void Stop()
        {
            if (AddListenerToEstablishedConnections)
            {
                foreach (KeyValuePair<Guid, InternalSession> keyValuePair in _connections)
                {
                    keyValuePair.Value.Connection.Disconnect();
                }
            }
            _cancellationTokens.Cancel();
        }

        /// <summary>
        ///     A Task for listening to new connections. Invokes <see cref="NewConnectionReceived" /> on
        ///     new connection.
        /// </summary>
        /// <param name="token"> Cancellation token. </param>
        /// <returns> Encapsulating task. </returns>
        protected async Task ConnectionListener(CancellationToken token)
        {
            _tcpListener.Start();

            while (!token.IsCancellationRequested)
            {
                TcpClient client = await _tcpListener.AcceptTcpClientAsync();
                NewConnectionReceived?.Invoke(client);
            }
        }

        /// <summary>
        ///     Handling new connections.
        /// </summary>
        /// <param name="client"> <see cref="TcpClient" /> representing new connection. </param>
        protected void OnNewConnectionReceived(TcpClient client)
        {
            DuplexConnection connection = new DuplexConnection(client);

            Guid newGuid = Guid.NewGuid();
            InternalSession session = new InternalSession(newGuid, connection);
            if (AddListenerToEstablishedConnections)
            {
                session.AddListener(OnDataReceived);
            }
            session.ErrorEncountered += OnSessionErrorEncountered;


            if (_connections.TryAdd(newGuid, session))
            {
                NewConnectionEstablished?.Invoke(newGuid, connection);
            }
        }

        /// <summary>
        ///     New data was received. Invokes <see cref="DataReceived" /> event.
        /// </summary>
        /// <param name="identity"> ServiceManager identity. </param>
        /// <param name="data">     Data received. </param>
        protected void OnDataReceived(Guid identity, byte[] data)
        {
            DataReceived?.Invoke(identity, data);
        }

        /// <summary>
        ///     Session reported an error.
        /// </summary>
        /// <param name="sessionIdentity"> Session identity. </param>
        /// <param name="ex">              Inner exception. </param>
        protected void OnSessionErrorEncountered(Guid sessionIdentity, Exception ex)
        {
            CloseSession(sessionIdentity);
            SessionEncounteredError?.Invoke(sessionIdentity, ex);
        }

        protected delegate void NewConnectionReceivedDelegate(TcpClient client);
    }
}