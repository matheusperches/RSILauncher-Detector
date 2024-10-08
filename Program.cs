using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;

class Program
{
    static void Main(string[] args)
    {
        string Game = "RSI Launcher.exe"; // The game 
        string TrackIR = "TrackIR5.exe"; // HeadTrackingSoftware

        // Event listener for when a process starts 
        ManagementEventWatcher startWatcher = CreateEventWatcher(Game);
        startWatcher.EventArrived += new EventArrivedEventHandler(ProcessStarted);

        // Event listener for when a process ends
        ManagementEventWatcher endWatcher = CreateEventWatcher(Game);
        endWatcher.EventArrived += new EventArrivedEventHandler(ProcessEnded); // This calls ProcessEnded when a process ends

        // Keep the application running to listen to events
        Console.WriteLine("Listening for process events... press any key to exit.");
        Console.ReadLine();

        // Stop the watchers when the program exit
        startWatcher.Stop();
        endWatcher.Stop();
    }
    static ManagementEventWatcher CreateEventWatcher(String processName)
    {
        // Event listener for when a process starts 
        string processQuery = $"SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Name = '{processName}'";
        ManagementEventWatcher newWatcher = new (new WqlEventQuery(processQuery));
        newWatcher.Start();
        return newWatcher;
    }
    static void ProcessStarted(object sender, EventArrivedEventArgs e)
    {
       ManagementBaseObject process = e.NewEvent["TargetInstance"] as ManagementBaseObject;
       string processName = process["Name"].ToString();
       string processId = process["ProcessId"].ToString();

       Console.WriteLine($"Process Started: {processName} (ID: {processId})");

        try
        {
            //Process.Start("C:\\Program Files (x86)\\TrackIR5\\TrackIR5.exe");
            Console.WriteLine($"TrackIR5 software launched sucessfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static void ProcessEnded(object sender, EventArrivedEventArgs e)
    {
        ManagementBaseObject processInfo = e.NewEvent["TargetInstance"] as ManagementBaseObject;
        string processName = processInfo["Name"].ToString();
        string processId = processInfo["ProcessId"].ToString();

        Console.WriteLine($"Process Ended: {processName} (ID: {processId})");
        try
        {
            Process[] processes = Process.GetProcessesByName("TrackIR5.exe");
            foreach (Process process in processes)
            {
                process.Kill();
                Console.WriteLine($"Process {processName} (ID: {process.Id}) has been killed.");
            }
        }
        catch ( Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }
    }

    // Checking if the program is running
    static bool IsAppRunning(string processName)
    {
        Process[] runningProcesses = Process.GetProcessesByName(processName);
        return runningProcesses.Length > 0;
    }
}