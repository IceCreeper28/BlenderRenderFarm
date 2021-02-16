using MessagePack;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public sealed class DeliverRenderedFrameMessage {
        [Key(0)]
        public uint FrameIndex { get; init; }

        [Key(1)]
        public byte[] ImageBytes { get; init; }
    }
}
