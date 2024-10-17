using System.Diagnostics;
using System.Management;

namespace RSILauncherDetector.Interfaces
{
    public class RSILauncherDetector
    {
        public interface IProcessWrapper
        {
            Process[] GetProcessesByName(string processName);
            Process StartProcess(string fileName, string workingDirectory);
        }

        public interface IEventWatcher
        {
            void Start();
            void Stop();
            event EventArrivedEventHandler EventArrived;
        }
        public interface IWatcherFactory
        {
            IEventWatcher CreateWatcher(string query);
        }
        public interface IProcessTerminationWatcher
        {
            void WatchForProcessTermination(int processId);
        }

        public interface ITrackIRController
        {
            void StartTrackIR(string trackIRProcess, string path);
            void TerminateTrackIR(string processName);
        }

        public interface IWatcherCleaner
        {
            void CleanupWatchers(List<IEventWatcher> watchers);
        }

        public interface IDebugLogger
        {
            public static void Log(string message)
            {
                Console.WriteLine(message);
            }
        }
    }
}
