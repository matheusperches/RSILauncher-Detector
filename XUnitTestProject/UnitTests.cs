using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Moq;
using RSILauncherDetector.Components;
using static RSILauncherDetector.Interfaces.RSILauncherDetector;


namespace XUnitTestProject
{
    [SupportedOSPlatform("windows")]
    public class RSILauncherDetectorTests
    {
        // Test event on process startup detected
        [Fact]
        public void StartTrackIR_ShouldStartProcess_WhenNoProcessExists()
        {
            // Arrange
            var mockProcessWrapper = new Mock<IProcessWrapper>();
            mockProcessWrapper.Setup(p => p.GetProcessesByName(It.IsAny<string>())).Returns([]);

            var mockLogger = new Mock<IDebugLogger>();
            var controller = new TrackIRController(mockProcessWrapper.Object);

            // Act
            controller.StartTrackIR("TrackIR5", "path/to/executable");

            // Assert
            mockProcessWrapper.Verify(p => p.StartProcess(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        // Test event on processTermination
        [Fact]
        public void TerminateTrackIR_ShouldThrowException_WhenProcessDoesNotExist()
        {
            // Arrange
            var mockProcessWrapper = new Mock<IProcessWrapper>();
            mockProcessWrapper.Setup(p => p.GetProcessesByName(It.IsAny<string>())).Returns([]); // Simulate no processes found

            var controller = new TrackIRController(mockProcessWrapper.Object);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => controller.TerminateTrackIR("TrackIR5"));
            Assert.Equal($"No process found with the name TrackIR5.", exception.Message);
        }
        [Fact]
        public void StartTrackIR_ShouldStartProcess_WhenNoInstanceIsRunning()
        {
            // Arrange
            var mockProcessWrapper = new Mock<IProcessWrapper>();
            var controller = new TrackIRController(mockProcessWrapper.Object);
            string trackIRProcess = "TrackIR";
            string path = "C:\\Dummy\\TrackIR.exe";

            mockProcessWrapper.Setup(m => m.GetProcessesByName(trackIRProcess)).Returns([]); // No process running
            mockProcessWrapper.Setup(m => m.StartProcess(path, It.IsAny<string>())).Returns(new Process());

            // Act
            controller.StartTrackIR(trackIRProcess, path);

            // Assert
            mockProcessWrapper.Verify(m => m.StartProcess(path, It.IsAny<string>()), Times.Once);
        }
        [Fact]
        public void StartTrackIR_ShouldNotStartProcess_WhenInstanceIsAlreadyRunning()
        {
            // Arrange
            var mockProcessWrapper = new Mock<IProcessWrapper>();
            var controller = new TrackIRController(mockProcessWrapper.Object);
            string trackIRProcess = "TrackIR";
            string path = "C:\\Dummy\\TrackIR.exe";

            var mockProcess = new Mock<Process>();
            mockProcessWrapper.Setup(m => m.GetProcessesByName(trackIRProcess)).Returns([mockProcess.Object]); // One process running

            // Act
            controller.StartTrackIR(trackIRProcess, path);

            // Assert
            mockProcessWrapper.Verify(m => m.StartProcess(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}