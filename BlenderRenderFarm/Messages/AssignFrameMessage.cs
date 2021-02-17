using MessagePack;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public sealed class AssignFrameMessage {
        public AssignFrameMessage(uint frameIndex) {
            FrameIndex = frameIndex;
        }

        [Key(0)]
        public uint FrameIndex { get; }
    }
}
