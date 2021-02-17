using MessagePack;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public sealed class DeliverRenderedFrameMessage {
        public DeliverRenderedFrameMessage(uint frameIndex, byte[] imageBytes) {
            FrameIndex = frameIndex;
            ImageBytes = imageBytes;
        }

        [Key(0)]
        public uint FrameIndex { get; }

        [Key(1)]
        public byte[] ImageBytes { get; }
    }
}
