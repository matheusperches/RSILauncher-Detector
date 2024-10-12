using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using static System.Net.Mime.MediaTypeNames;

namespace RSILauncherDetectorSetup
{
    [SupportedOSPlatform("windows")]
    public class TaskSchedulerSetup
    {
        static readonly string taskName = "RSILauncherDetector";
        static readonly string? exePath = Environment.ProcessPath;
        public static void CreateTask()
        {
            try
            {
                using TaskService taskService = new();
                if (taskService.FindTask(taskName) != null)
                {
                    DebugLogger.Log($"Task '{taskName}' already exists. Skipping...");
                    return;
                }

                if (!IsRunningAsAdmin() && exePath != null)
                {
                    RestartWithAdminPrivileges(exePath);
                }

                TaskDefinition taskDef = taskService.NewTask();

                // General properties
                taskDef.RegistrationInfo.Description = "Launch RSILauncherDetector at startup";
                taskDef.Principal.LogonType = TaskLogonType.InteractiveToken;
                taskDef.Principal.RunLevel = TaskRunLevel.LUA; // Run as default
                taskDef.Settings.StopIfGoingOnBatteries = false;
                taskDef.Settings.Compatibility = TaskCompatibility.V2_3;

                // Disable idle conditions
                taskDef.Settings.IdleSettings.StopOnIdleEnd = false;
                taskDef.Settings.IdleSettings.RestartOnIdle = false;
                taskDef.Settings.RunOnlyIfIdle = false;

                // Trigger at logon
                LogonTrigger logonTrigger = new()
                {
                    Enabled = true
                };
                taskDef.Triggers.Add(logonTrigger);

                // Action to start the application
                if (exePath != null)
                {
                    taskDef.Actions.Add(new ExecAction(exePath, null, null));

                    // Register the task
                    taskService.RootFolder.RegisterTaskDefinition(taskName, taskDef);
                    DebugLogger.Log($"Task '{taskName}' created successfully.");
                    TriggerTaskExecution();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Failed to create task: {ex.Message}");
            }
        }
        public static bool IsRunningAsAdmin()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void RestartWithAdminPrivileges(string exeLocation)
        {
            try
            {
                Process process = new();
                process.StartInfo.FileName = exeLocation; // Path to the current executable
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.Verb = "runas"; // "runas" will try to run with elevated privileges, but we'll change that.
                process.Start();

                Environment.Exit(0); // Close the current instance
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Failed to restart the application without admin privileges: {ex.Message}");
            }
        }
        public static void TriggerTaskExecution()
        {
            try
            {
                using TaskService ts = new();
                Microsoft.Win32.TaskScheduler.Task task = ts.FindTask(taskName);
                if (task != null)
                {
                    task.Run(); // Starts the task
                    Console.WriteLine($"Task '{taskName}' started successfully.");
                }

                Environment.Exit(0); // Close the current exe
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Failed to restart the application without admin privileges: {ex.Message}");
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
