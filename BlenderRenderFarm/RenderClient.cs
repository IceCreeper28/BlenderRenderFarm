using Basic.Tcp;
using BlenderRenderFarm.Messages;
using MessagePack;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BlenderRenderFarm {
    public class RenderClient : IDisposable {

        private readonly BasicTcpClient Client;

        public event RenderInitEventHandler? RenderInit;
        public delegate void RenderInitEventHandler(byte[] blendFileBytes);
        public event FrameAssignedEventHandler? FrameAssigned;
        public delegate void FrameAssignedEventHandler(Index frameIndex);
        public event FrameCancelledEventHandler? FramesCancelled;
        public delegate void FrameCancelledEventHandler(Range frameIndex, string reason);

        public RenderClient() {
            Client = new BasicTcpClient();
            Client.MessageReceived += Client_MessageReceived;
            // Client.Connected += Client_Connected;
            // Client.Disconnected += Client_Disconnected;
        }

        public async Task ConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken = default) {
            await Client.ConnectAsync(endPoint, cancellationToken);
            await Client.ReadMessagesAsync(cancellationToken);
        }

        public void Disconnect() {
            Client.Disconnect();
        }

        public void SendProgress(Index frameIndex, TimeSpan remaining) {
            SendMessage(new FrameRenderProgressMessage() {
                FrameIndex = frameIndex,
                RemainingTime = remaining
            });
        }
        public void SendFrameBytes(Index frameIndex, byte[] frameBytes) {
            SendMessage(new DeliverRenderedFrameMessage() {
                FrameIndex = frameIndex,
                ImageBytes = frameBytes
            });
        }
        public void SendFrameFailure(Index frameIndex, string reason) {
            SendMessage(new FrameRenderFailureMessage() {
                FrameIndex = frameIndex,
                Reason = reason
            });
        }

        private void Client_MessageReceived(ReadOnlySpan<byte> message) {
            var messageObject = MessagePackSerializer.Typeless.Deserialize(message.ToArray());
            HandleMessage(messageObject);
        }

        private void SendMessage(object message) {
            var bytes = MessagePackSerializer.Typeless.Serialize(message);
            Task.Run(() => Client.SendMessageAsync(bytes));
        }

        private void HandleMessage(object messageObject) {
            lock (Client) { // TODO
                switch (messageObject) {
                    case InitRenderMessage message:
                        OnRenderInit(message);
                        break;
                    case AssignFrameMessage message:
                        OnFrameAssigned(message);
                        break;
                    case CancelFrameRenderMessage message:
                        OnFramesCancelled(message);
                        break;
                    default:
                        throw new NotSupportedException("The given message is not supported: " + messageObject);
                }
            }
        }

        protected virtual void OnFramesCancelled(CancelFrameRenderMessage message) {
            FramesCancelled?.Invoke(message.Frames, message.Reason);
        }

        protected virtual void OnFrameAssigned(AssignFrameMessage message) {
            FrameAssigned?.Invoke(message.FrameIndex);
        }

        protected virtual void OnRenderInit(InitRenderMessage message) {
            RenderInit?.Invoke(message.BlendFileBytes);
        }

        public void Dispose() {
            Client?.Dispose();
        }
    }
}
