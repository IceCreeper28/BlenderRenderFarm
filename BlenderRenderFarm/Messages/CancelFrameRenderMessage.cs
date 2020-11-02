using MessagePack;
using System;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public class CancelFrameRenderMessage {

        [Key(0)]
        public Range Frames { get; init; }

        [Key(1)]
        public string Reason { get; init; }

    }
}
