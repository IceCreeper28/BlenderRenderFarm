using MessagePack;
using System;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public class AssignFrameMessage {

        [Key(0)]
        public Index FrameIndex { get; init; }

    }
}
