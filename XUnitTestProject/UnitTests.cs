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

        // Functionality tests
        [Fact]
        public void StartTrackIR_ShouldStartProcess_WhenNoProcessExists()
        {
            // Arrange
            var mockProcessWrapper = new Mock<IProcessWrapper>();
            mockProcessWrapper.Setup(p => p.GetProcessesByName(It.IsAny<string>())).Returns([]);

            var mockLogger = new Mock<IDebugLogger>();
            var controller = new TrackIRController(mockProcessWrapper.Object);

            // Act
            controller.StartTrackIR("TrackIR5", "path\\to\\executable");

            // Assert
            mockProcessWrapper.Verify(p => p.StartProcess(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }
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

        // Edge case test: Test that the method throws an exception when an invalid path is provided
        [Fact]
        public void StartTrackIR_ShouldThrowException_WhenPathIsInvalid()
        {
            // Arrange
            var mockProcessWrapper = new Mock<IProcessWrapper>();
            var controller = new TrackIRController(mockProcessWrapper.Object);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => controller.StartTrackIR("TrackIR5", "invalidPath"));
        }

        // Edge case test: Test that the method handles null values properly:
        [Fact]
        public void StartTrackIR_ShouldThrowException_WhenProcessNameIsNull()
        {
            // Arrange
            var mockProcessWrapper = new Mock<IProcessWrapper>();
            var controller = new TrackIRController(mockProcessWrapper.Object);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => controller.StartTrackIR(null!, "C:\\path\\to\\trackir.exe"));
        }

        // Testing the flow of start and terminate process
        [Fact]
        public void ProcessHandler_ShouldStartAndTerminateProcess()
        {
            // Arrange
            var mockProcessWrapper = new Mock<IProcessWrapper>();
            var controller = new TrackIRController(mockProcessWrapper.Object);

            // Simulate the process not running initially
            mockProcessWrapper.Setup(p => p.GetProcessesByName(It.IsAny<string>())).Returns([]);

            // Act
            controller.StartTrackIR("TrackIR5", "C:\\path\\to\\TrackIR5.exe");
            controller.TerminateTrackIR("TrackIR5");

            // Assert
            mockProcessWrapper.Verify(p => p.StartProcess(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            mockProcessWrapper.Verify(p => p.GetProcessesByName("TrackIR5"), Times.Once);
        }
    }
}