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
            Assert.Equal("No process found with the name TrackIR5.", exception.Message);
        }
    }
}