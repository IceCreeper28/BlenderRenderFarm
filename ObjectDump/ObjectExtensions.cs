using System;
using System.Diagnostics;

namespace ObjectDump.Extensions {
    public static class ObjectExtensions {

        public static void DumpToConsole(this object obj) => Console.WriteLine(ObjectDumper.Dump(obj));
        public static void DumpToError(this object obj) => Console.Error.WriteLine(ObjectDumper.Dump(obj));
        public static void DumpToTrace(this object obj) => Trace.WriteLine(ObjectDumper.Dump(obj));

    }
}
