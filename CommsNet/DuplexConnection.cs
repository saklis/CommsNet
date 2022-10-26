using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CommsNet {
    public class DuplexConnection {
        public delegate void ConnectionClosedRemotelyDelegate();

        public delegate void DataReceivedDelegate(byte[] data);

        public delegate void ErrorEncounteredDelegate(Exception ex);

        public delegate void LogDelegate(string message);

        protected CancellationTokenSource _cancellationTokens = new CancellationTokenSource();

        /// <summary>
        ///     Data sent through socket when client closes connection.
        ///     <remarks>42 because... why not 42?</remarks>
        /// </summary>
        protected byte[] _command_disconnected = { 42 };

        /// <summary>
        ///     Semaphore to ensure only one thread can send data at once.
        /// </summary>
        protected SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        protected TcpClient _tcpClient;

        /// <summary>
        ///     Initializes a new instance of the <see cref="DuplexConnection" /> class.
        /// </summary>
        public DuplexConnection(TcpClient tcpClient) {
            _tcpClient                   = tcpClient;
            _tcpClient.ReceiveBufferSize = int.MaxValue;
            _tcpClient.SendBufferSize    = int.MaxValue;
        }

        /// <summary>
        ///     Inheritance constructor
        /// </summary>
        protected DuplexConnection() {
            // intentionally left empty
        }

        /// <summary>
        ///     If set to true, object will automatically stop listening for further transmission in
        ///     case of an error.
        /// </summary>
        public bool StopListeningForTransmissionOnError { get; set; } = false;

        /// <summary>
        ///     If set to false, aside of using events to signal exceptions, it'll also re-throw all
        ///     encountered exception. Default value is true (exceptions will NOT be re-thrown).
        /// </summary>
        public bool SuppressExceptions { get; set; } = true;

        /// <summary>
        ///     If set, all DataReceived events will be marshaled to provided Synchronization Context.
        /// </summary>
        public SynchronizationContext SynchronizationContext { get; set; }

        /// <summary>
        ///     Time that the object waits for a data packet after receiving a header, in milliseconds.
        /// </summary>
        public int TransmissionReceivedTimeout { get; set; } = 5000;

        /// <summary>
        ///     New transmission was received.
        /// </summary>
        public event DataReceivedDelegate DataReceived;

        /// <summary>
        ///     Network or other error was encountered.
        /// </summary>
        public event ErrorEncounteredDelegate ErrorEncountered;

        /// <summary>
        ///     TCP connection was closed by the other side.
        /// </summary>
        public event ConnectionClosedRemotelyDelegate ConnectionClosedRemotely;

        /// <summary>
        ///     Leg entry was generated.
        /// </summary>
        public event LogDelegate Log;

        /// <summary>
        ///     Close connection.
        /// </summary>
        public void Disconnect() {
            Send(_command_disconnected);
            StopListening();
            _tcpClient.Close();
        }

        /// <summary>
        ///     SendAsync data to connected client.
        /// </summary>
        /// <param name="data"> Data to be send. </param>
        public void Send(byte[] data) {
            try {
                _semaphore.Wait();
                try {
                    byte[] dataLengthBytes = BitConverter.GetBytes(data.Length); // data.Length is Int32 - assume 4 bytes length
                    Log?.Invoke($"CN>DC: Sending transmission. Data length: {data.Length};");
                    _tcpClient.GetStream().Write(dataLengthBytes, 0, dataLengthBytes.Length);
                    _tcpClient.GetStream().Write(data, 0, data.Length);
                    Log?.Invoke($"CN>DC: Transmission sent.");
                } finally {
                    _semaphore.Release();
                }
            } catch (IOException ex) {
                ErrorEncountered?.Invoke(ex);

                if (!SuppressExceptions) {
                    throw;
                }
            }
        }

        /// <summary>
        ///     SendAsync data to connected client.
        /// </summary>
        /// <param name="data">  Data to be send. </param>
        /// <param name="token">
        ///     Optional cancellation token that can be used to cancel operation before it's completed.
        /// </param>
        public async Task SendAsync(byte[] data, CancellationToken token = default(CancellationToken)) {
            try {
                await _semaphore.WaitAsync(token);
                try {
                    byte[] dataLengthBytes = BitConverter.GetBytes(data.Length); // data.Length is Int32 - assume 4 bytes length
                    Log?.Invoke($"CN>DC: Sending transmission. Data length: {data.Length};");
                    await _tcpClient.GetStream().WriteAsync(dataLengthBytes, 0, dataLengthBytes.Length, token);
                    await _tcpClient.GetStream().WriteAsync(data, 0, data.Length, token);
                    Log?.Invoke($"CN>DC: Transmission sent.");
                } finally {
                    _semaphore.Release();
                }
            } catch (IOException ex) {
                ErrorEncountered?.Invoke(ex);

                if (!SuppressExceptions) {
                    throw;
                }
            }
        }

        /// <summary>
        ///     Creates a background thread listening for message from the client.
        /// </summary>
        public void StartListening(Action<byte[]> onDataReceived) {
            CancellationToken token = _cancellationTokens.Token;

            DataReceived += data => { onDataReceived(data); };

            Task task = new Task(() => TcpBackgroundListener(_tcpClient, token), token, TaskCreationOptions.LongRunning);
            task.Start();
        }

        /// <summary>
        ///     Stops background listening thread.
        /// </summary>
        public void StopListening() {
            _cancellationTokens?.Cancel();

            if (DataReceived != null) {
                foreach (Delegate @delegate in DataReceived.GetInvocationList()) {
                    DataReceived -= @delegate as DataReceivedDelegate;
                }
            }
        }

        protected void InvokeDataReceived(byte[] data) {
            if (SynchronizationContext != null) {
                SynchronizationContext.Post(delegate { DataReceived?.Invoke(data); }, null);
            } else {
                DataReceived?.Invoke(data);
            }
        }

        /// <summary>
        ///     Background listener for external data transmissions.
        /// </summary>
        /// <param name="client"> Reference to TcpClient to listen too. </param>
        /// <param name="token">  Cancellation token. </param>
        protected async void TcpBackgroundListener(TcpClient client, CancellationToken token) {
            byte[]        buffer       = new byte[1000000];
            NetworkStream stream       = client.GetStream();
            Stopwatch     timeoutWatch = new Stopwatch();

            while (!token.IsCancellationRequested) {
                try {
                    byte[] data           = Array.Empty<byte>();
                    int    bytesReadCount = 0;

                    int dataAlreadyRead = 0;
                    int dataLength      = 0;

                    timeoutWatch.Reset();

                    while ((bytesReadCount = await stream.ReadAsync(buffer, 0, 4, token)) != 0) // read 4 bytes - int32 length of next transmission.
                    {
                        if (token.IsCancellationRequested) {
                            return;
                        }

                        Log?.Invoke($"CN>DC: Data received. New transmission.");

                        // dataLength == 0 means that this is the beginning of new transmission
                        // first 4 bytes of the array is length of entire transmission - convert it
                        // to int (dataLength) and handle the rest as actual transmission data
                        dataLength = BitConverter.ToInt32(buffer, 0);
                        Log?.Invoke($"CN>DC: Declared data length: {dataLength};");

                        Array.Resize(ref data, dataLength);

                        timeoutWatch.Start();

                        do {
                            if (timeoutWatch.ElapsedMilliseconds > TransmissionReceivedTimeout) {
                                throw new TimeoutException($"Duplex Connection received declaration of transmission of size {dataLength} but only received {dataAlreadyRead} bytes within {TransmissionReceivedTimeout} milliseconds.");
                            }

                            int readCount = stream.Read(buffer, 0, dataLength - dataAlreadyRead < buffer.Length ? dataLength - dataAlreadyRead : buffer.Length);
                            if (readCount > 0) {
                                Array.Copy(buffer, 0, data, dataAlreadyRead, readCount);
                            }

                            dataAlreadyRead += readCount;
                            Log?.Invoke($"CN>DC: Transmission data received. Read bytes count: {readCount}; Sum of data read: {dataAlreadyRead};");
                        } while (dataAlreadyRead < dataLength);

                        Log?.Invoke($"CN>DC: Data received reporting to Service Manager. Length: {data.Length};");

                        if (data.Length == _command_disconnected.Length && data[0] == _command_disconnected[0]) {
                            Log?.Invoke("CN>DC: Remote disconnection command received.");
                            ConnectionClosedRemotely?.Invoke();
                            StopListening();
                            return;
                        }

                        InvokeDataReceived(data);
                        Log?.Invoke($"CN>DC: Transmission length == data read. Breaking wait loop...");
                        break;
                    }
                } catch (OperationCanceledException) {
                    /* ignore */
                } catch (Exception ex) {
                    ErrorEncountered?.Invoke(ex);

                    if (StopListeningForTransmissionOnError) {
                        StopListening();
                    }

                    if (!SuppressExceptions) {
                        throw;
                    }
                }
            }
        }
    }
}