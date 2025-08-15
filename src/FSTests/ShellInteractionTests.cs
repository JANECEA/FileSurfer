using FileSurfer.Models.FileOperations;

namespace FSTests;

public class ShellInteractionTests
{
    private readonly WindowsFileIOHandler _fileIOHandler = new(100);

    [Fact]
    public void ExecuteCmd_Should_Not_Hang()
    {
        // Arrange
        string command = "for /L %i in (1,1,1000) do @echo Line %i"; // Generates 1000 lines of output

        // Act
        bool result = _fileIOHandler.ExecuteCmd(command, out string? errorMessage);

        // Assert
        Assert.True(result, "The command should complete successfully in cca 80ms.");
        Assert.Null(errorMessage);
    }

    [Fact]
    public void ExecuteCmd_Should_Return_False()
    {
        // Arrange
        string command = "ping --invalidoption";

        // Act
        bool result = _fileIOHandler.ExecuteCmd(command, out string? errorMessage);

        // Assert
        Assert.False(result, "The command should fail.");
        Assert.NotNull(errorMessage);
    }

    [Fact]
    public void ExecuteCmd_Should_Return_True()
    {
        // Arrange
        string command = "echo \"Hello world!\"";

        // Act
        bool result = _fileIOHandler.ExecuteCmd(command, out string? errorMessage);

        // Assert
        Assert.True(result, "The command should succeed.");
        Assert.Null(errorMessage);
    }
}
