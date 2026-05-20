using System.Security.Cryptography;
using Krypteia.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Krypteia.KeyReset;

/// <summary>
/// Orchestrates the email-token key reset flow.
/// </summary>
/// <remarks>
/// <para>
/// <b>Flow:</b>
/// </para>
/// <list type="number">
///   <item><description><b>Initiate.</b> Generate a 32-byte random token, hash it with SHA-256, persist the hash with a TTL, and email the token (in plaintext) to the user.</description></item>
///   <item><description><b>Complete.</b> Receive the token from the link, hash it, look it up. If valid: read the old encrypted backup, unwrap the old private key, generate a new key pair, optionally re-encrypt the user's data under the new public key, persist the new pair, return the new private key.</description></item>
/// </list>
/// <para>
/// <b>Security properties:</b>
/// </para>
/// <list type="bullet">
///   <item><description>Tokens are 32 random bytes (256 bits of entropy) — guessing one is computationally infeasible.</description></item>
///   <item><description>Only the SHA-256 hash is persisted. A database leak does not enable resets.</description></item>
///   <item><description>Tokens are single-use. The first successful completion marks them used; further attempts fail.</description></item>
///   <item><description>Tokens expire (default 15 minutes). Stolen tokens have a small window.</description></item>
///   <item><description>Rate limiting (default 3 attempts per user per hour) limits enumeration and email-bombing abuse.</description></item>
///   <item><description>Initiate always returns the same result regardless of whether the user exists — no user enumeration.</description></item>
///   <item><description>Every outcome (success, expired token, reused token, rate-limited, unknown user, re-encryption failure) is audited via <see cref="IAuditService"/>.</description></item>
/// </list>
/// </remarks>
public sealed partial class KeyResetService : IKeyResetService
{
    private const int TokenSizeBytes = 32;

    private readonly IKeyManagementService _keyManagement;
    private readonly IKeyEnvelope _envelope;
    private readonly IMasterKeyProvider _masterKeys;
    private readonly IResetTokenStore _tokenStore;
    private readonly IEmailSender _emailSender;
    private readonly IAuditService _audit;
    private readonly IDataReencryptionService? _reencryption;
    private readonly KeyResetOptions _options;
    private readonly ILogger<KeyResetService> _logger;

    /// <summary>Initializes a new instance.</summary>
    public KeyResetService(
        IKeyManagementService keyManagement,
        IKeyEnvelope envelope,
        IMasterKeyProvider masterKeys,
        IResetTokenStore tokenStore,
        IEmailSender emailSender,
        IAuditService audit,
        IOptions<KeyResetOptions> options,
        ILogger<KeyResetService> logger,
        IDataReencryptionService? reencryption = null)
    {
        _keyManagement = keyManagement ?? throw new ArgumentNullException(nameof(keyManagement));
        _envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
        _masterKeys = masterKeys ?? throw new ArgumentNullException(nameof(masterKeys));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _reencryption = reencryption; // optional — null is valid
    }

