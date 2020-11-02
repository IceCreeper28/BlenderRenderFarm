using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Basic.Tcp {
    public class BasicTcpServer : IDisposable {

        private TcpListener _listener;
        private ConcurrentDictionary<long, ClientToken> _clients;
        private CancellationTokenSource _tokenSource;
        private CancellationToken _token => _tokenSource.Token;
        private long _nextClientId = 0;

        public bool IsRunning { get; private set; } = false;

        public event MessageReceivedEventHandler? MessageReceived;
        public delegate void MessageReceivedEventHandler(long clientId, ReadOnlySpan<byte> message);

        public event ClientConnectedEventHandler? ClientConnected;
        public delegate void ClientConnectedEventHandler(long clientId);

        public event ClientDisconnectedEventHandler? ClientDisconnected;
        public delegate void ClientDisconnectedEventHandler(long clientId);

        public BasicTcpServer(int port) {
            _listener = TcpListener.Create(port);
            _clients = new ConcurrentDictionary<long, ClientToken>();
        }

        public void QueueMessage(long clientId, ReadOnlyMemory<byte> message) {
            if (!_clients.TryGetValue(clientId, out var client))
                throw new Exception();

            client.QueueMessage(message);
        }

        public async Task ListenAsync(CancellationToken cancellationToken = default) {
            if (IsRunning)
                throw new InvalidOperationException();
            IsRunning = true;

            _tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _listener.Start();

            while (IsRunning && !_token.IsCancellationRequested) {
                var socket = await _listener.AcceptTcpClientAsync();
                var clientId = GetAndIncrementNextClientId();
                var client = new ClientToken(clientId, socket);
                _clients.TryAdd(clientId, client);
                ClientConnected?.Invoke(clientId);
                HandleClient(client);
            }
        }

        protected long GetNextClientId() {
            return _nextClientId;
        }
        protected long GetAndIncrementNextClientId() {
            return Interlocked.Increment(ref _nextClientId) - 1;
        }

        private void HandleClient(ClientToken client) {
            var socket = client.Socket;
            using var stream = socket.GetStream();
            var readTask = Task.Run(async () => {
                var headerBuffer = new byte[4];
                while (socket.Connected && !_token.IsCancellationRequested) {
                    await stream.ReadAsync(headerBuffer, _token);
                    var messageLength = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer);
                    var messageBuffer = ArrayPool<byte>.Shared.Rent(messageLength);
                    await stream.ReadAsync(messageBuffer, 0, messageLength, _token);
                    MessageReceived?.Invoke(client.Id, messageBuffer.AsSpan(0..messageLength));
                    ArrayPool<byte>.Shared.Return(messageBuffer);
                }
            }, _token);
            var writeTask = Task.Run(async () => {
                var headerBuffer = new byte[4];
                while (socket.Connected && !_token.IsCancellationRequested) {
                    // wait for new packets to send.
                    client.WriteEvent.Wait(_token);
                    client.WriteEvent.Reset();
                    while (client.WriteQueue.TryDequeue(out var message)) {
                        BinaryPrimitives.WriteInt32LittleEndian(headerBuffer, message.Length);
                        await stream.WriteAsync(headerBuffer, _token);
                        await stream.WriteAsync(message, _token);
                        await stream.FlushAsync(_token);
                    }
                }
            }, _token);
            Task.WhenAll(readTask, writeTask).ContinueWith(_ => {
                ClientDisconnected?.Invoke(client.Id);
            });
            Task.WhenAny(readTask, writeTask).ContinueWith(_ => {
                ClientDisconnected?.Invoke(client.Id);
            });
        }
        

        public void Stop() {
            if (IsRunning)
                throw new InvalidOperationException();
            IsRunning = false;

            _tokenSource.Cancel();
            _listener.Stop();
            foreach (var client in _clients.Values) {
                client.Socket.Close();
                client.Dispose();
            }
            _clients.Clear();
        }

        public void Dispose() {
            if (IsRunning)
                Stop();
        }

        private class ClientToken : IDisposable {

            public TcpClient Socket;
            public long Id;
            public ConcurrentQueue<ReadOnlyMemory<byte>> WriteQueue;
            public ManualResetEventSlim WriteEvent;

            public ClientToken(long clientId, TcpClient socket) {
                Socket = socket;
                Id = clientId;
                WriteQueue = new ConcurrentQueue<ReadOnlyMemory<byte>>();
                WriteEvent = new ManualResetEventSlim(false);
            }

            public void Dispose() {
                Socket?.Dispose();
                WriteEvent?.Dispose();
            }

            public void QueueMessage(ReadOnlyMemory<byte> message) {
                WriteQueue.Enqueue(message);
                WriteEvent.Set();
            }
        }
    }
}
