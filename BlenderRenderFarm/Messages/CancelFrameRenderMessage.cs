using MessagePack;
using System;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public sealed class CancelFrameRenderMessage {
        [Key(0)]
        public uint[] Frames { get; init; }

        [Key(1)]
        public string Reason { get; init; }
    }
}
