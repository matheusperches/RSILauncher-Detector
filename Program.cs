﻿using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using RSILauncherDetectorSetup;

namespace RSILauncherDetector
{
    [SupportedOSPlatform("windows")]
    public static class Program
    {

        // A dictionary to track all processes in the tree
        static readonly HashSet<int> trackedProcessIds = [];

        // Flag to track whether the first instance has been detected
        static bool isFirstInstanceDetected = false;

        // The process ID of the first detected process (main process)
        static int mainProcessId = -1;

        // The processes we want to track and start / stop
        static readonly string gameExe = "RSI Launcher.exe";
        static readonly string gameProcess = "RSI Launcher";
        static readonly string trackIRProcess = "TrackIR5";
        static readonly string trackIRPath = "C:\\Program Files (x86)\\TrackIR5\\TrackIR5.exe"; // Assumes default installation path of TrackIR5

        // Keeping the application alive
        static readonly ManualResetEvent resetEvent = new(false);

        // Maintaining a list of watchers
        private static List<ManagementEventWatcher> watchers = [];

        public static void Main()
        {
            // Tries to create a new task, if none exists
            TaskSchedulerSetup.CreateTask();

            StartScanning();
            
            resetEvent.WaitOne(); // Block the main thread here
        }

        private static void StartScanning()
        {
            Process[] existingProcess = Process.GetProcessesByName(gameProcess);
            // If there is no process running, create a query and add a watcher for it. 
            if (existingProcess.Length == 0)
            {
                // Query for when the main process starts
                using (ManagementEventWatcher watcher = new(new WqlEventQuery($"SELECT * FROM __InstanceCreationEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Name = '{gameExe}'")))
                {
                    // Subscribe to the process start event
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
                        return;
                    }
                };
            }
            else
            {
                foreach (Process process in existingProcess)
                {
                    try
                    {
                        DebugLogger.Log($"Already running {process.ProcessName} detected with ID: {process.Id} \n ");
                        AddWatcherForProcessTermination(process.Id);
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log(ex.ToString());
                        return;
                    }
                }
            }
            Array.Clear(existingProcess);
        }

        // Add a ManagementEventWatcher for process termination based on Process ID, for already running process detected upon launch.
        private static void AddWatcherForProcessTermination(int processId)
        {
            // WMI query to detect process termination by Process ID
            using ManagementEventWatcher watcher = new(new WqlEventQuery($"SELECT * FROM __InstanceDeletionEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Handle = '{processId}'"));
            try
            {
                // Subscribe to the process termination event
                watcher.EventArrived += new EventArrivedEventHandler(ProcessTerminated);
                watcher.Start();
                watchers.Add(watcher);
                DebugLogger.Log($"Monitoring termination of process with ID {processId}...");
                trackedProcessIds.Add(processId);
                StartTrackIR(trackIRPath);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"AddWatcherForProcessTermination Error: {ex.Message}");
            }
        }

        // Monitor the process for when an instance is not detected upon launch.
        private static void ProcessStarted(object sender, EventArrivedEventArgs e)
        {
            using (ManagementBaseObject? process = e.NewEvent["TargetInstance"] as ManagementBaseObject)
            {
                if (process != null)
                {
                    mainProcessId = Convert.ToInt32(process["ProcessId"]);

                    // Monitoring if its the first instance detected, then launching TrackIR5... 
                    if (!isFirstInstanceDetected)
                    {
                        DebugLogger.Log($"First process detected with ID: {mainProcessId} \nNot logging subsequent processes..."); 
                        StartTrackIR(trackIRPath);
                    }
                    else
                        isFirstInstanceDetected = true; // Mark that the first instance has been detected

                    // Add the main process to the tracked process list 
                    trackedProcessIds.Add(mainProcessId);
                    // Start watching for process termination events
                    MonitorProcessTermination(mainProcessId);
                }
            };
        }

        // Monitor process termination events for the tracked process
        private static void MonitorProcessTermination(int processId)
        {
            using (ManagementEventWatcher endWatcher = new(new WqlEventQuery($"SELECT * FROM __InstanceDeletionEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Handle = {processId}")))
            {
                try
                {
                    endWatcher.EventArrived += new EventArrivedEventHandler(ProcessTerminated);
                    endWatcher.Start();
                    watchers.Add(endWatcher);
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"MonitorProcessTermination Error: {ex.Message}");
                }
            };
        }

        // Called when a process in the system is terminated
        private static void ProcessTerminated(object sender, EventArrivedEventArgs e)
        {
            using (ManagementBaseObject? process = e.NewEvent["TargetInstance"] as ManagementBaseObject)
            {
                if (process != null)
                {
                    int processId = Convert.ToInt32(process["ProcessId"]);

                    // Check if the terminated process is part of the tracked process tree
                    if (trackedProcessIds.Contains(processId))
                    {
                        DebugLogger.Log($"Process {processId} has been terminated.");

                        // Remove the process from the tracked list and terminate TrackIR5 software
                        trackedProcessIds.Remove(processId);

                        if (trackedProcessIds.Count == 0)
                        {
                            DebugLogger.Log("All processes in the tree have been terminated.");
                            TerminateTrackIR();
                            // Resetting variables
                            isFirstInstanceDetected = false;
                            mainProcessId = -1;
                            trackedProcessIds.Clear();
                            CleanupWatchers();
                            StartScanning();
                        }
                    }
                }
            };
        }

        // This method will be called after the first instance of the tracked process is detected
        private static void StartTrackIR(string programPath)
        {
            try
            {
                Process[] existingProcess = Process.GetProcessesByName(trackIRProcess);
                // Checking if there is already a process running
                if (existingProcess.Length == 0)
                {
                    Process.Start(programPath);
                    DebugLogger.Log($"{trackIRProcess} started...");
                }
                else
                    DebugLogger.Log($"An instance of {trackIRProcess} is already running.");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Failed to start task: {ex}");
            }
        }

        // This method will be called when all processes in the tree are terminated
        private static void TerminateTrackIR()
        {
            try
            {
                DebugLogger.Log("Searching for TrackIR5 process...");
                Process[] processes = Process.GetProcessesByName(trackIRProcess);

                if (processes.Length == 0)
                {
                    DebugLogger.Log("No TrackIR5 processes found.");
                    return;
                }

                foreach (Process process in processes)
                {
                    try
                    {
                        process.Kill();
                        // If tracking process IDs, remove them after termination
                        trackedProcessIds.Clear();
                    }
                    catch (Win32Exception win32Ex)
                    {
                        DebugLogger.Log($"Failed to kill process {process.Id} due to insufficient privileges: {win32Ex.Message}");
                    }
                    catch (InvalidOperationException invalidOpEx)
                    {
                        DebugLogger.Log($"Process {process.Id} has already exited: {invalidOpEx.Message}");
                    }
                    DebugLogger.Log($"Process {process.Id} has been terminated.");
                }
            }

            catch (Exception ex)
            {
                DebugLogger.Log($"Failed to kill task: {ex.Message}");
            }
        }
        private static void CleanupWatchers()
        {
            foreach (var watcher in watchers)
            {
                watcher.EventArrived -= ProcessTerminated; // Unsubscribe the event handler
                watcher.Stop();
                watcher.Dispose();
            }
            watchers.Clear(); // Clear the list after disposing
            GC.Collect();
        }
    }

    public static class DebugLogger
    {
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}