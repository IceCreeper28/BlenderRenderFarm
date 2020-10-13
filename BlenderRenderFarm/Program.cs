using BlenderRenderFarm.Extensions;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BlenderRenderFarm {
    class Program {
        static async Task Main(string[] args) {
            // var output = BlenderRenderProgressOutput.FromLine(@"Fra:1 Mem:1217.50M (0.00M, Peak 1928.75M) | Time:01:42.41 | Remaining:01:41.37 | Mem:1050.48M, Peak:1052.07M | Scene, View Layer | Rendered 66/135 Tiles, Sample 72/128, Denoised 50 tiles");
            // output.DumpToConsole();

            var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolderOption.DoNotVerify);
            var blenderCommonPath = Path.Combine(programFilesPath, "Blender Foundation");
            var blenderDirectory = Directory.GetDirectories(blenderCommonPath).FirstOrDefault();
            var blenderPath = Path.Combine(blenderDirectory, "blender.exe");

            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop, Environment.SpecialFolderOption.DoNotVerify);

            var render = new BlenderRender() {
                BlenderPath = blenderPath,
                BlendFilePath = Path.Combine(desktopPath, @"Test\test.blend"),
                OutputDirectory = Path.Combine(desktopPath, @"Test\out\frame######.frame"),
                Frame = ^10
            };
            render.Output += (s, e) => Console.Out.WriteLine(e);
            render.Error += (s, e) => Console.Error.WriteLine(e);
            render.Progress += (s, e) => e.DumpToConsole();
            await render.RunAsync();

            Console.Read();
        }
    }
}
