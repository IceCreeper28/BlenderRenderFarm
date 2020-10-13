using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BlenderRenderFarm {
    public class BlenderRender {

        public string BlenderPath { get; init; }
        public string BlendFilePath { get; init; }
        public string OutputDirectory { get; init; }
        public Range Frames { get; init; }
        public Index Frame { 
            init {
                Frames = new Range(value, value);
            }
        }

        public event EventHandler<string> Output;
        public event EventHandler<string> Error;
        public event EventHandler<BlenderRenderProgressOutput> Progress;

        // TODO optimize for when progress is null
        public async Task RunAsync(CancellationToken cancellationToken = default) {
            var blenderPath = Path.GetFullPath(BlenderPath);
            var blendFilePath = Path.GetFullPath(BlendFilePath);
            var outputDirectory = Path.GetFullPath(OutputDirectory);
            var frameStart = (Frames.Start.IsFromEnd ? '-' : '+') + Frames.Start.Value.ToString();
            var frameEnd = (Frames.End.IsFromEnd ? '-' : '+') + Frames.End.Value.ToString();
            var frameRange = $"{frameStart}..{frameEnd}";

            var blenderProcess = new Process();
            blenderProcess.StartInfo = new ProcessStartInfo() {
                FileName = blenderPath,
                Arguments = $"--background \"{blendFilePath}\" --render-output \"{outputDirectory}\" --log * --threads 0 --render-frame {frameRange}",
                // CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false // required for redirection
            };

            blenderProcess.OutputDataReceived += (s, e) => {
                if (e.Data is null)
                    return;
                var output = BlenderRenderProgressOutput.FromLine(e.Data);
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
            await blenderProcess.WaitForExitAsync(cancellationToken);
        }

    }
}
