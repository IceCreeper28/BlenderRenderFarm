
using MessagePack;
using System;

namespace BlenderRenderFarm.Messages {
    [MessagePackObject]
    public class DeliverRenderedFrameMessage {

        [Key(0)]
        public Index FrameIndex { get; init; }

        [Key(1)]
        public byte[] ImageBytes { get; init; }

    }
}
