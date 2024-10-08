using System;
using System.CodeDom;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;

class Program
{
    // A dictionary to track all processes in the tree
    static HashSet<int> trackedProcessIds = [];

    // Flag to track whether the first instance has been detected
    static bool isFirstInstanceDetected = false;

    // The process ID of the first detected process (main process)
    static int mainProcessId = -1;

    // The processes we want to track and start / stop 
    static string Game = "RSI Launcher.exe";
    static string TrackIR = "TrackIR5.exe";
    static string TrackIRPath = "C:\\Program Files (x86)\\TrackIR5\\TrackIR5.exe";

    static void Main(string[] args)
    {

        // Query for when the main process starts
        string processStartQuery = $"SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Name = '{Game}'";
        ManagementEventWatcher startWatcher = new ManagementEventWatcher(new WqlEventQuery(processStartQuery));

        // Subscribe to the process start event
        startWatcher.EventArrived += new EventArrivedEventHandler(ProcessStarted);
        startWatcher.Start();

        Console.WriteLine($"Listening for {Game} process events. Press Enter to exit...");
        Console.ReadLine();

        startWatcher.Stop();
    }
    static void ProcessStarted(object sender, EventArrivedEventArgs e)
    {
        ManagementBaseObject process = e.NewEvent["TargetInstance"] as ManagementBaseObject;
        mainProcessId = Convert.ToInt32(process["ProcessId"]);

        // Monitoring if its the first instance detected, then launching TrackIR5... 
        if (!isFirstInstanceDetected)
        {
            Console.WriteLine($"First process detected with ID: {mainProcessId} \n Not logging subsequent processes...");
            // Mark that the first instance has been detected
            isFirstInstanceDetected = true;
            StartTrackIR(TrackIRPath);
        }

        // Add the main process to the tracked process list 
        trackedProcessIds.Add(mainProcessId);

        // Start watching for process termination events
        MonitorProcessTermination();
    }

    // Monitor process termination events for all tracked processes
    static void MonitorProcessTermination()
    {
        string processEndQuery = "SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process'";
        ManagementEventWatcher endWatcher = new ManagementEventWatcher(new WqlEventQuery(processEndQuery));

        endWatcher.EventArrived += new EventArrivedEventHandler(ProcessTerminated);
        endWatcher.Start();
    }

    // Called when a process in the system is terminated
    static void ProcessTerminated(object sender, EventArrivedEventArgs e)
    {
        ManagementBaseObject process = e.NewEvent["TargetInstance"] as ManagementBaseObject;
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
            }
        }
    }

    // This method will be called after the first instance of the tracked process is detected
    static void StartTrackIR(string programPath)
    {
        try
        {
            Process.Start(programPath); // This assumes the installation will be done in the C drive on the default path.
            Console.WriteLine("TrackIR started...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start task: {ex}");
        }
    }

    // This method will be called when all processes in the tree are terminated
    static void TerminateTrackIR()
    {
        Console.WriteLine("Terminating TrackIR5 software...");

        try
        {
            Process[] processes = Process.GetProcessesByName(TrackIR);
            foreach (Process process in processes)
            {

                Console.WriteLine($"Process {process.Id} has been terminated.");

                if (trackedProcessIds.Count == 0)
                {
                    process.Kill();
                    Console.WriteLine($"Process {process.ProcessName} (ID: {process.Id}) has been killed.");
                    Console.WriteLine("All processes in the tree have been terminated.");
                    TerminateTrackIR();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to kill task: {ex}");
        }
    }
}