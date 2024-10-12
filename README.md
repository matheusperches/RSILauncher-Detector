# TrackIr-Detector
 - A background app that automatically launches TrackIR5 software when the RSI Launcher is opened, and closed when RSI Launcher is closed.
 - WMI Event based. This means little to none CPU usage.
 - Creates a task scheduler for you that automatically launches this app when you boot Windows.


   ## Installation Guide
   - Download the [Release](https://github.com/matheusperches/RSILauncher-Detector/tree/main/Release) folder.
   - Put the file somewhere you won't change. I recommend the documents folder.
   - Run the app and allow the UAC prompt. It will only prompt once for the task creation.
   - You can see it running in the background in the Task Manager -> Search for RSILauncherDetector.
   - Note: This program assumes the default installation path for TrackIR5. If there is a demand for it, I can implement a differnet functionality for finding your installation path.
  
   ## Uninstall
   1. Kill the task in Task Manager (RSILauncherDetector)
   2. Open Task Scheduler -> Scheduler Library (Main folder from the left pane) and look for RSILauncherDetector on the list.
   3. Right click -> Remove (underneath properties)
   4. (Optional) Restart your computer.
  

 ## Code snippets

- Starting point
```C# 
public static void Main()
{
    // Tries to create a new task, if none exists
    TaskSchedulerSetup.CreateTask();

    StartScanning();

    resetEvent.WaitOne(); // Block the main thread here to prevent the app from shutting down
}         
```

- Creating the task 
```C#
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
        taskDef.RegistrationInfo.Description = "Launch RSILauncherDetector at startup with admin privileges";
        taskDef.Principal.LogonType = TaskLogonType.InteractiveToken;
        taskDef.Principal.RunLevel = TaskRunLevel.LUA; // Run as default
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
        {
            taskDef.Actions.Add(new ExecAction(exePath, null, null));

            // Register the task
            taskService.RootFolder.RegisterTaskDefinition(taskName, taskDef);
            DebugLogger.Log($"Task '{taskName}' created successfully.");
            RestartWithRegularPrivileges();
        }
    }
    catch (Exception ex)
    {
        DebugLogger.Log($"Failed to create task: {ex.Message}");
    }
}
```

- Scanning for processes
```C#
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

```
- Handling the TrackIR5 process
```C#
// This method will be called after the first instance of the tracked process is detected
private static void StartTrackIR(string programPath)
{
    try
    {
        Process[] existingProcess = Process.GetProcessesByName(trackIRProcess);
        // Checking if there is already a process running
        if (existingProcess.Length == 0)
        {
            using (Process trackirProc = new())
            {
                trackirProc.StartInfo.FileName = programPath;
                trackirProc.StartInfo.WorkingDirectory = Path.GetDirectoryName(programPath);
                trackirProc.StartInfo.UseShellExecute = false;
                trackirProc.Start();
                DebugLogger.Log($"{trackIRProcess} started...");
            };
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
```

- Triggered when the RSI Launcher is detected
```C#
// Monitor the process for when an instance is not detected upon launch.
private static void ProcessStarted(object sender, EventArrivedEventArgs e)
{
    using (ManagementBaseObject? process = e.NewEvent["TargetInstance"] as ManagementBaseObject)
    {
        if (process != null)
        {
            firstProcessID = Convert.ToInt32(process["ProcessId"]);

            // Monitoring if its the first instance detected, then launching TrackIR5... 
            if (!isFirstInstanceDetected)
            {
                isFirstInstanceDetected = true; // Mark that the first instance has been detected
                DebugLogger.Log($"First process detected with ID: {firstProcessID} \nNot logging subsequent processes..."); 
                StartTrackIR(trackIRPath);
            }

            // Add the main process to the tracked process list 
            trackedProcessIds.Add(firstProcessID);
            // Start watching for process termination events
            MonitorProcessTermination(firstProcessID);
        }
    };
}
```
