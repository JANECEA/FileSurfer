using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;
using FileSurfer.Core.ViewModels;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace FileSurfer.Core.Services.Sftp;

public class SftpFileProperties : IFileProperties
{
    private readonly SftpClient _sftpClient;
    private readonly SshShellHandler _shellHandler;

    public SftpFileProperties(SftpClient sftpClient, SshShellHandler shellHandler)
    {
        _sftpClient = sftpClient;
        _shellHandler = shellHandler;
    }

    private static string GetPermissions(ISftpFile entry)
    {
        char[] p = new char[9];

        p[0] = entry.OwnerCanRead ? 'r' : '-';
        p[1] = entry.OwnerCanWrite ? 'w' : '-';
        p[2] = entry.OwnerCanExecute ? 'x' : '-';

        p[3] = entry.GroupCanRead ? 'r' : '-';
        p[4] = entry.GroupCanWrite ? 'w' : '-';
        p[5] = entry.GroupCanExecute ? 'x' : '-';

        p[6] = entry.OthersCanRead ? 'r' : '-';
        p[7] = entry.OthersCanWrite ? 'w' : '-';
        p[8] = entry.OthersCanExecute ? 'x' : '-';

        return new string(p);
    }

    private ValueResult<string> GetOwner(ISftpFile entry)
    {
        int uid = entry.Attributes.UserId;
        int gid = entry.Attributes.GroupId;

        ValueResult<string> userR = _shellHandler.ExecuteSshCommand(
            $"getent passwd {uid} | cut -d: -f1"
        );
        ValueResult<string> groupR = _shellHandler.ExecuteSshCommand(
            $"getent group {gid} | cut -d: -f1"
        );
        if (ResultExtensions.FirstError(userR, groupR) is IResult result)
            return ValueResult<string>.Error(result);

        string user = userR.Value.Trim();
        if (string.IsNullOrEmpty(user))
            user = uid.ToString(CultureInfo.InvariantCulture);

        string group = groupR.Value.Trim();
        if (string.IsNullOrEmpty(group))
            group = gid.ToString(CultureInfo.InvariantCulture);

        return (user == group ? user : $"{user}:{group}").OkResult();
    }

    private string GetSize(FileSystemEntryViewModel entry)
    {
        if (!entry.IsDirectory)
        {
            if (entry.Size is [.., ' ', 'B'])
                return entry.Size;

            string bytesFormatted = (entry.SizeB ?? 0).ToString("N0", CultureInfo.InvariantCulture);
            return $"{entry.Size} ({bytesFormatted} B)";
        }
        try
        {
            List<ISftpFile> filesAndDirs = _sftpClient
                .ListDirectory(entry.PathToEntry)
                .Where(e => e.Name is not ("." or ".."))
                .ToList();

            int fileCount = filesAndDirs.Count(x => x.IsRegularFile || x.IsSymbolicLink);
            int dirCount = filesAndDirs.Count(x => x.IsDirectory);
            return $"{fileCount} files, {dirCount} sub-directories";
        }
        catch
        {
            return "-";
        }
    }

    private DateTime? GetCreationDate(string path)
    {
        string quotedPath = SshShellHandler.Quote(path);
        ValueResult<string> result = _shellHandler.ExecuteSshCommand($"stat -c %w {quotedPath}");
        if (!result.IsOk)
            return null;

        string dateStr = result.Value.Trim();
        if (string.IsNullOrEmpty(dateStr) || dateStr == "-")
            return null;

        return DateTime.TryParse(
            dateStr,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime date
        )
            ? date
            : null;
    }

    public IResult ShowFileProperties(FileSystemEntryViewModel entry)
    {
        try
        {
            ISftpFile remoteEntry = _sftpClient.Get(entry.PathToEntry);
            ValueResult<string> ownerResult = GetOwner(remoteEntry);
            if (!ownerResult.IsOk)
                return ownerResult;

            PropertiesWindowViewModel vm = new(entry, GetPermissions(remoteEntry))
            {
                Size = GetSize(entry),
                DateCreated = GetCreationDate(entry.PathToEntry) ?? remoteEntry.LastWriteTime,
                DateAccessed = remoteEntry.LastAccessTime,
                DateModified = remoteEntry.LastWriteTime,
                Owner = ownerResult.Value,
            };
            vm.Show();
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public bool SupportsOpenAs(IFileSystemEntry entry) => false;

    public IResult ShowOpenAsDialog(IFileSystemEntry entry) =>
        SimpleResult.Error("Unsupported environment.");
}
