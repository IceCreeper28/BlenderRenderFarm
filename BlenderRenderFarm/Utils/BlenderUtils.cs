using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
        private static Regex VersionOutputRegex => new(@"Blender\s+(\d+(?:\.\d+)+)", RegexOptions.IgnoreCase);
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

            var match = VersionOutputRegex.Match(output);
            if (!match.Success)
                return null;

            if (SemVersion.TryParse(match.Groups[1].Value, out var version))
                return version;

            return null;
        }

        [Lazy]
        private static HttpClient HttpClient => new();
        [Lazy]
        private static Regex VersionListingRegex => new(@"<a href=\x22(?<fullname>[^\x22]+)\x22>[^<]+<\/a>\s+(?<date>\d+-\w+-\d+\s+\d+:\d+)", RegexOptions.IgnoreCase);
        public static async Task<string?> DownloadBlenderVersionAsync(
            SemVersion version,
            string? targetDirectory,
            IProgress<float>? listVersionsProgress,
            IProgress<float>? downloadArchiveProgress,
            CancellationToken cancellationToken = default) {
            var zipUrl = await GetBlenderPortableDownloadUrl(version, listVersionsProgress, cancellationToken).ConfigureAwait(false);

            using var zipStream = await HttpClient.GetStreamAsync(zipUrl, downloadArchiveProgress, cancellationToken).ConfigureAwait(false);

            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            targetDirectory ??= CreateUniqueTempDirectory();
            archive.ExtractToDirectory(targetDirectory);

            var extractedInstallation = Directory.GetDirectories(targetDirectory).FirstOrDefault();
            if (extractedInstallation is null)
                return null;

            return Path.Combine(extractedInstallation, "blender.exe");

            static async Task<string?> GetBlenderPortableDownloadUrl(SemVersion version, IProgress<float>? progress, CancellationToken cancellationToken) {
                var basePath = $"https://download.blender.org/release/Blender{version.Major}.{version.Minor}/";
                var content = await HttpClient.GetStringAsync(basePath, progress, cancellationToken).ConfigureAwait(false);

                var windowsVersionStr = Environment.Is64BitOperatingSystem ? "windows64" : "windows32";
                var match = GetAllMatches(VersionListingRegex.Match(content))
                    .Select(match => {
                        var path = match.Groups["fullname"].Value;
                        var dateStr = match.Groups["date"].Value;
                        var parsedDate = DateTime.TryParse(dateStr, out var date);
                        return (Path: path, Date: parsedDate ? date : default);
                    })
                    .Where(e => e.Date != default && e.Path.Contains("windows"))
                    .OrderByDescending(e => e.Path.Contains(windowsVersionStr))
                    .ThenByDescending(e => e.Date)
                    .FirstOrDefault();

                if (match.Path is null)
                    return null;

                return Path.Combine(basePath, match.Path);

                static IEnumerable<Match> GetAllMatches(Match firstMatch) {
                    var current = firstMatch;
                    while (current.Success) {
                        yield return current;
                        current = current.NextMatch();
                    }
                }
            }

            static string CreateUniqueTempDirectory() {
                var uniqueTempDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "BlenderRenderFarm-", Guid.NewGuid().ToString()));
                Directory.CreateDirectory(uniqueTempDir);
                return uniqueTempDir;
            }
        }
        public static Task<string?> DownloadBlenderVersionAsync(
            SemVersion version,
            string? targetDirectory,
            IProgress<(float, string)>? progress,
            CancellationToken cancellationToken = default) {
            if (progress is null) {
                return DownloadBlenderVersionAsync(version, targetDirectory, null, null, cancellationToken);
            } else {
                return Core();
            }

            async Task<string?> Core() {
                var listVersionsProgressHandler = new Progress<float>(p => progress.Report((p / 10f, "Retrieving available versions...")));
                var downloadArchiveProgressHandler = new Progress<float>(p => {
                    if (p != 1) {
                        progress.Report((p * 0.7f + 0.1f, "Downloading archive..."));
                    } else {
                        progress.Report((0.8f, "Extracting archive..."));
                    }
                });
                var result = await DownloadBlenderVersionAsync(version, targetDirectory, listVersionsProgressHandler, downloadArchiveProgressHandler, cancellationToken).ConfigureAwait(false);
                progress.Report((1f, "Blender Portable successfully downloaded."));
                return result;
            }
        }
        public static Task<string?> DownloadBlenderVersionAsync(SemVersion version, string? targetDirectory = null, CancellationToken cancellationToken = default) {
            return DownloadBlenderVersionAsync(version, targetDirectory, null, cancellationToken);
        }
    }
}
