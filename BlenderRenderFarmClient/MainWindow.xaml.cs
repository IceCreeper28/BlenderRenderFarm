using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using BlenderRenderFarm;

namespace BlenderRenderFarmClient {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
        }

        private void LogToGUIConsole(string message) {
            LogOutput.Text += message + "\n";
            LogOutputContainer.ScrollToBottom();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e) {
            IPAddress[] IPs = resolveIPAddresses(IPInput.Text);
            if (IPs == null) {
                //Output has already been printed by resolveIpAddresses()
            } else if (!int.TryParse(PortInput.Text, out int PortNumber) || PortNumber > 65565 || PortNumber < 1) {
                LogToGUIConsole($"\"{PortInput.Text}\" is an invalid port number!");
            } else {
                LogToGUIConsole($"Connecting to Server at address {IPs[0]}:{PortNumber}...");
            }

            //Start the BlenderRenderFarmClient
            Task.Run(async () => {
                int tmp = 0;
                while (tmp < 100) {
                    await Task.Delay(100);
                    Dispatcher.Invoke(() => {
                        RenderingProgressBar.Value += 2;
                        tmp += 2;
                    });
                }
                tmp = 0;
                while (tmp < 100) {
                    await Task.Delay(100);
                    Dispatcher.Invoke(() => {
                        CompositingProgressBar.Value += 2;
                        tmp += 2;
                    });
                }
            });
        }

        IPAddress[] resolveIPAddresses(string ipstr) {
            try {
                return Dns.GetHostAddresses(ipstr);
            } catch (ArgumentException) {
                LogToGUIConsole($"Invalid host \"{ipstr}\"!");
            } catch (SocketException) {
                LogToGUIConsole($"Failed to resolve host \"{ipstr}\"!");
            }

            return null;
        }

        //Handle connect button enabled state
        private void IPInput_TextChanged(object sender, TextChangedEventArgs e) {
            Trace.WriteLine("IPInput_TextChanged();");
            ValidateConnectButton();
        }

        private void PortInput_TextChanged(object sender, TextChangedEventArgs e) {
            Trace.WriteLine("PortInput_TextInput();");
            ValidateConnectButton();
        }

        private void ValidateConnectButton() {
            ConnectButton.IsEnabled = IPInput.Text.Length != 0 && PortInput.Text.Length != 0;
        }
        //Handle connect button enabled state END

        //public static async Task StartClient() {
        //    TaskScheduler.UnobservedTaskException += (s, e) => Console.Error.WriteLine(e);

        //    Console.WriteLine("Enter server ip:");
        //    var serverIp = Console.ReadLine().Trim();

        //    BlenderRenderFarm.RenderClient client = null;
        //    try {
        //        var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolderOption.DoNotVerify);
        //        var blenderCommonPath = Path.Combine(programFilesPath, "Blender Foundation");
        //        var blenderDirectory = Directory.GetDirectories(blenderCommonPath).FirstOrDefault();
        //        var blenderPath = Path.Combine(blenderDirectory, "blender.exe");

        //        var tempPath = Path.Combine(Path.GetTempPath(), $"BlenderRenderFarm-{Guid.NewGuid()}");
        //        Directory.CreateDirectory(tempPath);
        //        var blendFilePath = Path.Combine(tempPath, "scene.blend");
        //        var renderOutputDirectory = Path.Combine(tempPath, "out");
        //        Directory.CreateDirectory(renderOutputDirectory);
        //        var renderOutputScheme = Path.Combine(renderOutputDirectory, "frame######");

        //        var render = new BlenderRender(blenderPath, blendFilePath, renderOutputDirectory);

        //        render.Output += (_, e) => Console.Out.WriteLine(e);
        //        render.Error += (_, e) => Console.Error.WriteLine(e);
        //        render.Progress += (_, e) => e.DumpToConsole();

        //        client = new BlenderRenderFarm.RenderClient();
        //        client.RenderInit += blendFileBytes => {
        //            Console.WriteLine("Received blend from server.");
        //            File.WriteAllBytes(blendFilePath, blendFileBytes);
        //            Console.WriteLine("Wrote blend to file.");
        //        };
        //        client.FrameAssigned += frameIndex => {
        //            Console.WriteLine($"Server assigned frame {frameIndex}");
        //            Task.Run(async () => {
        //                Console.WriteLine($"Rendering frame {frameIndex}...");
        //                try {
        //                    await render.RenderFrameAsync(frameIndex).ConfigureAwait(false);
        //                    Console.WriteLine($"Finished rendering frame {frameIndex}");
        //                    var framePath = render.FindFrameFile(frameIndex);
        //                    var frameBytes = File.ReadAllBytes(framePath);
        //                    client.SendFrameBytes(frameIndex, frameBytes);
        //                } catch (Exception e) {
        //                    Console.WriteLine($"Render failure: {e}");
        //                    throw;
        //                }
        //            });
        //        };
        //        client.FramesCancelled += (frameIndex, reason) => {
        //            Console.WriteLine("Frame cancelled: " + frameIndex + ", Reason: " + reason);
        //        };

        //        await client.ConnectAsync(new IPEndPoint(IPAddress.Parse(serverIp), 42424)).ConfigureAwait(false);
        //        Console.WriteLine("Started Client - Press enter to stop");

        //        Console.Read();
        //    } finally {
        //        client?.Disconnect();
        //        client?.Dispose();
        //    }
        //}
    }
}
