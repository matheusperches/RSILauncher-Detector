using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using static RSILauncherDetector.Interfaces.RSILauncherDetector;

namespace RSILauncherDetector.Components
{

    [SupportedOSPlatform("windows")]
    public class ProcessHandler(Interfaces.RSILauncherDetector.IProcessManager processManager, Interfaces.RSILauncherDetector.IWatcherFactory watcherFactory, Interfaces.RSILauncherDetector.IProcessTerminationWatcher processTerminationWatcher, string trackIRPath)
    {
        private readonly IProcessManager processManager = processManager;
        private readonly IWatcherFactory watcherFactory = watcherFactory;
        private readonly IProcessTerminationWatcher processTerminationWatcher = processTerminationWatcher;
        private readonly List<IEventWatcher> watchers = [];
        private readonly string trackIRPath = trackIRPath;

        // A dictionary to track all processes in the tree
        private static readonly HashSet<int> trackedProcessIds = [];

        // Flag to track whether the first instance has been detected
        static bool isFirstInstanceDetected = false;

        // The process ID of the first detected process (main process)
        static readonly int firstProcessID = -1;

        public void StartScanning(string gameProcess, string gameExe)
        {
            Process[] existingProcesses = processManager.GetProcessesByName(gameProcess);

            // If there is no process running, create a query and add a watcher for it
            if (existingProcesses.Length == 0)
            {
                string query = $"SELECT * FROM __InstanceCreationEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Name = '{gameExe}'";
                IEventWatcher watcher = watcherFactory.CreateWatcher(query);
                try
                {
                    watcher.EventArrived += new EventArrivedEventHandler(ProcessStarted);
                    watcher.Start();
                    watchers.Add(watcher);
                    DebugLogger.Log($"Listening for {gameExe} process events. Press Enter to exit...");
                }
                catch (Exception ex)
                {
                    DebugLogger.Log(ex.ToString());
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
                        DebugLogger.Log(ex.ToString());
                    }
                }
            }
            Array.Clear(existingProcesses);
        }

        private void ProcessStarted(object sender, EventArrivedEventArgs e)
        {
            using ManagementBaseObject? process = e.NewEvent["TargetInstance"] as ManagementBaseObject;
            if (process != null)
            {
                int processID = Convert.ToInt32(process["ProcessId"]);

                // Monitoring if it's the first instance detected, then launching TrackIR...
                if (!isFirstInstanceDetected)
                {
                    isFirstInstanceDetected = true; // Mark that the first instance has been detected
                    DebugLogger.Log($"First process detected with ID: {processID} \nNot logging subsequent processes...");
                    StartTrackIR(trackIRPath);
                }

                // Use the IProcessTerminationWatcher to add the process to the tracked list
                trackedProcessIds.Add(processID);
                processTerminationWatcher.AddWatcherForProcessTermination(processID);
            }
        }

        private static void MonitorProcessTermination(int firstProcessID)
        {
            throw new NotImplementedException();
        }

        private static void StartTrackIR(string trackIRPath)
        {
            throw new NotImplementedException();
        }
    }

    [SupportedOSPlatform("windows")]
    public class WatcherFactory : IWatcherFactory
    {
        public IEventWatcher CreateWatcher(string query)
        {
            // Create the ManagementEventWatcher with the provided query
            var managementWatcher = new ManagementEventWatcher(query);
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
            ManagementEventWatcher watcher = new();

            watcher.EventArrived += (sender, e) =>
            {
                // Logic to handle process termination
                DebugLogger.Log($"Process with ID {processId} has terminated.");
                // Additional cleanup or logic can be added here
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
}