using BlenderRenderFarm.Messages;
using Flare.Tcp;
using Memowned;
using MessagePack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BlenderRenderFarm {
    public class RenderServer : IDisposable {
        private readonly ConcurrentFlareTcpServer Server = new();

        public event FrameReceivedEventHandler? FrameReceived;
        public delegate void FrameReceivedEventHandler(uint frameIndex, byte[] frameBytes);
        public event FrameProgressEventHandler? FrameProgress;
        public delegate void FrameProgressEventHandler(uint frameIndex, TimeSpan remaining);
        public event FrameFailureEventHandler? FrameFailure;
        public delegate void FrameFailureEventHandler(uint frameIndex, string reason);

        private readonly byte[] BlendFileBytes;
        private readonly ConcurrentQueue<uint> FailedFrames = new();
        private readonly ConcurrentDictionary<long, List<uint>> AssignedFrames = new();
        private uint NextFrame = 1;
        private readonly uint FrameCount;

        public RenderServer(byte[] blendFileBytes, uint frameCount) {
            Server.MessageReceived += Server_MessageReceived;
            Server.ClientConnected += Server_ClientConnected;
            Server.ClientDisconnected += Server_ClientDisconnected;

            BlendFileBytes = blendFileBytes;
            FrameCount = frameCount;
        }

        public async Task ListenAsync(CancellationToken cancellationToken = default) {
            await Server.ListenAsync(42424, cancellationToken).ConfigureAwait(false);
        }

        public void Stop() {
            Server.Shutdown();
        }

        private bool TryGetNextFrame(out uint nextFrame) {
            nextFrame = Interlocked.Increment(ref NextFrame) - 1;
            return nextFrame < FrameCount; // TODO
        }

        private void Server_ClientConnected(long clientId) {
            Console.WriteLine($"Client {clientId} connected");
            Console.WriteLine($"Sending blend to client {clientId}");
            SendMessage(clientId, new InitRenderMessage() {
                BlendFileBytes = BlendFileBytes
            });

            if (!TryAssignNextFrame(clientId)) {
                // TODO remove client as no more frames are available.
            }
        }
        private void Server_ClientDisconnected(long clientId) {
            Console.WriteLine($"Client {clientId} disconnected");
            if (AssignedFrames.TryRemove(clientId, out var frameList))
                foreach (var frame in frameList)
                    FailedFrames.Enqueue(frame);
        }
        private void Server_MessageReceived(long clientId, RentedMemory<byte> message) {
            var messageObject = MessagePackSerializer.Typeless.Deserialize(message.Memory);
            HandleMessage(clientId, messageObject);
        }

        private void SendMessage(long clientId, object message) {
            var bytes = MessagePackSerializer.Typeless.Serialize(message);
            Server.EnqueueMessage(clientId, bytes);
        }

        private void HandleMessage(long clientId, object messageObject) {
            switch (messageObject) {
                case DeliverRenderedFrameMessage message:
                    OnFrameReceived(clientId, message);
                    break;
                case FrameRenderProgressMessage message:
                    OnFrameProgress(clientId, message);
                    break;
                case FrameRenderFailureMessage message:
                    OnFrameFailure(clientId, message);
                    break;
                default:
                    throw new NotSupportedException("The given message is not supported: " + messageObject);
            }
        }

        protected virtual void OnFrameReceived(long clientId, DeliverRenderedFrameMessage message) {
            var frameBag = AssignedFrames.GetOrAdd(clientId, new List<uint>());
            lock(frameBag) {
                frameBag.Remove(message.FrameIndex);
            }
            FrameReceived?.Invoke(message.FrameIndex, message.ImageBytes);

            if (!TryAssignNextFrame(clientId)) {
                Server.Shutdown();
            }
        }

        private bool TryAssignNextFrame(long clientId) {
            if (!FailedFrames.TryDequeue(out var frameIndex) && !TryGetNextFrame(out frameIndex))
                return false;
            AssignFrame(clientId, frameIndex);
            return true;
        }

        private void AssignFrame(long clientId, uint frameIndex) {
            Console.WriteLine($"Assigning frame {frameIndex} to client {clientId}");
            SendMessage(clientId, new AssignFrameMessage() {
                FrameIndex = frameIndex
            });
            var frameBag = AssignedFrames.GetOrAdd(clientId, new List<uint>());
            frameBag.Add(frameIndex);
        }

        protected virtual void OnFrameProgress(long clientId, FrameRenderProgressMessage message) {
            FrameProgress?.Invoke(message.FrameIndex, message.RemainingTime);
        }

        protected virtual void OnFrameFailure(long clientId, FrameRenderFailureMessage message) {
            var frameBag = AssignedFrames.GetOrAdd(clientId, new List<uint>());
            lock (frameBag) {
                frameBag.Remove(message.FrameIndex);
            }
            FailedFrames.Enqueue(message.FrameIndex);

            FrameFailure?.Invoke(message.FrameIndex, message.Reason);

            // assign next frame?
        }

        public void Dispose() {
            Server?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
