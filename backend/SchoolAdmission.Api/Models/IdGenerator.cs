using System.Security.Cryptography;

namespace SchoolAdmission.Api.Models;

/// <summary>
/// Generates prefixed short hexadecimal identifiers for entities.
/// </summary>
public static class IdGenerator
{
    /// <summary>
    /// Creates an identifier formatted as "{prefix}_" followed by 8 lowercase hexadecimal characters.
    /// </summary>
    public static string NewPrefixedId(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return $"{prefix}_{RandomNumberGenerator.GetHexString(8, lowercase: true)}";
    }
}

