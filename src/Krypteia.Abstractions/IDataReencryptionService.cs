namespace Krypteia.Abstractions;

/// <summary>
/// Optionally re-encrypts user data when a key is reset, so the user retains
/// access to their existing records under the new key pair.
/// </summary>
/// <remarks>
/// <para>
/// <b>Krypteia does not implement this interface itself.</b> The library has no
/// way to know what tables, files, or other storage hold the user's encrypted
/// data — that's application-specific. To preserve user access through a
/// reset, the consuming application must implement this interface and register
/// it via <c>AddKrypteiaKeyReset(...)</c>.
/// </para>
/// <para>
/// <b>If no implementation is registered</b>, the reset flow still works, but
/// any data the user had encrypted under the old key is no longer recoverable
/// through Krypteia. Consumers who can tolerate that (e.g., the encrypted
/// data is recoverable from another source) may skip this implementation.
/// </para>
/// <para>
/// <b>Threat model note.</b> By implementing this interface, the consumer
/// accepts that during the reset window the application code can decrypt
/// the user's data. The library limits the exposure window and audits every
/// access, but the underlying capability exists and must be documented.
/// </para>
/// </remarks>
public interface IDataReencryptionService
{
    /// <summary>
    /// Re-encrypts every piece of data belonging to <paramref name="userId"/>
    /// from the old key pair to the new one.
    /// </summary>
    /// <param name="userId">The user whose data should be re-encrypted.</param>
    /// <param name="oldPrivateKeyPem">The user's old private key in PEM format. Used to decrypt existing ciphertext. Implementations must zero or otherwise scrub this value after use.</param>
    /// <param name="newPublicKeyPem">The user's new public key in PEM format. Used to re-encrypt the plaintext.</param>
    /// <param name="cancellationToken">A token to cancel the operation. Honor it — re-encrypting a large dataset may take a while.</param>
    /// <returns>
    /// A summary of how many records were re-encrypted. Used by the reset
    /// service for audit logging.
    /// </returns>
    Task<DataReencryptionResult> ReencryptUserDataAsync(
        string userId,
        string oldPrivateKeyPem,
        string newPublicKeyPem,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an <see cref="IDataReencryptionService.ReencryptUserDataAsync"/> call.
/// </summary>
/// <param name="RecordsProcessed">How many records were successfully re-encrypted.</param>
/// <param name="RecordsFailed">How many records could not be re-encrypted (corrupt ciphertext, etc.). These records remain in their original encrypted state.</param>
public sealed record DataReencryptionResult(
    int RecordsProcessed,
    int RecordsFailed);