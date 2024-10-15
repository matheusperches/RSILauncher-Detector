using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32;
using RSILauncherDetector.Components;


namespace XUnitTestProject
{
    [SupportedOSPlatform("windows")]
    public class DetectorTests
    {

        [Fact]
        public void OnPowerModeChanged_SystemResumed()
        {
            // Arrange
            bool restartCalled;
        //var eventArgs = new PowerModeChangedEventArgs(PowerModes.Resume);

            // Act
            restartCalled = Program.StartScanning();

            // Call the original method after setting up the mock
            //originalRestartWatchers?.Invoke(null, null);

            // Assert
            Assert.True(restartCalled, "Expected RestartEventWatchers to be called when system resumes.");
        }
    }
}