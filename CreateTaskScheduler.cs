using Microsoft.Win32.TaskScheduler;

namespace RSILauncherSetup
{
    public class TaskSchedulerSetup
    {
        public static void CreateTask()
        {
            string taskName = "RSILauncherDetector";
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            try
            {
                using (TaskService taskService = new TaskService())
                {
                    if (taskService.FindTask(taskName) != null)
                    {
                        Console.WriteLine($"Task '{taskName}' already exists. Skipping...");
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
                    LogonTrigger logonTrigger = new LogonTrigger
                    {
                        Enabled = true
                    };
                    taskDef.Triggers.Add(logonTrigger);

                    // Action to start the application
                    taskDef.Actions.Add(new ExecAction(exePath, null, null));


                    // Register the task
                    taskService.RootFolder.RegisterTaskDefinition(taskName, taskDef);

                    Console.WriteLine($"Task '{taskName}' created successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create task: {ex.Message}");
            }
        }
    }
}
