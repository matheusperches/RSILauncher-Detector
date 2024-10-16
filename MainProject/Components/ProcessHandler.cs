using Microsoft.Win32;
using System;
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
        private bool wasFirstInstanceDetected = false;

        public void StartScanning()
        {
            try
            {
                Process[] existingProcesses = Process.GetProcessesByName(launcherProcessName);

                // Subscribing to system power change events to reset the watchers
                SystemEvents.PowerModeChanged +=  OnSystemResume;

                // Setup watcher for future process creation events
                string query = $"SELECT * FROM __InstanceCreationEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Name = '{launcherExeName}'";
                IEventWatcher watcher = watcherFactory.CreateWatcher(query);
                watcher.EventArrived += new EventArrivedEventHandler(OnLauncherStarted);
                watcher.Start();
                watchers.Add(watcher);
                IDebugLogger.Log($"Listening for {launcherExeName} process events. Press Enter to exit...");

                // If there are existing processes, setup termination watchers for them
                foreach (Process process in existingProcesses)
                {
                    IDebugLogger.Log($"Found {launcherExeName} process with ID {process.Id}, monitoring termination...");

                    // Adding to the tracked list
                    trackedProcessIds.Add(process.Id);

                    processTerminationWatcher.WatchForProcessTermination(process.Id);
                }

                // Clean up array after use
                Array.Clear(existingProcesses);
            }
            catch (Exception ex)
            {
                IDebugLogger.Log(ex.ToString());
            }
        }

        private void OnSystemResume(object sender, PowerModeChangedEventArgs e)
        {
            IDebugLogger.Log($"System sleep detected. Resetting watchers...");
            HandleAllProcessesTerminated();
        }

        private void OnLauncherStarted(object sender, EventArrivedEventArgs e)
        {
            using ManagementBaseObject? process = e.NewEvent["TargetInstance"] as ManagementBaseObject;
            if (process != null)
            {
                int processID = Convert.ToInt32(process["ProcessId"]);

                // Monitoring if it's the first instance detected, then launching TrackIR...
                if (!wasFirstInstanceDetected)
                {
                    wasFirstInstanceDetected = true; // Mark that the first instance has been detected
                    IDebugLogger.Log($"First process detected with ID: {processID} \nNot logging subsequent processes...");
                    trackIRController.StartTrackIR(trackIRProcess, trackIRPath);
                    processTerminationWatcher.WatchForProcessTermination(processID);
                }

                // Adding to the tracked list
                trackedProcessIds.Add(processID);

                // Subscribing to the ProcessTerminated event
                processTerminationWatcher.ProcessTerminated += () =>
                {
                    OnProcessTerminated(sender, e);
                };
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
                        IDebugLogger.Log("All processes in the tree have been terminated.");
                        trackIRController.TerminateTrackIR(trackIRProcess);
                        HandleAllProcessesTerminated();
                    }
                }
            }
        }

        private void HandleAllProcessesTerminated()
        {
            ResetProcessTracking();
            watcherCleaner.CleanupWatchers(watchers);
        }

        private void ResetProcessTracking()
        {
            wasFirstInstanceDetected = false;
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
            string query = $"SELECT * FROM __InstanceDeletionEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.ProcessId = {processId}";
            try
            {
                ManagementEventWatcher watcher = new(query);

                watcher.EventArrived += (sender, e) =>
                {
                    // Extract the process object from the event arguments

                    if (e.NewEvent["TargetInstance"] is ManagementBaseObject targetInstance)
                    {
                        // Get the process name and ID from the TargetInstance
                        string processName = targetInstance["Name"]?.ToString() ?? "Unknown";
                        int terminatedProcessId = Convert.ToInt32(targetInstance["ProcessId"]);

                        IDebugLogger.Log($"Process '{processName}' with ID '{terminatedProcessId}' has terminated.");

                        // Dispose of the watcher
                        watcher.Dispose();

                        // Raise the event to notify subscribers
                        ProcessTerminated?.Invoke();
                    }
                };
                watcher.Start();
                watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                IDebugLogger.Log($"Exception: {ex.Message}");
            }
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
}