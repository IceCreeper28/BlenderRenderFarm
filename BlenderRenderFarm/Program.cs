namespace BlenderRenderFarm {

    /*
    public enum Message {
        TestA,
        TestB
    }
    public static class Program {
        static async Task Main(string[] args) {

            

            Test();
            void Test() {
                var bytes = new byte[10];
                var span = bytes.AsSpan();
                var writer = new SpanWriter(span);
                writer.WriteEnum(Message.TestA);
                writer.WriteEnum(Message.TestB);
                var reader = new SpanReader(span);
                Console.WriteLine(reader.ReadEnum<Message>());
                Console.WriteLine(reader.ReadEnum<Message>());
            }


            var discoverer = new NatDiscoverer();
            var cts = new CancellationTokenSource(10000);
            var device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);
            var ip = await device.GetExternalIPAsync();
            Console.WriteLine(ip);
            await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 24242, 24242, "Test mapping"));


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
                RenderOutput = Path.Combine(desktopPath, @"//out\frame######.test")
            };

            render.Output += (s, e) => Console.Out.WriteLine(e);
            render.Error += (s, e) => Console.Error.WriteLine(e);
            render.Progress += (s, e) => e.DumpToConsole();
            await render.RenderFrameAsync(^10);

            Console.Read();
        }
    }*/
}
