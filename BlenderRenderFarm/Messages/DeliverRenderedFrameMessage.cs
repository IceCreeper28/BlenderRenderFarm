using System;
using MessagePack;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public sealed class FrameRenderProgressMessage {
        [Key(0)]
        public uint FrameIndex { get; init; }

        [Key(1)]
        public TimeSpan RemainingTime { get; init; }

        // TODO add more progress info
    }
}
