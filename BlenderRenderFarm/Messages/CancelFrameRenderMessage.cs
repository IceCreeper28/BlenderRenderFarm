using MessagePack;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public sealed class CancelFrameRenderMessage {
        public CancelFrameRenderMessage(uint[] frames, string reason) {
            Frames = frames;
            Reason = reason;
        }

        [Key(0)]
        public uint[] Frames { get; }

        [Key(1)]
        public string Reason { get; }
    }
}
