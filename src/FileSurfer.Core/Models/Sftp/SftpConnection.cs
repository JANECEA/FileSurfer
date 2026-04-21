using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace FileSurfer.Core.Models.Sftp;

/// <summary>
/// Represents an SSH host fingerprint used for trust verification.
/// </summary>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class FingerPrint(string algorithm, string hash)
{
    /// <summary>
    /// Gets the host key algorithm name.
    /// </summary>
    public string Algorithm { get; } = algorithm;

    /// <summary>
    /// Gets or sets the fingerprint hash for the algorithm.
    /// </summary>
    public string Hash { get; set; } = hash;

    /// <summary>
    /// Determines whether this fingerprint matches another fingerprint by algorithm and hash.
    /// </summary>
    /// <param name="other">The fingerprint to compare with this instance.</param>
    /// <returns><see langword="true"/> when both fingerprints represent the same key; otherwise, <see langword="false"/>.</returns>
    public bool IsSame(FingerPrint? other) =>
        other is not null
        && string.Equals(Algorithm, other.Algorithm, StringComparison.Ordinal)
        && string.Equals(Hash, other.Hash, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Represents user-configurable settings for establishing an SFTP connection.
/// </summary>
[SuppressMessage(
    "ReSharper",
    "AutoPropertyCanBeMadeGetOnly.Global",
    Justification = $"{nameof(JsonSerializer)} requires properties with setters."
)]
public sealed record SftpConnection
{
    /// <summary>
    /// Gets serializer options for persisting and loading SFTP connection settings.
    /// </summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    /// <summary>
    /// Gets or sets the remote host name or IP address.
    /// </summary>
    public string HostnameOrIpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SSH port.
    /// </summary>
    public ushort Port { get; set; } = 22;

    /// <summary>
    /// Gets or sets the username used for authentication.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the private key file path for key-based authentication.
    /// </summary>
    public string KeyPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the private key requires a passphrase.
    /// </summary>
    public bool NeedsPassphrase { get; set; } = false;

    /// <summary>
    /// Gets or sets the initial directory to open after connection.
    /// </summary>
    public string? InitialDirectory { get; set; } = null;

    /// <summary>
    /// Gets or sets the trusted host fingerprints for this connection.
    /// </summary>
    public List<FingerPrint> FingerPrints { get; set; } = [];
}
