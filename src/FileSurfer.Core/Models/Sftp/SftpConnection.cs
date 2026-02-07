using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace FileSurfer.Core.Models.Sftp;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public record FingerPrint(string Algorithm, string Hash);

public record SftpConnection
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    public string HostnameOrIpAddress { get; set; } = string.Empty;
    public ushort Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? InitialDirectory { get; set; } = null;
    public List<FingerPrint> FingerPrints { get; set; } = [];
}
