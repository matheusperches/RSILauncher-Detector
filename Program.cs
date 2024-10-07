using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

class Program 
{
    static void Main(string[] args)
    {  
        string Game = "RSI Launcher"; // The game 
        string TrackIR = "TrackIR5"; // HeadTrackingSoftware

        // Use a loop to continually monitor the processes (there is probably a way to optimize this)
        while (true)
        {
            bool isGameRunning = IsAppRunning(Game);
            bool isTrackIRRunning = IsAppRunning(TrackIR);

            if (isGameRunning)
            {
                Console.WriteLine($"{Game} is running.");
                
                if (!isTrackIRRunning)
                {
                    Console.WriteLine($"{TrackIR} has not been detected.");
                    try
                    {
                        Process.Start("C:\\Program Files (x86)\\TrackIR5\\TrackIR5.exe");
                        Console.Write($"{TrackIR} software launched sucessfully.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                        return;
                    }
                }
                else 
                {
                    Console.WriteLine($"{TrackIR} has been detected.");
                }
            }
            else
                Console.WriteLine($"{Game} is not running.");
            // Sleep for 5 seconds to avoid excessive CPU usage.
            Thread.Sleep(5000);
        }
    }
    // Checking if the program is running
    static bool IsAppRunning(string processName)
    {
        Process[] runningProcesses = Process.GetProcessesByName(processName);
        return runningProcesses.Length > 0;
    }
}