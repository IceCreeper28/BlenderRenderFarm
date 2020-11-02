
using MessagePack;
using System;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public class FrameRenderProgressMessage {

        [Key(0)]
        public Index FrameIndex { get; init; }

        [Key(1)]
        public TimeSpan RemainingTime { get; init; }

        // TODO add more progress info

    }
}
