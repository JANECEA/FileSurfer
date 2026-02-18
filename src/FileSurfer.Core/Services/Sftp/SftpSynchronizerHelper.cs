using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;

namespace FileSurfer.Core.Services.Sftp;

public static class SftpSynchronizerHelper
{
    public static async Task<ValueResult<string>> GetLocalPath(
        Location remoteLocation,
        IEnumerable<Location> pastLocations,
        IDialogService dialogService
    )
    {
        if (!remoteLocation.Exists())
            return ValueResult<string>.Error(
                $"Remote directory \"{remoteLocation.Path}\" does not exist."
            );

        const string title = "Pick local path";
        string context = $"""
            Select local path for 
            remote path: "{remoteLocation.FileSystem.GetLabel()}:{remoteLocation.Path}".
            """;
        const string suggestLabel = "Recent local paths:";
        IEnumerable<string> suggestions = pastLocations
            .Where(l => l.FileSystem is LocalFileSystem)
            .Select(l => l.Path)
            .Distinct();

        string? path = await dialogService.SuggestInputDialog(
            title,
            context,
            suggestLabel,
            suggestions
        );
        return path is not null ? path.OkResult() : ValueResult<string>.Error();
    }
}
