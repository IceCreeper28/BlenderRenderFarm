using MessagePack;
using System;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public sealed class AssignFrameMessage {
        [Key(0)]
        public int FrameIndex { get; init; }
    }
}
