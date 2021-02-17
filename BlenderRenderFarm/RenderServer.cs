using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using BlenderRenderFarm.Messages;
using Flare.Tcp;
using Memowned;
using MessagePack;

namespace BlenderRenderFarm {
    public class RenderServer : IDisposable {
        private readonly ConcurrentFlareTcpServer Server = new();

        public event FrameReceivedEventHandler? FrameReceived;
        public delegate void FrameReceivedEventHandler(uint frameIndex, byte[] frameBytes);
        public event FrameProgressEventHandler? FrameProgress;
        public delegate void FrameProgressEventHandler(uint frameIndex, TimeSpan remaining);
        public event FrameFailureEventHandler? FrameFailure;
        public delegate void FrameFailureEventHandler(uint frameIndex, string reason);

        private readonly byte[] blendFileBytes;
        private readonly ConcurrentQueue<uint> failedFrames = new();
        private readonly ConcurrentDictionary<long, ClientToken> clients = new();
        private uint nextFrame = 1;
        private readonly uint frameCount;

        public RenderServer(byte[] blendFileBytes, uint startFrame, uint frameCount) {
            Server.MessageReceived += Server_MessageReceived;
            Server.ClientConnected += Server_ClientConnected;
            Server.ClientDisconnected += Server_ClientDisconnected;

            this.blendFileBytes = blendFileBytes;
            this.nextFrame = startFrame;
            this.frameCount = frameCount;
        }

        public Task ListenAsync(CancellationToken cancellationToken = default) {
            return Server.ListenAsync(42424, cancellationToken);
        }

        public void Stop() {
            Server.Shutdown();
        }

        private bool TryGetNextFrame(out uint nextFrame) {
            nextFrame = Interlocked.Increment(ref this.nextFrame) - 1;
            return nextFrame < frameCount;
        }

        private void Server_ClientConnected(long clientId) {
            var client = AddClient(clientId);

            Console.WriteLine($"Client {clientId} connected");
            Console.WriteLine($"Sending blend to client {clientId}");
            SendMessage(clientId, new InitRenderMessage(blendFileBytes));

            if (!TryAssignNextFrame(client)) {
                // TODO remove client as no more frames are available.
            }
        }

        private void Server_ClientDisconnected(long clientId) {
            Console.WriteLine($"Client {clientId} disconnected");

            var client = RemoveClient(clientId);
            foreach (var frame in client.DrainFrames())
                failedFrames.Enqueue(frame);
        }
        private void Server_MessageReceived(long clientId, RentedMemory<byte> message) {
            using (message) {
                var messageObject = MessagePackSerializer.Typeless.Deserialize(message.Memory);
                HandleMessage(clientId, messageObject);
            }
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
            var client = GetClient(clientId);
            client.RemoveFrame(message.FrameIndex);
            FrameReceived?.Invoke(message.FrameIndex, message.ImageBytes);

            if (!TryAssignNextFrame(client)) {
                // TODO
            }
        }

        private bool TryGetClient(long clientId, [NotNullWhen(true)] out ClientToken? client) {
            return clients.TryGetValue(clientId, out client);
        }
        /*private ClientToken GetOrAddClient(long clientId) {
            return clients.GetOrAdd(clientId, id => new ClientToken(id));
        }*/
        private ClientToken GetClient(long clientId) {
            if (!TryGetClient(clientId, out var client))
                throw new IllegalStateException();
            return client;
        }
        private bool TryRemoveClient(long clientId, [NotNullWhen(true)] out ClientToken client) {
            return clients.TryRemove(clientId, out client!);
        }
        private ClientToken RemoveClient(long clientId) {
            if (!TryRemoveClient(clientId, out var client))
                throw new IllegalStateException();
            return client;
        }
        private ClientToken AddClient(long clientId) {
            var client = new ClientToken(clientId);
            if (!clients.TryAdd(clientId, client))
                throw new IllegalStateException();
            return client;
        }

        private bool TryAssignNextFrame(ClientToken client) {
            if (!failedFrames.TryDequeue(out var frameIndex) && !TryGetNextFrame(out frameIndex))
                return false;
            AssignFrame(client, frameIndex);
            return true;
        }

        private void AssignFrame(ClientToken client, uint frameIndex) {
            Console.WriteLine($"Assigning frame {frameIndex} to client {client.Id}");
            SendMessage(client.Id, new AssignFrameMessage(frameIndex));
            client.AssignFrame(frameIndex);
        }

        protected virtual void OnFrameProgress(long clientId, FrameRenderProgressMessage message) {
            FrameProgress?.Invoke(message.FrameIndex, message.RemainingTime);
        }

        protected virtual void OnFrameFailure(long clientId, FrameRenderFailureMessage message) {
            var client = GetClient(clientId);
            client.RemoveFrame(message.FrameIndex);
            failedFrames.Enqueue(message.FrameIndex);

            FrameFailure?.Invoke(message.FrameIndex, message.Reason);

            // assign next frame?
        }

        public void Dispose() {
            Server?.Dispose();
            GC.SuppressFinalize(this);
        }

        private sealed class ClientToken {
            public readonly long Id;

            private List<uint> assignedFrames = new();
            private readonly object clientLock = new();

            public ClientToken(long id) {
                Id = id;
            }

            public void AssignFrame(uint frame) {
                lock(clientLock) {
                    assignedFrames.Add(frame);
                }
            }

            public void RemoveFrame(uint frame) {
                lock (clientLock) {
                    assignedFrames.Remove(frame);
                }
            }

            public List<uint> DrainFrames() {
                lock (clientLock) {
                    var frames = assignedFrames;
                    assignedFrames = new();
                    return frames;
                }
            }
        }
    }
}
