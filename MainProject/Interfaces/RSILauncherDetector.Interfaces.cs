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
    }
}
