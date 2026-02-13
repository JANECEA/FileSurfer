using System;
using System.Linq;
using FileSurfer.Core;
using FileSurfer.Core.Models.Shell;

namespace FileSurfer.Linux;

public class LinuxDefaultSettingsProvider : IDefaultSettingsProvider
{
    private static readonly (string Executable, string TerminalArgs)[] CommonTerminals =
    {
        ("wezterm", "start --cwd"),
        ("kitty", "--directory"),
        ("konsole", "--workdir"),
        ("gnome-terminal", "--working-directory"),
        ("tilix", "--working-directory"),
        ("xfce4-terminal", "--working-directory"),
        ("lxterminal", "--working-directory"),
        ("alacritty", "--working-directory"),
    };

    private static readonly string[] CommonGuiTextEditors =
    {
        "gedit",
        "pluma",
        "mousepad",
        "kwrite",
        "kate",
        "codium",
        "vscodium",
        "code",
        "atom",
        "subl",
        "notepadqq",
        "geany",
        "micro",
    };

    private readonly IShellHandler _shellHandler;

    private (string Executable, string TerminalArgs)? _terminal;
    private string? _textEditor;

    public LinuxDefaultSettingsProvider(IShellHandler shellHandler) => _shellHandler = shellHandler;

    private (string Executable, string TerminalArgs) GetTerminal()
    {
        if (_terminal is not null)
            return _terminal.Value;

        foreach ((string executable, string argsForDir) in CommonTerminals)
            if (_shellHandler.ExecuteCommand("which", executable).IsOk)
            {
                _terminal = (executable, argsForDir);
                return _terminal.Value;
            }

        _terminal = (string.Empty, string.Empty);
        return _terminal.Value;
    }

    private string GetTextEditor(string? editorFromVar)
    {
        if (_textEditor is not null)
            return _textEditor;

        if (
            editorFromVar is not null
            && CommonGuiTextEditors.Contains(editorFromVar, StringComparer.OrdinalIgnoreCase)
        )
            return _textEditor = editorFromVar;

        foreach (string textEditor in CommonGuiTextEditors)
            if (_shellHandler.ExecuteCommand("which", textEditor).IsOk)
                return _textEditor = textEditor;

        return _textEditor = string.Empty;
    }

    private static string? Variable(string varName)
    {
        string? result = Environment.GetEnvironmentVariable(varName);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    public void PopulateDefaults(SettingsRecord settingsRecord)
    {
        (string terminal, string terminalArgs) = GetTerminal();
        string textEditor = GetTextEditor(Variable("EDITOR"));

        settingsRecord.newDirectoryName = "New Directory";
        settingsRecord.notepadApp = textEditor;
        settingsRecord.notepadAppArgs = string.Empty;
        settingsRecord.terminal = terminal;
        settingsRecord.terminalArgs = terminalArgs;
    }
}
