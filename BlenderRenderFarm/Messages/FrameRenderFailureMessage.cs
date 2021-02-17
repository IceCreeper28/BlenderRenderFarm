using MessagePack;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public sealed class FrameRenderFailureMessage {
        public FrameRenderFailureMessage(uint frameIndex, string reason) {
            FrameIndex = frameIndex;
            Reason = reason;
        }

        [Key(0)]
        public uint FrameIndex { get;  }

        [Key(1)]
        public string Reason { get; }
    }
}
