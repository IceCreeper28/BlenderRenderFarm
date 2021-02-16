using MessagePack;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public sealed class AssignFrameMessage {
        [Key(0)]
        public uint FrameIndex { get; init; }
    }
}
