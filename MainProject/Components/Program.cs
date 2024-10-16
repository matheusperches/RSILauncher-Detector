using System.Runtime.Versioning;

namespace RSILauncherDetector.Components
{
    [SupportedOSPlatform("windows")]
    public static class Program
    {

        // The processes we want to track and start / stop
        private static readonly string launcherProcessName = "RSI Launcher";
        private static readonly string launcherExeName = "RSI Launcher.exe";
        private static readonly string trackIRProcess = "TrackIR5";
        private static readonly string trackIRPath = "C:\\Program Files (x86)\\TrackIR5\\TrackIR5.exe"; // Assumes default installation path of TrackIR5

        // Keeping the application alive
        static readonly ManualResetEvent resetEvent = new(false);
        public static void Main()
        {

            // Create an instance of the ProcessHandler with all dependencies
            var processHandler = new ProcessHandler(
                launcherProcessName,
                launcherExeName,
                trackIRPath,
                trackIRProcess
            );

            // Start the scanning process
            processHandler.StartScanning();

            resetEvent.WaitOne(); // Block the main thread here
        }
    }
}