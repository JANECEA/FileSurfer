using FileSurfer.Core.Models;
using FileSurfer.Windows.Models.Shell;

namespace FSTests;

public class ShellInteractionTests
{
    private readonly WindowsShellHandler _shellHandler = new();

    [Fact]
    public void ExecuteCmd_Should_Not_Hang()
    {
        // Arrange
        string command = "for /L %i in (1,1,1000) do @echo Line %i"; // Generates 1000 lines of output

        // Act
        IResult result = _shellHandler.ExecuteCmd(command);

        // Assert
        Assert.True(result.IsOk, "The command should complete successfully in cca 80ms.");
    }

    [Fact]
    public void ExecuteCmd_Should_Return_False()
    {
        // Arrange
        string command = "ping --invalidoption";

        // Act
        IResult result = _shellHandler.ExecuteCmd(command);

        // Assert
        Assert.False(result.IsOk, "The command should fail.");
    }

    [Fact]
    public void ExecuteCmd_Should_Return_True()
    {
        // Arrange
        string command = "echo \"Hello world!\"";

        // Act
        IResult result = _shellHandler.ExecuteCmd(command);

        // Assert
        Assert.True(result.IsOk, "The command should succeed.");
    }
}
