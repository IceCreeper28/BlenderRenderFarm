using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BlenderRenderFarm;
using Open.Nat;

namespace BlenderRenderServer {
    public static class Program {
        public static async Task Main() {
            TaskScheduler.UnobservedTaskException += (s, e) => Console.Error.WriteLine(e);

            Console.WriteLine("Enter blend file path:");
            var blendFilePath = Console.ReadLine().Trim();

            var blendFileBytes = File.ReadAllBytes(blendFilePath);
            using RenderServer server = new(blendFileBytes, 0, 10);
            server.FrameReceived += (frameIndex, _) => {
                Console.WriteLine("Frame received: " + frameIndex);
            };
            server.FrameProgress += (frameIndex, remaining) => {
                Console.WriteLine("Frame progress: " + frameIndex + ", ETA: " + remaining);
            };
            server.FrameFailure += (frameIndex, reason) => {
                Console.WriteLine("Frame failure: " + frameIndex + ", Reason: " + reason);
            };

            NatDevice device = null;
            Mapping mapping = null;
            try {
                Console.WriteLine("Seting up port forwarding...");
                var discoverer = new NatDiscoverer();
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts).ConfigureAwait(false);
                var ip = await device.GetExternalIPAsync().ConfigureAwait(false);
                Console.WriteLine("External IP: " + ip);
                mapping = new Mapping(Protocol.Tcp, 42424, 42424, "BlenderRenderFarm");
                await device.CreatePortMapAsync(mapping).ConfigureAwait(false);
                Console.WriteLine("Forwarded port 42424");

                await server.ListenAsync().ConfigureAwait(false);
                Console.WriteLine("Started Server - Press enter to stop");

                Console.Read();
            } finally {
                server?.Stop();
                await device.DeletePortMapAsync(mapping).ConfigureAwait(false);
            }
        }
    }
}
