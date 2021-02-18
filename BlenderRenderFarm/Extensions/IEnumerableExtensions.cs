using System;
using System.Collections.Generic;
using System.Linq;

namespace BlenderRenderFarm.Extensions {
    internal static class IEnumerableExtensions {
        public static IEnumerable<T> Peek<T>(this IEnumerable<T> enumerable, Action<T> action) {
            return enumerable.Select(e => { action(e); return e; });
        }
    }
}
