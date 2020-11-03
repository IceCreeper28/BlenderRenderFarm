using Basic.Tcp;
using BlenderRenderFarm.Messages;
using MessagePack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BlenderRenderFarm {
    public class RenderServer {

        private readonly BasicTcpServer Server;

        public event FrameReceivedEventHandler? FrameReceived;
        public delegate void FrameReceivedEventHandler(Index frameIndex, byte[] frameBytes);
        public event FrameProgressEventHandler? FrameProgress;
        public delegate void FrameProgressEventHandler(Index frameIndex, TimeSpan remaining);
        public event FrameFailureEventHandler? FrameFailure;
        public delegate void FrameFailureEventHandler(Index frameIndex, string reason);

        private byte[] BlendFileBytes;
        private ConcurrentBag<Index> FailedFrames = new ConcurrentBag<Index>();
        private ConcurrentDictionary<long, List<Index>> AssignedFrames = new ConcurrentDictionary<long, List<Index>>();
        private int NextFrame = 1;
        private int FrameCount;

        public RenderServer(byte[] blendFileBytes, int frameCount) {
            Server = new BasicTcpServer(42424);
            Server.MessageReceived += Server_MessageReceived;
            Server.ClientConnected += Server_ClientConnected;
            Server.ClientDisconnected += Server_ClientDisconnected;

            BlendFileBytes = blendFileBytes;
            FrameCount = frameCount;
        }

        public async Task ListenAsync(CancellationToken cancellationToken = default) {
            await Server.ListenAsync(cancellationToken);
        }

        public void Stop() {
            Server.Stop();
        }

        private bool TryGetNextFrame(out Index nextFrame) {
            nextFrame = Interlocked.Increment(ref NextFrame) - 1;
            return nextFrame.Value < FrameCount; // TODO
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
                    FailedFrames.Add(frame);
        }
        private void Server_MessageReceived(long clientId, ReadOnlySpan<byte> message) {
            var messageObject = MessagePackSerializer.Typeless.Deserialize(message.ToArray());
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
            var frameBag = AssignedFrames.GetOrAdd(clientId, new List<Index>());
            lock(frameBag) {
                frameBag.Remove(message.FrameIndex);
            }
            FrameReceived?.Invoke(message.FrameIndex, message.ImageBytes);

            if (!TryAssignNextFrame(clientId)) {
                Server.Stop();
            }
        }

        private bool TryAssignNextFrame(long clientId) {
            Index frameIndex;
            if (!FailedFrames.TryTake(out frameIndex) && !TryGetNextFrame(out frameIndex))
                return false;
            AssignFrame(clientId, frameIndex);
            return true;
        }

        private void AssignFrame(long clientId, Index frameIndex) {
            Console.WriteLine($"Assigning frame {frameIndex} to client {clientId}");
            SendMessage(clientId, new AssignFrameMessage() {
                FrameIndex = frameIndex
            });
            var frameBag = AssignedFrames.GetOrAdd(clientId, new List<Index>());
            frameBag.Add(frameIndex);
        }

        protected virtual void OnFrameProgress(long clientId, FrameRenderProgressMessage message) {
            FrameProgress?.Invoke(message.FrameIndex, message.RemainingTime);
        }

        protected virtual void OnFrameFailure(long clientId, FrameRenderFailureMessage message) {
            var frameBag = AssignedFrames.GetOrAdd(clientId, new List<Index>());
            lock (frameBag) {
                frameBag.Remove(message.FrameIndex);
            }
            FailedFrames.Add(message.FrameIndex);

            FrameFailure?.Invoke(message.FrameIndex, message.Reason);

            // assign next frame?
        }

        public void Dispose() {
            Server?.Dispose();
        }
    }
}
