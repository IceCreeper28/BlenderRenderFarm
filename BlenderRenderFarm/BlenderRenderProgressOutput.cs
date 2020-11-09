using Lazy;
using System;
using System.Text.RegularExpressions;

namespace BlenderRenderFarm {
    public class BlenderRenderProgressOutput {

        [Lazy]
        private static Regex LineRegex => new Regex(
            @"^Fra:(?<FrameIndex>\d+) [^|]*\| Time:(?<ElapsedTime>[\d:.]+) \| Remaining:(?<RemainingTime>[\d:.]+) \| [^|]* \| [^|]* \| Rendered (?<RenderedTiles>\d+)\/(?<TileCount>\d+) Tiles, Sample (?<CurrentSample>\d+)\/(?<SampleCount>\d+), Denoised (?<DenoisedTiles>\d+) tiles$",
            RegexOptions.IgnoreCase
        );

        public int FrameIndex { get; private set; }
        public TimeSpan ElapsedTime { get; private set; }
        public TimeSpan RemainingTime { get; private set; }
        public int RenderedTiles { get; private set; }
        public int TileCount { get; private set; }
        public int CurrentSample { get; private set; }
        public int SampleCount { get; private set; }
        public int DenoisedTiles { get; private set; }

        public static BlenderRenderProgressOutput? FromLine(string line) {
            var match = LineRegex.Match(line);
            if (!match.Success)
                return null;

            var output = new BlenderRenderProgressOutput {
                FrameIndex = int.Parse(match.Groups["FrameIndex"].Value),
                ElapsedTime = ParseTime(match.Groups["ElapsedTime"].Value),
                RemainingTime = ParseTime(match.Groups["RemainingTime"].Value),
                RenderedTiles = int.Parse(match.Groups["RenderedTiles"].Value),
                TileCount = int.Parse(match.Groups["TileCount"].Value),
                CurrentSample = int.Parse(match.Groups["CurrentSample"].Value),
                SampleCount = int.Parse(match.Groups["SampleCount"].Value),
                DenoisedTiles = int.Parse(match.Groups["DenoisedTiles"].Value)
            };

            return output;

            static TimeSpan ParseTime(ReadOnlySpan<char> value) {
                var colonIndex = value.IndexOf(':');
                // var dotIndex = value[colonIndex..].IndexOf('.');
                var minutesStr = value[0..colonIndex];
                var secondsStr = value[(colonIndex+1)..];
                var minutes = int.Parse(minutesStr);
                var seconds = double.Parse(secondsStr);
                return TimeSpan.FromSeconds(minutes * 60 + seconds);
            }
        }
    }
}
