using System;
using System.Runtime.InteropServices;
using Microsoft.Windows.Sdk;

namespace BlenderRenderFarm.Interop {
    internal static class NativeUtils {
        private static readonly Guid CLSID_QueryAssociations = new("a07034fd-6caa-4954-ac3f-97a27216f98a");
        public static unsafe ReadOnlySpan<char> GetExecutableForFileExtension(string fileExtension) {
            ThrowExceptionForHR(PInvoke.AssocCreate(CLSID_QueryAssociations, typeof(IQueryAssociations).GUID, out var ptr));

            var query = (IQueryAssociations*)ptr;

            try {
                ThrowExceptionForHR(query->Init(Constants.ASSOCF_INIT_DEFAULTTOSTAR, fileExtension, default, default));

                var size = 0u;
                ThrowExceptionForHR(query->GetString(Constants.ASSOCF_NOTRUNCATE, ASSOCSTR.ASSOCSTR_EXECUTABLE, null, null, ref size));

                var data = new string('\0', (int)size);
                ThrowExceptionForHR(query->GetString(Constants.ASSOCF_NOTRUNCATE, ASSOCSTR.ASSOCSTR_EXECUTABLE, null, data, ref size));

                return data.AsSpan(0, data.Length - 1);
            } finally {
                query->Release();
            }
        }

        public static HRESULT ThrowExceptionForHR(HRESULT result) {
            Marshal.ThrowExceptionForHR(result.Value);
            return result;
        }
    }
}
