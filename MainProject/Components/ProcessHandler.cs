using Microsoft.VisualStudio.TestPlatform.TestHost;
using Microsoft.Win32;
using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using static RSILauncherDetector.Interfaces.RSILauncherDetector;

namespace RSILauncherDetector.Components
{

    [SupportedOSPlatform("windows")]
    public class ProcessHandler(
    string launcherProcessName,
    string launcherExeName,
    string trackIRPath,
    string trackIRProcess
    )
    {

        // Initializing the dependencies
        private readonly WatcherFactory watcherFactory = new();
        private readonly ProcessTerminationWatcher processTerminationWatcher = new();
        private readonly TrackIRController trackIRController = new();
        private readonly WatcherCleaner watcherCleaner = new();

        // Other parameters
        private readonly string launcherProcessName = launcherProcessName;
        private readonly string launcherExeName = launcherExeName;
        private readonly string trackIRPath = trackIRPath;
        private readonly string trackIRProcess = trackIRProcess;

        private readonly List<IEventWatcher> watchers = []; // List of event watchrs
        private readonly HashSet<int> trackedProcessIds = []; // Dictionary to track all processes in the tree

        // Flag and ID for first process detection
        private bool isFirstInstanceDetected = false;

        public void StartScanning()
        {
            Process[] existingProcesses = Process.GetProcessesByName(launcherProcessName);

            // If there is no process running, create a query and add a watcher for it
            if (existingProcesses.Length == 0)
            {
                string query = $"SELECT * FROM __InstanceCreationEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Name = '{launcherExeName}'";
                IEventWatcher watcher = watcherFactory.CreateWatcher(query);
                try
                {
                    watcher.EventArrived += new EventArrivedEventHandler(OnLauncherStarted);
                    watcher.Start();
                    watchers.Add(watcher);
                    IDebugLogger.Log($"Listening for {launcherExeName} process events. Press Enter to exit...");
                }
                catch (Exception ex)
                {
                    IDebugLogger.Log(ex.ToString());
                }
            }
            else
            {
                foreach (Process process in existingProcesses)
                {
                    try
                    {
                        processTerminationWatcher.WatchForProcessTermination(process.Id);
                    }
                    catch (Exception ex)
                    {
                        IDebugLogger.Log(ex.ToString());
                    }
                }
            }
            Array.Clear(existingProcesses);
        }

        private void OnLauncherStarted(object sender, EventArrivedEventArgs e)
        {

            using ManagementBaseObject? process = e.NewEvent["TargetInstance"] as ManagementBaseObject;
            if (process != null)
            {
                int processID = Convert.ToInt32(process["ProcessId"]);

                // Monitoring if it's the first instance detected, then launching TrackIR...
                if (!isFirstInstanceDetected)
                {
                    isFirstInstanceDetected = true; // Mark that the first instance has been detected
                    IDebugLogger.Log($"First process detected with ID: {processID} \nNot logging subsequent processes...");
                    trackIRController.StartTrackIR(trackIRProcess, trackIRPath);

                    // Subscribing to the ProcessTerminated event
                    processTerminationWatcher.ProcessTerminated += () =>
                    {
                        trackIRController.TerminateTrackIR(trackIRProcess);
                    };
                }

                // Adding to the tracked list
                trackedProcessIds.Add(processID);
                processTerminationWatcher.WatchForProcessTermination(processID);
            }
        }

        // Called when a process in the system is terminated
        public void OnProcessTerminated(object sender, EventArrivedEventArgs e)
        {
            using ManagementBaseObject? process = e.NewEvent["TargetInstance"] as ManagementBaseObject;
            if (process != null)
            {
                int processId = Convert.ToInt32(process["ProcessId"]);

                if (trackedProcessIds.Contains(processId))
                {
                    IDebugLogger.Log($"Process {processId} has been terminated.");
                    trackedProcessIds.Remove(processId);

                    if (trackedProcessIds.Count == 0)
                    {
                        HandleAllProcessesTerminated();
                        StartScanning();
                    }
                }
            }
        }

        private void HandleAllProcessesTerminated()
        {
            IDebugLogger.Log("All processes in the tree have been terminated.");
            ResetProcessTracking();
            watcherCleaner.CleanupWatchers(watchers);
        }

        private void ResetProcessTracking()
        {
            isFirstInstanceDetected = false;
            trackedProcessIds.Clear();
        }
    }

