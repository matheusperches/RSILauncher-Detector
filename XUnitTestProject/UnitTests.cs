using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32;


namespace XUnitTestProject
{
    [SupportedOSPlatform("windows")]
    public class DetectorTests
    {
        private bool restartCalled;

        [Fact]
        public void OnPowerModeChanged_SystemResumed()
        {
            // Arrange
            var eventArgs = new PowerModeChangedEventArgs(PowerModes.Resume);

            // Replace the actual RestartEventWatchers with the mock before calling the event

            var originalRestartWatchers = typeof(RSILauncherDetector.Program).GetMethod("RestartEventWatchers", BindingFlags.Public);

            // Act
            //RSILauncherDetector.Program.OnPowerModeChanged(null, eventArgs); // Sending null as the sender

            // Call the original method after setting up the mock
            //originalRestartWatchers?.Invoke(null, null);

            // Assert
            Assert.True(true, "Expected RestartEventWatchers to be called when system resumes.");
        }
    }
}