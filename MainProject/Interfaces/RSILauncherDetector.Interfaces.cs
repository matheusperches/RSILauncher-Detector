using System.Diagnostics;
using System.Management;

namespace RSILauncherDetector.Interfaces
{
    public class RSILauncherDetector
    {
        public interface IProcessManager
        {
            Process[] GetProcessesByName(string processName);
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
            void AddWatcherForProcessTermination(int processId);
        }

        public interface ITrackIRController
        {
            void StartTrackIR(string trackIRProcess, string path);
            void TerminateTrackIR(string trackIRProcess);
        }

        public interface IWatcherManager
        {
            void CleanupWatchers();
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
