using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BlenderRenderFarm.Messages;
using Flare.Tcp;
using Memowned;
using MessagePack;

namespace BlenderRenderFarm {
    public class RenderClient : IDisposable {
        private readonly ConcurrentFlareTcpClient Client = new();

        public event RenderInitEventHandler? RenderInit;
        public delegate void RenderInitEventHandler(byte[] blendFileBytes);
        public event FrameAssignedEventHandler? FrameAssigned;
        public delegate void FrameAssignedEventHandler(uint frameIndex);
        public event FrameCancelledEventHandler? FramesCancelled;
        public delegate void FrameCancelledEventHandler(uint[] frameIndex, string reason);

        public RenderClient() {
            Client.MessageReceived += Client_MessageReceived;
            // Client.Connected += Client_Connected;
            // Client.Disconnected += Client_Disconnected;
        }

        public ValueTask ConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken = default) {
            return Client.ConnectAsync(endPoint, cancellationToken);
        }

        public void Disconnect() {
            Client.Disconnect();
        }

        public void SendProgress(uint frameIndex, TimeSpan remaining) {
            SendMessage(new FrameRenderProgressMessage() {
                FrameIndex = frameIndex,
                RemainingTime = remaining
            });
        }
        public void SendFrameBytes(uint frameIndex, byte[] frameBytes) {
            SendMessage(new DeliverRenderedFrameMessage() {
                FrameIndex = frameIndex,
                ImageBytes = frameBytes
            });
        }
        public void SendFrameFailure(uint frameIndex, string reason) {
            SendMessage(new FrameRenderFailureMessage() {
                FrameIndex = frameIndex,
                Reason = reason
            });
        }

        private void Client_MessageReceived(RentedMemory<byte> message) {
            var messageObject = MessagePackSerializer.Typeless.Deserialize(message.Memory);
            HandleMessage(messageObject);
        }

        private void SendMessage(object message) {
            var bytes = MessagePackSerializer.Typeless.Serialize(message);
            Client.EnqueueMessage(bytes);
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
            GC.SuppressFinalize(this);
        }
    }
}
