using System;

namespace CommsNet.Structures {
    /// <summary>
    ///     Internal simple session object for <see cref="ConnectionManager" />. Pairs connection object
    ///     with session identity. Helps marshaling events from connection object to manager.
    /// </summary>
    internal class InternalSession {
        public delegate void ConnectionClosedRemotelyDelegate(Guid sessionIdentity);

        public delegate void ErrorEncounteredDelegate(Guid sessionIdentity, Exception ex);

        protected Action<Guid, byte[]> _onDataReceived;

        public InternalSession(Guid identity, DuplexConnection connection) {
            Identity = identity;
            Connection = connection;

            Connection.ErrorEncountered += OnErrorEncountered;
            Connection.ConnectionClosedRemotely += OnConnectionClosedRemotely;
        }

        public DuplexConnection Connection { get; set; }

        public Guid Identity { get; set; }

        private void OnConnectionClosedRemotely() {
            ConnectionClosedRemotely?.Invoke(Identity);
        }

        public event ErrorEncounteredDelegate ErrorEncountered;

        public event ConnectionClosedRemotelyDelegate ConnectionClosedRemotely;

        public void AddListener(Action<Guid, byte[]> onDataReceived) {
            _onDataReceived = onDataReceived;
            Connection.StartListening(OnDataReceived);
        }

        private void OnDataReceived(byte[] data) {
            _onDataReceived(Identity, data);
        }

        private void OnErrorEncountered(Exception ex) {
            ErrorEncountered?.Invoke(Identity, ex);
        }
    }
}