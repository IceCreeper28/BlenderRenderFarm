using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using BlenderRenderFarm.Extensions;
using BlenderRenderFarm.Interop;
using Lazy;
using Semver;

namespace BlenderRenderFarm {
    public static class BlenderUtils {
        public static SemVersion GetBlenderVersionFromBlendFile(string path) {
            Span<char> buffer = stackalloc char[20];
            using (var reader = new StreamReader(File.OpenRead(path))) {
                reader.Read(buffer);
            }
            return GetBlenderVersionFromBlendFileChars(buffer);
        }
        public static SemVersion GetBlenderVersionFromBlendFileBytes(ReadOnlySpan<byte> bytes) {
            return GetBlenderVersionFromBlendFileChars(MemoryMarshal.Cast<byte, char>(bytes));
        }
        public static SemVersion GetBlenderVersionFromBlendFileChars(ReadOnlySpan<char> chars) {
            chars = chars.TrimStart("BLENDER-v");
            var endIndex = chars.IndexOf("RENDH");
            chars = chars.Slice(0, endIndex);

            var major = (int)char.GetNumericValue(chars[0]);
            var minor = int.Parse(chars[1..]);

            return new SemVersion(major, minor);
        }

        public static ReadOnlySpan<char> GetDefaultBlenderExecutablePath() {
            return NativeUtils.GetExecutableForFileExtension(".blend");
        }

        public static Dictionary<SemVersion, string> GetAvailableBlenderExecutables() {
            var defaultExePath = GetDefaultBlenderExecutablePath();
            var defaultCommonPath = Path.GetDirectoryName(Path.GetDirectoryName(defaultExePath));

            var programFilesX64Path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolderOption.DoNotVerify);
            var blenderX64CommonPath = Path.Combine(programFilesX64Path, "Blender Foundation");

            var programFilesX86Path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolderOption.DoNotVerify);
            var blenderX86CommonPath = Path.Combine(programFilesX86Path, "Blender Foundation");

            var commonPaths = new string[] { defaultCommonPath.ToString(), blenderX86CommonPath, blenderX64CommonPath };

            return commonPaths.Distinct()
                .SelectMany(path => Directory.GetDirectories(path))
                .Select(path => Path.Combine(path, "blender.exe"))
                .Where(path => File.Exists(path))
                .Select(path => (Version: GetBlenderVersionFromExecutable(path), Path: path))
                .Where(e => e.Version != null)
                .ToDictionary(e => e.Version!, e => e.Path);
        }

        [Lazy]
        private static Regex VersionRegex => new(@"Blender\s+(\d+(?:\.\d+)+)", RegexOptions.IgnoreCase);
        private static SemVersion? GetBlenderVersionFromExecutable(string path) {
            using var process = Process.Start(new ProcessStartInfo {
                FileName = path,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd();

            var match = VersionRegex.Match(output);
            if (!match.Success)
                return null;

            if (SemVersion.TryParse(match.Groups[1].Value, out var version))
                return version;

            return null;
        }
    }
}
