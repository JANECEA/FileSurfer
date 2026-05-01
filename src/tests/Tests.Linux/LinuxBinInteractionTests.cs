using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Linux.Services.Shell;
using Mocks;
using Mocks.Services;

namespace Tests.Linux;

public sealed class TestShellCommandHandler : MockShellHandler, IShellCommandHandler
{
    public ValueResult<string> TrashListResult { get; init; } = string.Empty.OkResult();
    public ValueResult<string> TrashPutResult { get; init; } = string.Empty.OkResult();
    public ValueResult<string> TrashRestoreResult { get; init; } = string.Empty.OkResult();

    public string? LastProgramName { get; private set; }
    public string[] LastProgramArgs { get; private set; } = [];

    public string? LastShellCommand { get; private set; }
    public string[] LastShellCommandArgs { get; private set; } = [];

    public override ValueResult<string> ExecuteCommand(string programName, params string[] args)
    {
        RecordCall(nameof(ExecuteCommand), programName, args);
        LastProgramName = programName;
        LastProgramArgs = args;
        return programName switch
        {
            "trash-list" => TrashListResult,
            "trash-put" => TrashPutResult,
            _ => ValueResult<string>.Error($"Unexpected command: {programName}"),
        };
    }

    public ValueResult<string> ExecuteShellCommand(string shellCommand, params string[] args)
    {
        LastShellCommand = shellCommand;
        LastShellCommandArgs = args;
        return TrashRestoreResult;
    }
}

public class LinuxBinInteractionTests
{
    public static TheoryData<bool, string> EntryPaths =>
        new() { { false, "/tmp/a.txt" }, { true, "/tmp/a-dir" } };

    [Theory]
    [MemberData(nameof(EntryPaths))]
    public void MoveEntryToTrash_UsesTrashPut(bool isDir, string path)
    {
        TestShellCommandHandler shell = new() { TrashPutResult = ValueResult<string>.Ok("done") };
        LinuxBinInteraction interaction = new(shell);

        IResult result = Move(interaction, isDir, path);

        Assert.True(result.IsOk);
        Assert.Equal("trash-put", shell.LastProgramName);
        Assert.Equal([path], shell.LastProgramArgs);
    }

    [Theory]
    [MemberData(nameof(EntryPaths))]
    public void RestoreEntry_WhenTrashListFails_ReturnsError(bool isDir, string path)
    {
        TestShellCommandHandler shell = new()
        {
            TrashListResult = ValueResult<string>.Error("trash-list failed"),
        };
        LinuxBinInteraction interaction = new(shell);

        IResult result = Restore(interaction, isDir, path);

        Assert.False(result.IsOk);
        Assert.Equal("Failed to access system trash.", result.Errors.First());
    }

    [Theory]
    [MemberData(nameof(EntryPaths))]
    public void RestoreEntry_WhenTrashListEmpty_ReturnsError(bool isDir, string path)
    {
        TestShellCommandHandler shell = new()
        {
            TrashListResult = ValueResult<string>.Ok(string.Empty),
        };
        LinuxBinInteraction interaction = new(shell);

        IResult result = Restore(interaction, isDir, path);

        Assert.False(result.IsOk);
        Assert.Equal("Trash list is empty.", result.Errors.First());
    }

    [Theory]
    [MemberData(nameof(EntryPaths))]
    public void RestoreEntry_WhenPathNotFound_ReturnsError(bool isDir, string path)
    {
        TestShellCommandHandler shell = new()
        {
            TrashListResult = ValueResult<string>.Ok("2026-05-01 10:00:00 /tmp/other.txt"),
        };
        LinuxBinInteraction interaction = new(shell);

        IResult result = Restore(interaction, isDir, path);

        Assert.False(result.IsOk);
        Assert.Equal($"Could not find \"{path}\" in trash.", result.Errors.First());
    }

    [Theory]
    [MemberData(nameof(EntryPaths))]
    public void RestoreEntry_WhenPathExistsMultipleTimes_RestoresNewestMatch(
        bool isDir,
        string path
    )
    {
        TestShellCommandHandler shell = new()
        {
            TrashListResult = ValueResult<string>.Ok(
                $"""
                2026-05-01 10:00:00 /tmp/other.txt
                2026-05-01 11:00:00 {path}
                2026-05-01 12:00:00 {path}
                """
            ),
            TrashRestoreResult = ValueResult<string>.Ok("restored"),
        };
        LinuxBinInteraction interaction = new(shell);

        IResult result = Restore(interaction, isDir, path);

        Assert.True(result.IsOk);
        Assert.Equal("echo 1 | trash-restore \"$1\"", shell.LastShellCommand);
        Assert.Equal([path], shell.LastShellCommandArgs);
    }

    private static IResult Move(LinuxBinInteraction interaction, bool isDir, string path) =>
        isDir ? interaction.MoveDirToTrash(path) : interaction.MoveFileToTrash(path);

    private static IResult Restore(LinuxBinInteraction interaction, bool isDir, string path) =>
        isDir ? interaction.RestoreDir(path) : interaction.RestoreFile(path);
}
