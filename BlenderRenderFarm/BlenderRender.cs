using BlenderRenderFarm.Extensions;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BlenderRenderFarm {
    public class BlenderRender {

        private string _blenderPath;
        private string _blendFilePath;
        private string _renderOutput;

        public string BlenderPath {
            get => _blenderPath;
            set => _blenderPath = Path.GetFullPath(value);
        }
        public string BlendFilePath {
            get => _blendFilePath;
            set => _blendFilePath = Path.GetFullPath(value);
        }
        public string RenderOutput {
            get => _renderOutput;
            set {
                _renderOutput = value;

                if (!_renderOutput.StartsWith("//")) // check if not relative path
                    _renderOutput = Path.GetFullPath(_renderOutput);
            }
        }

        public event EventHandler<string>? Output;
        public event EventHandler<string>? Error;
        public event EventHandler<BlenderRenderProgressOutput>? Progress;

        public Task RenderFrameAsync(int frame, CancellationToken cancellationToken = default) =>
            RenderFramesAsync(frame.ToString(), cancellationToken);
        public Task RenderFramesAsync(int startFrame, int endFrame, CancellationToken cancellationToken = default) =>
            RenderFramesAsync($"{startFrame}..{endFrame}", cancellationToken);
        public Task RenderFrameAsync(Index frame, CancellationToken cancellationToken = default) =>
            RenderFramesAsync(frame..frame, cancellationToken);
        public Task RenderFramesAsync(Range frames, CancellationToken cancellationToken = default) {
            var frameStart = (frames.Start.IsFromEnd ? '-' : '+') + frames.Start.Value.ToString();
            var frameEnd = (frames.End.IsFromEnd ? '-' : '+') + frames.End.Value.ToString();
            var frameRange = $"{frameStart}..{frameEnd}";
            return RenderFramesAsync(frameRange, cancellationToken);
        }
        private async Task RenderFramesAsync(string frameRange, CancellationToken cancellationToken = default) {
            BlendFilePath = @"C:\Users\Admin\Desktop\Test\test.blend";
            RenderOutput = @"C:\Users\Admin\Desktop\Test\out\frame######";
            using var blenderProcess = new Process {
                StartInfo = new ProcessStartInfo() {
                    FileName = BlenderPath,
                    Arguments = $"--background \"{BlendFilePath}\" --render-output \"{RenderOutput}\" --log * --debug --threads 0 --render-frame {frameRange}",
                    // CreateNoWindow = false,
                    // WindowStyle = ProcessWindowStyle.Normal,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false // required for redirection
                }
            };

            blenderProcess.OutputDataReceived += (s, e) => {
                if (e.Data is null)
                    return;
                BlenderRenderProgressOutput? output = BlenderRenderProgressOutput.FromLine(e.Data);
                if (output != null)
                    Progress?.Invoke(this, output);
                else
                    Output?.Invoke(this, e.Data);
            };
            blenderProcess.ErrorDataReceived += (s, e) => {
                if (e.Data is null)
                    return;
                Error?.Invoke(this, e.Data);
            };

            blenderProcess.Start();
            blenderProcess.BeginOutputReadLine();
            blenderProcess.BeginErrorReadLine();
            await blenderProcess.StandardInput.WriteLineAsync(ReadOnlyMemory<char>.Empty, cancellationToken).ConfigureAwait(false);
            await blenderProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }

        public string GetFramePathWithoutExtension(int frameIndex) {
            // ensure index mask
            var maskStartIndex = RenderOutput.IndexOf('#');
            string fileWithMask = RenderOutput;
            if (maskStartIndex == -1) {
                fileWithMask += "####";
                maskStartIndex = fileWithMask.Length;
            }

            // find mask
            var maskEndIndex = maskStartIndex + 1;
            while (maskEndIndex < fileWithMask.Length && fileWithMask[maskEndIndex] == '#')
                maskEndIndex++;
            var maskLength = maskEndIndex - maskStartIndex;

            // replace mask
            var paddedFrameIndex = frameIndex.ToString().PadLeft(maskLength, '0');
            var filePath = fileWithMask.AsSpan().Replace(maskStartIndex, maskLength, paddedFrameIndex);

            // make file path absolute
            if (filePath.StartsWith("//")) {
                var relativePath = fileWithMask[2..];
                var directory = Path.GetDirectoryName(BlendFilePath);
                filePath = Path.GetFullPath(relativePath, directory!);
            }

            return filePath;
        }
        public string GetFramePath(int frameIndex, string extension) {
            return Path.ChangeExtension(GetFramePathWithoutExtension(frameIndex), extension);
        }
        public string? FindFrameFile(int frameIndex) {
            var framePath = GetFramePathWithoutExtension(frameIndex);
            var frameDirectory = Path.GetDirectoryName(framePath);
            var frameFileWithoutExtension = Path.GetFileName(framePath);
            foreach (var file in Directory.EnumerateFiles(frameDirectory!))
                if (Path.GetFileNameWithoutExtension(file) == frameFileWithoutExtension)
                    return file;
            return null;
        }

    }
}
