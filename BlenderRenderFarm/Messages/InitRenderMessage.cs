using MessagePack;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public sealed class InitRenderMessage {
        [Key(0)]
        public byte[] BlendFileBytes { get; init; }
    }
}
