using MessagePack;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public sealed class InitRenderMessage {
        public InitRenderMessage(byte[] blendFileBytes) {
            BlendFileBytes = blendFileBytes;
        }

        [Key(0)]
        public byte[] BlendFileBytes { get; }
    }
}
