using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using static RSILauncherDetector.Interfaces.RSILauncherDetector;

namespace RSILauncherDetector.Components
{

    [SupportedOSPlatform("windows")]
    public class ProcessHandler(
        Interfaces.RSILauncherDetector.IProcessManager processManager, 
        Interfaces.RSILauncherDetector.IWatcherFactory watcherFactory, 
        Interfaces.RSILauncherDetector.IProcessTerminationWatcher processTerminationWatcher, 
        Interfaces.RSILauncherDetector.ITrackIRController trackIRController,
        Interfaces.RSILauncherDetector.IWatcherManager watcherManager,
        string trackIRPath
        )
    {
        private readonly IProcessManager processManager = processManager;
        private readonly IWatcherFactory watcherFactory = watcherFactory;
        private readonly IProcessTerminationWatcher processTerminationWatcher = processTerminationWatcher;
        private readonly List<IEventWatcher> watchers = [];
        private readonly ITrackIRController trackIRController = trackIRController;
        private readonly IWatcherManager watcherManager = watcherManager;
        private readonly string trackIRPath = trackIRPath;

        // A dictionary to track all processes in the tree
        private readonly HashSet<int> trackedProcessIds = [];

        // New fields for gameProcess and gameExe
        public required string launcherProcessName;
        public required string launcherExeName;

        public readonly string trackIRProcess = "TrackIR5";

        // Flag and ID for first process detection
        private bool isFirstInstanceDetected = false;
        private int firstProcessID = -1;

        public void StartScanning()
        {
            Process[] existingProcesses = processManager.GetProcessesByName(launcherProcessName);

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
                        processTerminationWatcher.AddWatcherForProcessTermination(process.Id);
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
                    trackIRController.StartTrackIR(trackIRProcess,trackIRPath);
                }

                // Use the IProcessTerminationWatcher to add the process to the tracked list
                trackedProcessIds.Add(processID);
                processTerminationWatcher.AddWatcherForProcessTermination(processID);
            }
        }

        // Called when a process in the system is terminated
        public void ProcessTerminated(object sender, EventArrivedEventArgs e)
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
                        StartScanning();  // gameProcess and gameExe are class-level fields
                    }
                }
            }
        }

        private void HandleAllProcessesTerminated()
        {
            IDebugLogger.Log("All processes in the tree have been terminated.");
            trackIRController.TerminateTrackIR(trackIRProcess);
            ResetTrackingState();
            watcherManager.CleanupWatchers();
        }

        private void ResetTrackingState()
        {
            isFirstInstanceDetected = false;
            firstProcessID = -1;
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

        public void AddWatcherForProcessTermination(int processId)
        {
            string query = $"SELECT * FROM __InstanceDeletionEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.ProcessId = {processId}";
            using ManagementEventWatcher watcher = new();

            watcher.EventArrived += (sender, e) =>
            {
                // Logic to handle process termination
                IDebugLogger.Log($"Process with ID {processId} has terminated.");
            };
            watcher.Start();
            watchers.Add(watcher);
        }

        public void CleanupWatchers()
        {
            foreach (var watcher in watchers)
            {
                watcher.Stop();
                watcher.Dispose();
            }
            watchers.Clear();
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

        public void TerminateTrackIR(string TrackIRProcess)
        {
            try
            {
                IDebugLogger.Log("Searching for TrackIR5 process...");

                var trackIRProcesses = Process.GetProcessesByName(TrackIRProcess);
                foreach (var process in trackIRProcesses)
                {
                    process.Kill();
                    IDebugLogger.Log($"{TrackIRProcess} process with ID {process.Id} terminated.");
                }

                if (trackIRProcesses.Length == 0)
                {
                    IDebugLogger.Log($"No {TrackIRProcess} processes found.");
                }
            }
            catch (Exception ex)
            {
                IDebugLogger.Log($"Failed to terminate {TrackIRProcess}: {ex.Message}");
            }
        }
    }
}