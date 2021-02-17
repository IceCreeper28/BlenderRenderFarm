using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BlenderRenderFarm;
using ObjectDump.Extensions;

namespace RenderClient {
    public static class Program {
        public static async Task Main() {
            TaskScheduler.UnobservedTaskException += (s, e) => Console.Error.WriteLine(e);

            Console.WriteLine("Enter server ip:");
            var serverIp = Console.ReadLine().Trim();

            BlenderRenderFarm.RenderClient client = null;
            try {
                var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolderOption.DoNotVerify);
                var blenderCommonPath = Path.Combine(programFilesPath, "Blender Foundation");
                var blenderDirectory = Directory.GetDirectories(blenderCommonPath).FirstOrDefault();
                var blenderPath = Path.Combine(blenderDirectory, "blender.exe");

                var tempPath = Path.Combine(Path.GetTempPath(), $"BlenderRenderFarm-{Guid.NewGuid()}");
                Directory.CreateDirectory(tempPath);
                var blendFilePath = Path.Combine(tempPath, "scene.blend");
                var renderOutputDirectory = Path.Combine(tempPath, "out");
                Directory.CreateDirectory(renderOutputDirectory);
                var renderOutputScheme = Path.Combine(renderOutputDirectory, "frame######");

                var render = new BlenderRender(blenderPath, blendFilePath, renderOutputDirectory);

                render.Output += (_, e) => Console.Out.WriteLine(e);
                render.Error += (_, e) => Console.Error.WriteLine(e);
                render.Progress += (_, e) => e.DumpToConsole();

                client = new BlenderRenderFarm.RenderClient();
                client.RenderInit += blendFileBytes => {
                    Console.WriteLine("Received blend from server.");
                    File.WriteAllBytes(blendFilePath, blendFileBytes);
                    Console.WriteLine("Wrote blend to file.");
                };
                client.FrameAssigned += frameIndex => {
                    Console.WriteLine($"Server assigned frame {frameIndex}");
                    Task.Run(async () => {
                        Console.WriteLine($"Rendering frame {frameIndex}...");
                        try {
                            await render.RenderFrameAsync(frameIndex).ConfigureAwait(false);
                            Console.WriteLine($"Finished rendering frame {frameIndex}");
                            var framePath = render.FindFrameFile(frameIndex);
                            var frameBytes = File.ReadAllBytes(framePath);
                            client.SendFrameBytes(frameIndex, frameBytes);
                        } catch (Exception e) {
                            Console.WriteLine($"Render failure: {e}");
                            throw;
                        }
                    });
                };
                client.FramesCancelled += (frameIndex, reason) => {
                    Console.WriteLine("Frame cancelled: " + frameIndex + ", Reason: " + reason);
                };

                await client.ConnectAsync(new IPEndPoint(IPAddress.Parse(serverIp), 42424)).ConfigureAwait(false);
                Console.WriteLine("Started Client - Press enter to stop");

                Console.Read();
            } finally {
                client?.Disconnect();
                client?.Dispose();
            }
        }
    }
}
