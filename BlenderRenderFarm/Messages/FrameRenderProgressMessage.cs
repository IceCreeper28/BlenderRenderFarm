using System;
using MessagePack;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public sealed class FrameRenderProgressMessage {
        public FrameRenderProgressMessage(uint frameIndex, TimeSpan remainingTime) {
            FrameIndex = frameIndex;
            RemainingTime = remainingTime;
        }

        [Key(0)]
        public uint FrameIndex { get; }

        [Key(1)]
        public TimeSpan RemainingTime { get; }

        // TODO add more progress info
    }
}
