using Microsoft.Win32.TaskScheduler;

namespace RSILauncherDetectorSetup
{
    public class TaskSchedulerSetup
    {
        public static void CreateTask()
        {
            string taskName = "RSILauncherDetector";
            string? exePath = Environment.ProcessPath;
            try
            {
                using TaskService taskService = new();
                if (taskService.FindTask(taskName) != null)
                {
                    DebugLogger.Log($"Task '{taskName}' already exists. Skipping...");
                    return;
                }
                TaskDefinition taskDef = taskService.NewTask();

                // General properties
                taskDef.RegistrationInfo.Description = "Launch RSILauncherDetector at startup with admin privileges";
                taskDef.Principal.LogonType = TaskLogonType.InteractiveToken;
                taskDef.Principal.RunLevel = TaskRunLevel.Highest; // Run as admin
                taskDef.Settings.StopIfGoingOnBatteries = false;
                taskDef.Settings.Compatibility = TaskCompatibility.V2_3;

                // Disable idle conditions
                taskDef.Settings.IdleSettings.StopOnIdleEnd = false;
                taskDef.Settings.IdleSettings.RestartOnIdle = false;
                taskDef.Settings.RunOnlyIfIdle = false;  // Disable running only if idle

                // Trigger at logon
                LogonTrigger logonTrigger = new()
                {
                    Enabled = true
                };
                taskDef.Triggers.Add(logonTrigger);

                // Action to start the application
                if (exePath != null)
                    taskDef.Actions.Add(new ExecAction(exePath, null, null));



                // Register the task
                taskService.RootFolder.RegisterTaskDefinition(taskName, taskDef);

                DebugLogger.Log($"Task '{taskName}' created successfully.");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Failed to create task: {ex.Message}");
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