    [SupportedOSPlatform("windows")]
    public class WatcherFactory : IWatcherFactory
    {
        public IEventWatcher CreateWatcher(string query)
        {
            // Create the ManagementEventWatcher with the provided query
            using ManagementEventWatcher managementWatcher = new(query);
            // Return a wrapped instance of IEventWatcher
            return new EventWatcherWrapper(managementWatcher);
        }
    }

    [SupportedOSPlatform("windows")]
    public class EventWatcherWrapper(ManagementEventWatcher watcher) : IEventWatcher
    {
        private readonly ManagementEventWatcher watcher = watcher;

        public void Start()
        {
            watcher.Start();
        }

        public void Stop()
        {
            watcher.Stop();
        }

        public event EventArrivedEventHandler EventArrived
        {
            add { watcher.EventArrived += value; }
            remove { watcher.EventArrived -= value; }
        }
    }

    [SupportedOSPlatform("windows")]
    public class ProcessTerminationWatcher : IProcessTerminationWatcher
    {
        private readonly List<ManagementEventWatcher> watchers = [];

        // This method raises the event

        public event Action? ProcessTerminated;

        public void WatchForProcessTermination(int processId)
        {
            //Process[] watchedProcesses = Process.GetProcesses(Process.GetProcessById(processId).ProcessName);

            IDebugLogger.Log("Waiting for process termination...");
            string query = $"SELECT * FROM __InstanceDeletionEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.ProcessId = {processId}";
            ManagementEventWatcher watcher = new(query);

            watcher.EventArrived += (sender, e) =>
            {
                // Logic to handle process termination
                IDebugLogger.Log($"Process with ID {processId} has terminated.");
                watcher.Dispose();

                // Raise the event to notify subscribers
                ProcessTerminated?.Invoke();
            };
            watcher.Start();
            watchers.Add(watcher);
        }
    }

    [SupportedOSPlatform("windows")]
    public class WatcherCleaner : IWatcherCleaner 
    {
        public void CleanupWatchers(List<IEventWatcher> watchers)
        {
            foreach (ManagementEventWatcher watcher in watchers.Cast<ManagementEventWatcher>())
            {
                watcher.Dispose();
            }
        }
    }


    [SupportedOSPlatform("windows")]
    public class TrackIRController : ITrackIRController
    {
        public void StartTrackIR(string trackIRProcess, string path)
        {
            try
            {
                Process[] existingProcess = Process.GetProcessesByName(trackIRProcess);
                // Checking if there is already a process running
                if (existingProcess.Length == 0)
                {
                    using (Process trackirProc = new())
                    {
                        trackirProc.StartInfo.FileName = path;
                        trackirProc.StartInfo.WorkingDirectory = Path.GetDirectoryName(path);
                        trackirProc.Start();
                        IDebugLogger.Log($"{trackIRProcess} started...");
                    };
                }
                else
                    IDebugLogger.Log($"An instance of {trackIRProcess} is already running.");
            }
            catch (Exception ex)
            {
                IDebugLogger.Log($"Failed to start task: {ex}");
            }
        }

        public void TerminateTrackIR(string processName)
        {
            try
            {
                IDebugLogger.Log("Searching for TrackIR5 process...");
                Process[] existingProcess = Process.GetProcessesByName(processName);
                foreach (Process singleProcess in existingProcess)
                {
                    singleProcess.Kill();
                    IDebugLogger.Log($"{singleProcess.ProcessName} found and terminated with ID {singleProcess.Id}.");
                }

                if (existingProcess.Length == 0)
                {
                    IDebugLogger.Log($"No processes found.");
                }
            }
            catch (Exception ex)
            {
                IDebugLogger.Log($"Failed to terminate the Process: {ex.Message}");
            }
        }
    }

    [SupportedOSPlatform("windows")]
    public class PowerModeHandler : IPowerModeHandler
    {
        public void OnSystemResume(object? sender, PowerModeChangedEventArgs e)
        {
            if (e != null && e.Mode == PowerModes.Resume)
            {
                Console.WriteLine("System resumed from sleep, restarting event watchers...");
            }
        }
    }
}