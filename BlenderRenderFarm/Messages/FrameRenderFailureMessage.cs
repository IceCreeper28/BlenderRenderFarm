using MessagePack;
using System;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public class FrameRenderFailureMessage {

        [Key(0)]
        public Index FrameIndex { get; init; }

        [Key(1)]
        public string Reason { get; init; }

    }
}
