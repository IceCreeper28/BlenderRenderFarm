using Open.Nat;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RenderServer {
    public static class Program {
        static async Task Main() {
            TaskScheduler.UnobservedTaskException += (s, e) => Console.Error.WriteLine(e);

            Console.WriteLine("Enter blend file path:");
            var blendFilePath = Console.ReadLine().Trim();

            BlenderRenderFarm.RenderServer server = null;
            NatDevice device = null;
            Mapping mapping = null;
            try {
                Console.WriteLine("Seting up port forwarding...");
                var discoverer = new NatDiscoverer();
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);
                var ip = await device.GetExternalIPAsync();
                Console.WriteLine("External IP: " + ip);
                mapping = new Mapping(Protocol.Tcp, 42424, 42424, "BlenderRenderFarm");
                await device.CreatePortMapAsync(mapping);
                Console.WriteLine("Forwarded port 42424");

                var blendFileBytes = File.ReadAllBytes(blendFilePath);
                server = new BlenderRenderFarm.RenderServer(blendFileBytes, 10);
                server.FrameReceived += (frameIndex, bytes) => {
                    Console.WriteLine("Frame received: " + frameIndex);
                };
                server.FrameProgress += (frameIndex, remaining) => {
                    Console.WriteLine("Frame progress: " + frameIndex + ", ETA: " + remaining);
                };
                server.FrameFailure += (frameIndex, reason) => {
                    Console.WriteLine("Frame failure: " + frameIndex + ", Reason: " + reason);
                };

                await server.ListenAsync();
                Console.WriteLine("Started Server - Press enter to stop");

                Console.Read();
            } finally {
                server?.Stop();
                await device?.DeletePortMapAsync(mapping);
            }
        }
    }
}
