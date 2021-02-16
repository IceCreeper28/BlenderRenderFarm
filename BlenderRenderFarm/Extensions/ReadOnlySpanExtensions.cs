using System;

namespace BlenderRenderFarm.Extensions {
    internal static class ReadOnlySpanExtensions {
        public static string Replace(this ReadOnlySpan<char> str, int startIndex, int length, ReadOnlySpan<char> replacement) {
            return string.Concat(str[0..startIndex], replacement, str[(startIndex + length)..]);
        }
    }
}
