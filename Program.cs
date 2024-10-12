using System.ComponentModel;
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
        static HashSet<int> trackedProcessIds = [];

        // Flag to track whether the first instance has been detected
        static bool isFirstInstanceDetected = false;

        // The process ID of the first detected process (main process)
        static int mainProcessId = -1;

        // The processes we want to track and start / stop
        static readonly string gameExe = "RSI Launcher.exe";
        static readonly string gameProcess = "RSI Launcher";
        static readonly string trackIRProcess = "TrackIR5";
        static readonly string trackIRPath = "C:\\Program Files (x86)\\TrackIR5\\TrackIR5.exe"; // Assumes default installation path of TrackIR5
        static readonly ManualResetEvent resetEvent = new(false);
        static void Main()
        {
            // Tries to create a new task, if none exists
            TaskSchedulerSetup.CreateTask();

            Process[] existingProcess = Process.GetProcessesByName(gameProcess);
            // If there is no process running, create a query and add a watcher for it. 
            if (existingProcess.Length == 0)
            {
                // Query for when the main process starts
                string processStartQuery = $"SELECT * FROM __InstanceCreationEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Name = '{gameExe}'";
                using (ManagementEventWatcher watcher = new(new WqlEventQuery(processStartQuery)))
                {
                    // Subscribe to the process start event
                    try
                    {
                        watcher.EventArrived += new EventArrivedEventHandler(ProcessStarted);
                        watcher.Start();
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
            resetEvent.WaitOne(); // Block the main thread here until resetEvent.Set() is called
        }

        // Add a ManagementEventWatcher for process termination based on Process ID, for already running process detected upon launch.
        static void AddWatcherForProcessTermination(int processId)
        {
            // WMI query to detect process termination by Process ID
            string processEndQuery = $"SELECT * FROM __InstanceDeletionEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Handle = '{processId}'";
            using ManagementEventWatcher watcher = new(new WqlEventQuery(processEndQuery));
            try
            {
                // Subscribe to the process termination event
                watcher.EventArrived += new EventArrivedEventHandler(ProcessTerminated);
                watcher.Start();
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
        static void ProcessStarted(object sender, EventArrivedEventArgs e)
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
        static void MonitorProcessTermination(int processId)
        {
            string processEndQuery = $"SELECT * FROM __InstanceDeletionEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Handle = {processId}";
            using (ManagementEventWatcher endWatcher = new(new WqlEventQuery(processEndQuery)))
            {
                try
                {
                    endWatcher.EventArrived += new EventArrivedEventHandler(ProcessTerminated);
                    endWatcher.Start();
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"MonitorProcessTermination Error: {ex.Message}");
                }
            };
        }

        // Called when a process in the system is terminated
        static void ProcessTerminated(object sender, EventArrivedEventArgs e)
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
                            trackedProcessIds = [];
                            //Main();
                        }
                    }
                }
            };
        }

        // This method will be called after the first instance of the tracked process is detected
        static void StartTrackIR(string programPath)
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
        static void TerminateTrackIR()
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
                        trackedProcessIds.Remove(process.Id);
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

                // Check if all tracked processes are terminated outside the loop
                if (trackedProcessIds.Count == 0)
                {
                    DebugLogger.Log("All processes in the tree have been terminated.");
                }
            }

            catch (Exception ex)
            {
                DebugLogger.Log($"Failed to kill task: {ex.Message}");
            }
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