using System;
using System.CodeDom;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;

class Program
{
    // A dictionary to track all processes in the tree
    static HashSet<int> trackedProcessIds = [];

    // Flag to track whether the first instance has been detected
    static bool isFirstInstanceDetected = false;

    // The process ID of the first detected process (main process)
    static int mainProcessId = -1;

    // The processes we want to track and start / stop
    static string gameExe = "RSI Launcher.exe";
    static string gameProcess = "RSI Launcher";
    static string trackIRProcess = "TrackIR5";
    static string trackIRPath = "C:\\Program Files (x86)\\TrackIR5\\TrackIR5.exe";

    static void Main()
    {
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
                    Console.WriteLine($"Listening for {gameExe} process events. Press Enter to exit...");
                    Console.ReadLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    Console.ReadLine();
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
                    Console.WriteLine($"Already running {process.ProcessName} detected with ID: {process.Id} \n ");
                    AddWatcherForProcessTermination(process.Id);
                    Console.ReadLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    return;
                }
            }
        }
    }
    // Add a ManagementEventWatcher for process termination based on Process ID, for already running process detected upon launch.
    static void AddWatcherForProcessTermination(int processId)
    {
        // WMI query to detect process termination by Process ID
        string processEndQuery = $"SELECT * FROM __InstanceDeletionEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Handle = '{processId}'";
        using (ManagementEventWatcher endWatcher = new(new WqlEventQuery(processEndQuery)))
        {
            try
            {
                // Subscribe to the process termination event
                endWatcher.EventArrived += new EventArrivedEventHandler(ProcessTerminated);
                endWatcher.Start();
                Console.WriteLine($"Monitoring termination of process with ID {processId}...");
                trackedProcessIds.Add(processId);
                StartTrackIR(trackIRPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AddWatcherForProcessTermination Error: {ex.Message}");
            }
        }
    }

    // Monitor the process for when an instance is not detected upon launch.
    static void ProcessStarted(object sender, EventArrivedEventArgs e)
    {
        using (ManagementBaseObject process = e.NewEvent["TargetInstance"] as ManagementBaseObject)
        {
            mainProcessId = Convert.ToInt32(process["ProcessId"]);

            // Monitoring if its the first instance detected, then launching TrackIR5... 
            if (!isFirstInstanceDetected)
            {
                Console.WriteLine($"First process detected with ID: {mainProcessId} \nNot logging subsequent processes...");
                // Mark that the first instance has been detected
                isFirstInstanceDetected = true;
                StartTrackIR(trackIRPath);
            }

            // Add the main process to the tracked process list 
            trackedProcessIds.Add(mainProcessId);

            // Start watching for process termination events
            MonitorProcessTermination(mainProcessId);
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
                Console.WriteLine($"MonitorProcessTermination Error: {ex.Message}");
            }
        };
    }

    // Called when a process in the system is terminated
    static void ProcessTerminated(object sender, EventArrivedEventArgs e)
    {
        using (ManagementBaseObject process = e.NewEvent["TargetInstance"] as ManagementBaseObject)
        {
            int processId = Convert.ToInt32(process["ProcessId"]);

            // Check if the terminated process is part of the tracked process tree
            if (trackedProcessIds.Contains(processId))
            {
                Console.WriteLine($"Process {processId} has been terminated.");

                // Remove the process from the tracked list and terminate TrackIR5 software
                trackedProcessIds.Remove(processId);

                if (trackedProcessIds.Count == 0)
                {
                    Console.WriteLine("All processes in the tree have been terminated.");
                    TerminateTrackIR();
                    // Resetting variables
                    isFirstInstanceDetected = false;
                    mainProcessId = -1;
                    trackedProcessIds = [];
                    Main();
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
                Process.Start(programPath); // This assumes the app is installed on the C drive, default path.
                Console.WriteLine($"{trackIRProcess} started...");
            }
            else
                Console.WriteLine($"An instance of {trackIRProcess} is already running.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start task: {ex}");
        }
    }

    // This method will be called when all processes in the tree are terminated
    static void TerminateTrackIR()
    {
        try
        {
            Console.WriteLine("Searching for TrackIR5 process...");
            Process[] processes = Process.GetProcessesByName(trackIRProcess);

            if (processes.Length == 0)
            {
                Console.WriteLine("No TrackIR5 processes found.");
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
                    Console.WriteLine($"Failed to kill process {process.Id} due to insufficient privileges: {win32Ex.Message}");
                }
                catch (InvalidOperationException invalidOpEx)
                {
                    Console.WriteLine($"Process {process.Id} has already exited: {invalidOpEx.Message}");
                }
                Console.WriteLine($"Process {process.Id} has been terminated.");
            }

            // Check if all tracked processes are terminated outside the loop
            if (trackedProcessIds.Count == 0)
            {
                Console.WriteLine("All processes in the tree have been terminated.");
            }
        }

        catch (Exception ex)
        {
            Console.WriteLine($"Failed to kill task: {ex.Message}");
        }
    }
}