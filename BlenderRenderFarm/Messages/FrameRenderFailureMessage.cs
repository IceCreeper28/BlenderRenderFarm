using MessagePack;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public sealed class FrameRenderFailureMessage {
        [Key(0)]
        public uint FrameIndex { get; init; }

        [Key(1)]
        public string Reason { get; init; }
    }
}