    /// <inheritdoc />
    public async Task<KeyResetInitiationResult> InitiateResetAsync(
        string userId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        int recentAttempts = await _tokenStore.CountRecentAttemptsAsync(
            userId, now - _options.RateLimitWindow, cancellationToken).ConfigureAwait(false);

        if (recentAttempts >= _options.MaxAttemptsPerUser)
        {
            await _audit.RecordAsync(new AuditEntry(
                Timestamp: now,
                Operation: AuditOperation.KeyResetRejected,
                UserId: userId,
                ActorId: null,
                FieldName: null,
                KeyVersion: 0,
                Result: AuditResult.Denied,
                IpAddress: ipAddress,
                ErrorCode: "rate_limited"), cancellationToken).ConfigureAwait(false);

            return KeyResetInitiationResult.RateLimited;
        }

        string? publicKey = await _keyManagement.GetPublicKeyAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        if (publicKey is null)
        {
            await _audit.RecordAsync(new AuditEntry(
                Timestamp: now,
                Operation: AuditOperation.KeyResetInitiated,
                UserId: userId,
                ActorId: null,
                FieldName: null,
                KeyVersion: 0,
                Result: AuditResult.Denied,
                IpAddress: ipAddress,
                ErrorCode: "unknown_user"), cancellationToken).ConfigureAwait(false);

            return KeyResetInitiationResult.Success;
        }

        byte[] tokenBytes = new byte[TokenSizeBytes];
        RandomNumberGenerator.Fill(tokenBytes);
        string tokenValue = Convert.ToBase64String(tokenBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        string tokenHash = HashToken(tokenValue);

        var record = new ResetTokenRecord(
            TokenHash: tokenHash,
            UserId: userId,
            CreatedAt: now,
            ExpiresAt: now + _options.TokenLifetime,
            UsedAt: null);

        await _tokenStore.StoreAsync(record, cancellationToken).ConfigureAwait(false);

        string resetUrl = BuildResetUrl(tokenValue);
        EmailMessage email = BuildResetEmail(userId, resetUrl);

        try
        {
            await _emailSender.SendAsync(email, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _audit.RecordAsync(new AuditEntry(
                Timestamp: DateTimeOffset.UtcNow,
                Operation: AuditOperation.KeyResetInitiated,
                UserId: userId,
                ActorId: null,
                FieldName: null,
                KeyVersion: 0,
                Result: AuditResult.Failure,
                IpAddress: ipAddress,
                ErrorCode: "email_send_failed"), cancellationToken).ConfigureAwait(false);

            Log.EmailSendFailed(_logger, userId, ex);
            return KeyResetInitiationResult.Success;
        }

        await _audit.RecordAsync(new AuditEntry(
            Timestamp: DateTimeOffset.UtcNow,
            Operation: AuditOperation.KeyResetInitiated,
            UserId: userId,
            ActorId: null,
            FieldName: null,
            KeyVersion: 0,
            Result: AuditResult.Success,
            IpAddress: ipAddress), cancellationToken).ConfigureAwait(false);

        return KeyResetInitiationResult.Success;
    }

    /// <inheritdoc />
    public async Task<KeyPair> CompleteResetAsync(
        string token,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string tokenHash = HashToken(token);

        ResetTokenRecord? record = await _tokenStore.FindByHashAsync(tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            await AuditResetRejected(now, userId: "(unknown)", "token_not_found", ipAddress, cancellationToken)
                .ConfigureAwait(false);
            throw new KeyResetException();
        }

        if (record.UsedAt is not null)
        {
            await AuditResetRejected(now, record.UserId, "token_already_used", ipAddress, cancellationToken)
                .ConfigureAwait(false);
            throw new KeyResetException();
        }

        if (record.ExpiresAt <= now)
        {
            await AuditResetRejected(now, record.UserId, "token_expired", ipAddress, cancellationToken)
                .ConfigureAwait(false);
            throw new KeyResetException();
        }

        // Mark the token used FIRST. This is the critical step — if anything
        // below fails, the token must still be invalid for future attempts.
        await _tokenStore.MarkUsedAsync(tokenHash, now, cancellationToken).ConfigureAwait(false);

        // Step 1: Capture the OLD encrypted backup BEFORE generating the new
        // key pair (which would overwrite it). We may need this to re-encrypt
        // the user's data.
        EncryptedKeyBackup? oldBackup = null;
        string? oldPrivateKeyPem = null;

        if (_reencryption is not null)
        {
            oldBackup = await _keyManagement.GetEncryptedBackupAsync(record.UserId, cancellationToken)
                .ConfigureAwait(false);

            if (oldBackup is not null)
            {
                // Step 2: Unwrap the old private key. This requires the master
                // key that was used at wrap time, identified by MasterKeyId.
                try
                {
                    oldPrivateKeyPem = await _envelope.UnwrapAsync(
                        oldBackup.WrappedPrivateKey,
                        oldBackup.MasterKeyId,
                        cancellationToken).ConfigureAwait(false);

                    // Audit the access to the private key backup — this is the
                    // most sensitive operation in the system.
                    await _audit.RecordAsync(new AuditEntry(
                        Timestamp: DateTimeOffset.UtcNow,
                        Operation: AuditOperation.PrivateKeyBackupAccess,
                        UserId: record.UserId,
                        ActorId: null,
                        FieldName: null,
                        KeyVersion: oldBackup.KeyVersion,
                        Result: AuditResult.Success,
                        IpAddress: ipAddress), cancellationToken).ConfigureAwait(false);
                }
                catch (KrypteiaException ex)
                {
                    // The old backup couldn't be unwrapped. This is a serious
                    // operator-facing problem — likely a missing master key
                    // after rotation, or a corrupted backup. Audit, log, and
                    // continue with reset; the user gets a new key pair but
                    // loses access to old data.
                    await _audit.RecordAsync(new AuditEntry(
                        Timestamp: DateTimeOffset.UtcNow,
                        Operation: AuditOperation.PrivateKeyBackupAccess,
                        UserId: record.UserId,
                        ActorId: null,
                        FieldName: null,
                        KeyVersion: oldBackup.KeyVersion,
                        Result: AuditResult.Failure,
                        IpAddress: ipAddress,
                        ErrorCode: "unwrap_failed"), cancellationToken).ConfigureAwait(false);

                    Log.OldBackupUnwrapFailed(_logger, record.UserId, ex);
                    oldPrivateKeyPem = null;
                }
            }
        }

        // Step 3: Generate the new key pair. This overwrites the database row,
        // so the OLD encrypted backup is no longer retrievable after this call.
        KeyPair newPair = await _keyManagement.GenerateKeyPairAsync(record.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Step 4: If we have the old private key and a re-encryption service,
        // hand both off so the consumer can re-encrypt the user's data.
        if (_reencryption is not null && oldPrivateKeyPem is not null)
        {
            try
            {
                DataReencryptionResult result = await _reencryption.ReencryptUserDataAsync(
                    record.UserId,
                    oldPrivateKeyPem,
                    newPair.PublicKey,
                    cancellationToken).ConfigureAwait(false);

                Log.ReencryptionCompleted(_logger, record.UserId,
                    result.RecordsProcessed, result.RecordsFailed);

                if (result.RecordsFailed > 0)
                {
                    await _audit.RecordAsync(new AuditEntry(
                        Timestamp: DateTimeOffset.UtcNow,
                        Operation: AuditOperation.KeyResetCompleted,
                        UserId: record.UserId,
                        ActorId: null,
                        FieldName: null,
                        KeyVersion: newPair.Version,
                        Result: AuditResult.Failure,
                        IpAddress: ipAddress,
                        ErrorCode: $"reencryption_partial_{result.RecordsFailed}_failed"),
                        cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Re-encryption failed entirely. The user has a new key pair
                // but their old data is now unrecoverable. Audit loudly.
                await _audit.RecordAsync(new AuditEntry(
                    Timestamp: DateTimeOffset.UtcNow,
                    Operation: AuditOperation.KeyResetCompleted,
                    UserId: record.UserId,
                    ActorId: null,
                    FieldName: null,
                    KeyVersion: newPair.Version,
                    Result: AuditResult.Failure,
                    IpAddress: ipAddress,
                    ErrorCode: "reencryption_failed"), cancellationToken).ConfigureAwait(false);

                Log.ReencryptionFailed(_logger, record.UserId, ex);
            }
            finally
            {
                // Best-effort scrub of the unwrapped private key. .NET strings
                // are immutable so true zeroization isn't possible, but we
                // drop the reference so the GC can collect it.
                oldPrivateKeyPem = null;
            }
        }

        await _audit.RecordAsync(new AuditEntry(
            Timestamp: DateTimeOffset.UtcNow,
            Operation: AuditOperation.KeyResetCompleted,
            UserId: record.UserId,
            ActorId: null,
            FieldName: null,
            KeyVersion: newPair.Version,
            Result: AuditResult.Success,
            IpAddress: ipAddress), cancellationToken).ConfigureAwait(false);

        return newPair;
    }

    private Task AuditResetRejected(
        DateTimeOffset timestamp,
        string userId,
        string errorCode,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        return _audit.RecordAsync(new AuditEntry(
            Timestamp: timestamp,
            Operation: AuditOperation.KeyResetRejected,
            UserId: userId,
            ActorId: null,
            FieldName: null,
            KeyVersion: 0,
            Result: AuditResult.Denied,
            IpAddress: ipAddress,
            ErrorCode: errorCode), cancellationToken);
    }

    private static string HashToken(string token)
    {
        byte[] tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
        byte[] hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hashBytes);
    }

    private string BuildResetUrl(string token)
    {
        if (string.IsNullOrWhiteSpace(_options.ResetUrlBase))
        {
            throw new InvalidOperationException(
                "KeyResetOptions.ResetUrlBase must be configured before initiating a reset.");
        }

        string separator = _options.ResetUrlBase.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{_options.ResetUrlBase}{separator}token={Uri.EscapeDataString(token)}";
    }

    private EmailMessage BuildResetEmail(string userId, string resetUrl)
    {
        string body =
            $"""
            Hello,

            We received a request to reset the encryption key associated with your account ({userId}).

            To complete the reset, open this link in your browser:

            {resetUrl}

            This link expires in {(int)_options.TokenLifetime.TotalMinutes} minutes and can only be used once.

            If you did not request this reset, you can ignore this email — no changes will be made unless the link is clicked.

            Thanks,
            The team
            """;

        return new EmailMessage(
            To: userId,
            Subject: _options.EmailSubject,
            BodyHtml: null,
            BodyText: body);
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 3000,
            Level = LogLevel.Error,
            Message = "Failed to send reset email for user {UserId}.")]
        public static partial void EmailSendFailed(
            ILogger logger,
            string userId,
            Exception exception);

        [LoggerMessage(
            EventId = 3001,
            Level = LogLevel.Error,
            Message =
                "Could not unwrap the old encrypted backup for user {UserId}. " +
                "The reset will proceed but old data is no longer recoverable.")]
        public static partial void OldBackupUnwrapFailed(
            ILogger logger,
            string userId,
            Exception exception);

        [LoggerMessage(
            EventId = 3002,
            Level = LogLevel.Information,
            Message =
                "Re-encryption completed for user {UserId}: " +
                "{RecordsProcessed} records processed, {RecordsFailed} failed.")]
        public static partial void ReencryptionCompleted(
            ILogger logger,
            string userId,
            int recordsProcessed,
            int recordsFailed);

        [LoggerMessage(
            EventId = 3003,
            Level = LogLevel.Error,
            Message =
                "Re-encryption FAILED for user {UserId}. " +
                "User has a new key pair but old data is unrecoverable.")]
        public static partial void ReencryptionFailed(
            ILogger logger,
            string userId,
            Exception exception);
    }
}