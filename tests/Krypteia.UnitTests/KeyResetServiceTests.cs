using AwesomeAssertions;
using Krypteia.Abstractions;
using Krypteia.KeyReset;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Krypteia.UnitTests;

/// <summary>
/// Unit tests for <see cref="KeyResetService"/>. All dependencies are
/// substituted so we can isolate the orchestration logic from the real
/// database, crypto, and email infrastructure.
/// </summary>
public class KeyResetServiceTests
{
    private readonly IKeyManagementService _keyManagement;
    private readonly IKeyEnvelope _envelope;
    private readonly IMasterKeyProvider _masterKeys;
    private readonly IResetTokenStore _tokenStore;
    private readonly IEmailSender _emailSender;
    private readonly IAuditService _audit;
    private readonly KeyResetOptions _options;

    private const string ValidUser = "alice";
    private const string ResetUrlBase = "https://app.example.com/reset";

    public KeyResetServiceTests()
    {
        _keyManagement = Substitute.For<IKeyManagementService>();
        _envelope = Substitute.For<IKeyEnvelope>();
        _masterKeys = Substitute.For<IMasterKeyProvider>();
        _tokenStore = Substitute.For<IResetTokenStore>();
        _emailSender = Substitute.For<IEmailSender>();
        _audit = Substitute.For<IAuditService>();

        _options = new KeyResetOptions
        {
            ResetUrlBase = ResetUrlBase,
            TokenLifetime = TimeSpan.FromMinutes(15),
            MaxAttemptsPerUser = 3,
            RateLimitWindow = TimeSpan.FromHours(1),
        };

        // Default: ValidUser exists with a public key on file.
        _masterKeys.CurrentKeyId.Returns("v1");
        _keyManagement.GetPublicKeyAsync(ValidUser, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>("-----BEGIN PUBLIC KEY-----\nfake\n-----END PUBLIC KEY-----"));
        _tokenStore.CountRecentAttemptsAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));
    }

    private KeyResetService CreateSut(IDataReencryptionService? reencryption = null) =>
        new(
            _keyManagement,
            _envelope,
            _masterKeys,
            _tokenStore,
            _emailSender,
            _audit,
            Options.Create(_options),
            NullLogger<KeyResetService>.Instance,
            reencryption);

    // ─────────────────────────────────────────────────────────────────────────
    // InitiateResetAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InitiateResetAsync_ForKnownUser_StoresTokenAndSendsEmail()
    {
        KeyResetService sut = CreateSut();

        KeyResetInitiationResult result = await sut.InitiateResetAsync(ValidUser);

        result.Should().Be(KeyResetInitiationResult.Success);
        await _tokenStore.Received(1).StoreAsync(
            Arg.Is<ResetTokenRecord>(r => r.UserId == ValidUser),
            Arg.Any<CancellationToken>());
        await _emailSender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.To == ValidUser),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiateResetAsync_ForUnknownUser_DoesNotSendEmailButReturnsSuccess()
    {
        // Anti-enumeration: from the caller's perspective, unknown user
        // and successful initiation produce identical responses.
        _keyManagement.GetPublicKeyAsync("ghost", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));

        KeyResetService sut = CreateSut();
        KeyResetInitiationResult result = await sut.InitiateResetAsync("ghost");

        result.Should().Be(KeyResetInitiationResult.Success);
        await _tokenStore.DidNotReceive().StoreAsync(Arg.Any<ResetTokenRecord>(), Arg.Any<CancellationToken>());
        await _emailSender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiateResetAsync_WhenAtRateLimit_ReturnsRateLimitedAndDoesNotSendEmail()
    {
        _tokenStore.CountRecentAttemptsAsync(ValidUser, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(_options.MaxAttemptsPerUser)); // exactly at limit

        KeyResetService sut = CreateSut();
        KeyResetInitiationResult result = await sut.InitiateResetAsync(ValidUser);

        result.Should().Be(KeyResetInitiationResult.RateLimited);
        await _emailSender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await _tokenStore.DidNotReceive().StoreAsync(Arg.Any<ResetTokenRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiateResetAsync_StoresOnlyTokenHashNotTokenValue()
    {
        ResetTokenRecord? capturedRecord = null;
        _tokenStore.StoreAsync(Arg.Do<ResetTokenRecord>(r => capturedRecord = r), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        EmailMessage? capturedEmail = null;
        _emailSender.SendAsync(Arg.Do<EmailMessage>(m => capturedEmail = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        KeyResetService sut = CreateSut();
        await sut.InitiateResetAsync(ValidUser);

        capturedRecord.Should().NotBeNull();
        capturedEmail.Should().NotBeNull();

        // The hash stored must not equal the token sent in the email.
        // Pull the token out of the URL in the email body.
        string emailBody = capturedEmail!.BodyText;
        int tokenStart = emailBody.IndexOf("token=", StringComparison.Ordinal) + "token=".Length;
        int tokenEnd = emailBody.IndexOf('\n', tokenStart);
        string tokenInEmail = emailBody[tokenStart..tokenEnd].Trim();

        capturedRecord!.TokenHash.Should().NotBe(tokenInEmail,
            "the persisted hash must not equal the plaintext token");
        capturedRecord.TokenHash.Should().HaveLength(64, "SHA-256 hex is 64 characters");
    }

    [Fact]
    public async Task InitiateResetAsync_GeneratesUniqueTokensAcrossCalls()
    {
        var capturedRecords = new List<ResetTokenRecord>();
        _tokenStore.StoreAsync(Arg.Do<ResetTokenRecord>(r => capturedRecords.Add(r)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        KeyResetService sut = CreateSut();
        await sut.InitiateResetAsync(ValidUser);
        await sut.InitiateResetAsync(ValidUser);
        await sut.InitiateResetAsync(ValidUser);

        capturedRecords.Should().HaveCount(3);
        capturedRecords.Select(r => r.TokenHash).Distinct().Should().HaveCount(3,
            "each token must be unique");
    }

    [Fact]
    public async Task InitiateResetAsync_WhenEmailFails_StillReturnsSuccessButAudits()
    {
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP down")));

        KeyResetService sut = CreateSut();
        KeyResetInitiationResult result = await sut.InitiateResetAsync(ValidUser);

        // Caller-facing result is unchanged — failure leakage is bad.
        result.Should().Be(KeyResetInitiationResult.Success);

        // But operator-facing audit reflects what happened.
        await _audit.Received().RecordAsync(
            Arg.Is<AuditEntry>(e =>
                e.Result == AuditResult.Failure &&
                e.ErrorCode == "email_send_failed"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiateResetAsync_WithoutResetUrlBaseConfigured_Throws()
    {
        _options.ResetUrlBase = string.Empty;

        KeyResetService sut = CreateSut();

        Func<Task> act = () => sut.InitiateResetAsync(ValidUser);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ResetUrlBase must be configured*");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CompleteResetAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CompleteResetAsync_WithValidToken_GeneratesNewKeyPair()
    {
        // Arrange: store a token, then look it up under the same hash.
        const string tokenValue = "valid-token-payload";
        string tokenHash = HashToken(tokenValue);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var storedRecord = new ResetTokenRecord(
            TokenHash: tokenHash,
            UserId: ValidUser,
            CreatedAt: now - TimeSpan.FromMinutes(5),
            ExpiresAt: now + TimeSpan.FromMinutes(10),
            UsedAt: null);

        _tokenStore.FindByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ResetTokenRecord?>(storedRecord));

        var newPair = new KeyPair("new-public", "new-private", 2);
        _keyManagement.GenerateKeyPairAsync(ValidUser, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(newPair));

        KeyResetService sut = CreateSut();

        // Act
        KeyPair result = await sut.CompleteResetAsync(tokenValue);

        // Assert
        result.Should().BeSameAs(newPair);
        await _tokenStore.Received(1).MarkUsedAsync(tokenHash, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _keyManagement.Received(1).GenerateKeyPairAsync(ValidUser, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteResetAsync_WithUnknownToken_Throws()
    {
        _tokenStore.FindByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ResetTokenRecord?>(null));

        KeyResetService sut = CreateSut();
        Func<Task> act = () => sut.CompleteResetAsync("unknown-token");

        await act.Should().ThrowAsync<KeyResetException>();
        await _keyManagement.DidNotReceive().GenerateKeyPairAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteResetAsync_WithExpiredToken_Throws()
    {
        const string tokenValue = "expired-token";
        DateTimeOffset now = DateTimeOffset.UtcNow;

        _tokenStore.FindByHashAsync(HashToken(tokenValue), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ResetTokenRecord?>(new ResetTokenRecord(
                TokenHash: HashToken(tokenValue),
                UserId: ValidUser,
                CreatedAt: now - TimeSpan.FromHours(1),
                ExpiresAt: now - TimeSpan.FromMinutes(1), // expired 1 min ago
                UsedAt: null)));

        KeyResetService sut = CreateSut();
        Func<Task> act = () => sut.CompleteResetAsync(tokenValue);

        await act.Should().ThrowAsync<KeyResetException>();
        await _keyManagement.DidNotReceive().GenerateKeyPairAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteResetAsync_WithAlreadyUsedToken_Throws()
    {
        const string tokenValue = "used-token";
        DateTimeOffset now = DateTimeOffset.UtcNow;

        _tokenStore.FindByHashAsync(HashToken(tokenValue), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ResetTokenRecord?>(new ResetTokenRecord(
                TokenHash: HashToken(tokenValue),
                UserId: ValidUser,
                CreatedAt: now - TimeSpan.FromMinutes(10),
                ExpiresAt: now + TimeSpan.FromMinutes(5),
                UsedAt: now - TimeSpan.FromMinutes(2))));

        KeyResetService sut = CreateSut();
        Func<Task> act = () => sut.CompleteResetAsync(tokenValue);

        await act.Should().ThrowAsync<KeyResetException>();
        await _keyManagement.DidNotReceive().GenerateKeyPairAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteResetAsync_GenericErrorMessage_DoesNotLeakWhichValidationFailed()
    {
        // All three failure modes — unknown, expired, used — must produce
        // the same exception message to avoid leaking why a token failed.
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Case 1: unknown
        _tokenStore.FindByHashAsync("hash-of-unknown", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ResetTokenRecord?>(null));

        // Case 2: expired
        _tokenStore.FindByHashAsync(HashToken("expired"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ResetTokenRecord?>(new ResetTokenRecord(
                HashToken("expired"), ValidUser, now - TimeSpan.FromHours(1), now - TimeSpan.FromSeconds(1), null)));

        // Case 3: used
        _tokenStore.FindByHashAsync(HashToken("used"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ResetTokenRecord?>(new ResetTokenRecord(
                HashToken("used"), ValidUser, now - TimeSpan.FromMinutes(10), now + TimeSpan.FromMinutes(5), now - TimeSpan.FromMinutes(1))));

        KeyResetService sut = CreateSut();

        KeyResetException? unknownEx = await Capture<KeyResetException>(() => sut.CompleteResetAsync("unknown"));
        KeyResetException? expiredEx = await Capture<KeyResetException>(() => sut.CompleteResetAsync("expired"));
        KeyResetException? usedEx = await Capture<KeyResetException>(() => sut.CompleteResetAsync("used"));

        unknownEx.Should().NotBeNull();
        expiredEx.Should().NotBeNull();
        usedEx.Should().NotBeNull();
        unknownEx!.Message.Should().Be(expiredEx!.Message);
        expiredEx.Message.Should().Be(usedEx!.Message);
    }

    [Fact]
    public async Task CompleteResetAsync_WithReencryptionService_PassesOldPrivateKeyToIt()
    {
        const string tokenValue = "valid-token";
        DateTimeOffset now = DateTimeOffset.UtcNow;

        _tokenStore.FindByHashAsync(HashToken(tokenValue), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ResetTokenRecord?>(new ResetTokenRecord(
                HashToken(tokenValue), ValidUser, now - TimeSpan.FromMinutes(5), now + TimeSpan.FromMinutes(10), null)));

        // The old encrypted backup that should be unwrapped before the new pair is generated.
        _keyManagement.GetEncryptedBackupAsync(ValidUser, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EncryptedKeyBackup?>(new EncryptedKeyBackup("old-wrapped", "v1", 1)));

        _envelope.UnwrapAsync("old-wrapped", "v1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("OLD-PRIVATE-KEY-PEM"));

        var newPair = new KeyPair("new-public", "new-private", 2);
        _keyManagement.GenerateKeyPairAsync(ValidUser, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(newPair));

        IDataReencryptionService reencryption = Substitute.For<IDataReencryptionService>();
        reencryption.ReencryptUserDataAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DataReencryptionResult(5, 0)));

        KeyResetService sut = CreateSut(reencryption);

        await sut.CompleteResetAsync(tokenValue);

        // Verify re-encryption was called with the old private key and the new public key.
        await reencryption.Received(1).ReencryptUserDataAsync(
            ValidUser,
            "OLD-PRIVATE-KEY-PEM",
            "new-public",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteResetAsync_WithoutReencryptionService_DoesNotCallEnvelopeUnwrap()
    {
        const string tokenValue = "valid-token";
        DateTimeOffset now = DateTimeOffset.UtcNow;

        _tokenStore.FindByHashAsync(HashToken(tokenValue), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ResetTokenRecord?>(new ResetTokenRecord(
                HashToken(tokenValue), ValidUser, now - TimeSpan.FromMinutes(5), now + TimeSpan.FromMinutes(10), null)));

        _keyManagement.GenerateKeyPairAsync(ValidUser, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new KeyPair("new-public", "new-private", 2)));

        KeyResetService sut = CreateSut(reencryption: null);

        await sut.CompleteResetAsync(tokenValue);

        await _envelope.DidNotReceive().UnwrapAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _keyManagement.DidNotReceive().GetEncryptedBackupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Argument validation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InitiateResetAsync_WithNullUserId_Throws()
    {
        KeyResetService sut = CreateSut();
        Func<Task> act = () => sut.InitiateResetAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CompleteResetAsync_WithNullToken_Throws()
    {
        KeyResetService sut = CreateSut();
        Func<Task> act = () => sut.CompleteResetAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string HashToken(string token)
    {
        byte[] tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
        byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hashBytes);
    }

    private static async Task<T?> Capture<T>(Func<Task> action) where T : Exception
    {
        try
        {
            await action();
            return null;
        }
        catch (T ex)
        {
            return ex;
        }
    }
}