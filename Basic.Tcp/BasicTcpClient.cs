using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Basic.Tcp {
    public class BasicTcpClient : IDisposable {

        private TcpClient _client;
        private CancellationTokenSource _tokenSource;
        private CancellationToken _token => _tokenSource.Token;

        public bool IsConnected => _client?.Connected == true;

        public event MessageReceivedEventHandler? MessageReceived;
        public delegate void MessageReceivedEventHandler(ReadOnlySpan<byte> message);

        public BasicTcpClient() {
            _client = new TcpClient();
        }

        public async Task SendMessageAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default) {
            var combinedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(_token, cancellationToken).Token;
            var stream = _client.GetStream();
            var headerBuffer = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(headerBuffer, message.Length);
            await stream.WriteAsync(headerBuffer, combinedCancellationToken);
            await stream.WriteAsync(message, combinedCancellationToken);
            await stream.FlushAsync(combinedCancellationToken);
        }

        public async Task ConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken = default) {
            if (IsConnected)
                throw new InvalidOperationException();

            _tokenSource = new CancellationTokenSource();
            var combinedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(_token, cancellationToken).Token;

            await _client.ConnectAsync(endPoint.Address, endPoint.Port, combinedCancellationToken);
        }

        public async Task ReadMessagesAsync(CancellationToken cancellationToken = default) {
            if (!IsConnected)
                throw new InvalidOperationException();
            var combinedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(_token, cancellationToken).Token;

            using var stream = _client.GetStream();
            var headerBuffer = new byte[4];
            while (_client.Connected && !combinedCancellationToken.IsCancellationRequested) {
                await stream.ReadAsync(headerBuffer, combinedCancellationToken);
                var messageLength = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer);
                var messageBuffer = ArrayPool<byte>.Shared.Rent(messageLength);
                await stream.ReadAsync(messageBuffer, 0, messageLength, combinedCancellationToken);
                MessageReceived?.Invoke(messageBuffer);
                ArrayPool<byte>.Shared.Return(messageBuffer);
            }
        }


        public void Disconnect() {
            if (!IsConnected)
                throw new InvalidOperationException();

            _tokenSource.Cancel();
            _client.Close();
            _client.Dispose();
        }

        public void Dispose() {
            if (IsConnected)
                Disconnect();
        }
    }
}
