using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace FileSurfer.Core.Models.Sftp;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class FingerPrint(string algorithm, string hash)
{
    public string Algorithm { get; } = algorithm;
    public string Hash { get; set; } = hash;

    public bool IsSame(FingerPrint? other) =>
        other is not null
        && string.Equals(Algorithm, other.Algorithm, StringComparison.Ordinal)
        && string.Equals(Hash, other.Hash, StringComparison.OrdinalIgnoreCase);
}

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
    public string KeyPath { get; set; } = string.Empty;
    public bool NeedsPassphrase { get; set; } = false;
    public string? InitialDirectory { get; set; } = null;
    public List<FingerPrint> FingerPrints { get; set; } = [];
}
