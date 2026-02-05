using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace FileSurfer.Core.Models.Sftp;

[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Json naming convention")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public record FingerPrint(string algorithm, string hash);

[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Json naming convention")]
public record SftpConnection
{
    public string hostnameOrIpAddress { get; set; } = string.Empty;
    public ushort port { get; set; } = 22;
    public string username { get; set; } = string.Empty;
    public string password { get; set; } = string.Empty;
    public string? initialDirectory { get; set; } = null;

    public List<FingerPrint> fingerPrints { get; set; } = [];
}
