using AwesomeAssertions;
using Krypteia;
using Krypteia.Abstractions;
using Krypteia.Audit;
using Krypteia.EntityFrameworkCore;
using Krypteia.KeyReset;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Krypteia.IntegrationTests;

/// <summary>
/// End-to-end tests for the full key reset flow with real Krypteia services
/// wired together against in-memory SQLite.
/// </summary>
/// <remarks>
/// Unlike the unit tests in <c>KeyResetServiceTests</c>, this suite uses real
/// instances of every dependency: real RSA key generation, real AES-GCM envelope,
/// real DbContext, real EF Core query translation. Catches integration issues
/// (DI registration mismatches, query translation failures, schema problems)
/// that unit tests can't see.
/// </remarks>
public class KeyResetFlowTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;
    private readonly InMemoryEmailSender _emailSender;

    public KeyResetFlowTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();

        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        services.AddDbContext<KrypteiaDbContext>(o => o.UseSqlite(_connection));

        // Capture sent emails so tests can extract reset tokens.
        _emailSender = new InMemoryEmailSender();
        services.AddSingleton<IEmailSender>(_emailSender);

        services.AddSingleton<IEncryptionService, RsaEncryptionService>();
        services.AddSingleton<IMasterKeyProvider>(_ => new InMemoryMasterKeyProvider("test-v1"));
        services.AddSingleton<IKeyEnvelope, AesGcmKeyEnvelope>();
        services.AddScoped<IAuditService, LoggerAuditService>();
        services.AddScoped<IKeyManagementService, EfCoreKeyManagementService>();
        services.AddScoped<IResetTokenStore, DbResetTokenStore>();
        services.AddScoped<IKeyResetService, KeyResetService>();

        services.Configure<KeyResetOptions>(o =>
        {
            o.ResetUrlBase = "https://test.example/reset";
            o.TokenLifetime = TimeSpan.FromMinutes(15);
            o.MaxAttemptsPerUser = 3;
            o.RateLimitWindow = TimeSpan.FromHours(1);
        });

        _services = services.BuildServiceProvider();

        using IServiceScope scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KrypteiaDbContext>();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task FullFlow_GenerateKey_InitiateReset_CompleteReset_ProducesNewKeyPair()
    {
        // Arrange: Alice has a key on file.
        using IServiceScope setupScope = _services.CreateScope();
        var keyManagement = setupScope.ServiceProvider.GetRequiredService<IKeyManagementService>();
        KeyPair originalPair = await keyManagement.GenerateKeyPairAsync("alice");

        // Act 1: initiate the reset.
        using (IServiceScope initiateScope = _services.CreateScope())
        {
            var resetService = initiateScope.ServiceProvider.GetRequiredService<IKeyResetService>();
            KeyResetInitiationResult result = await resetService.InitiateResetAsync("alice");
            result.Should().Be(KeyResetInitiationResult.Success);
        }

        // Extract the token from the email body (built like the production flow).
        EmailMessage email = _emailSender.SentMessages.Should().ContainSingle().Subject;
        string token = ExtractTokenFromEmail(email);
        token.Should().NotBeNullOrEmpty();

        // Act 2: complete the reset.
        KeyPair newPair;
        using (IServiceScope completeScope = _services.CreateScope())
        {
            var resetService = completeScope.ServiceProvider.GetRequiredService<IKeyResetService>();
            newPair = await resetService.CompleteResetAsync(token);
        }

        // Assert: new key pair is different and version bumped.
        newPair.PublicKey.Should().NotBe(originalPair.PublicKey);
        newPair.PrivateKey.Should().NotBe(originalPair.PrivateKey);
        newPair.Version.Should().Be(originalPair.Version + 1);
    }

    [Fact]
    public async Task ReusingAValidToken_FailsOnSecondAttempt()
    {
        using IServiceScope setupScope = _services.CreateScope();
        var keyManagement = setupScope.ServiceProvider.GetRequiredService<IKeyManagementService>();
        await keyManagement.GenerateKeyPairAsync("alice");

        using (IServiceScope initiateScope = _services.CreateScope())
        {
            var resetService = initiateScope.ServiceProvider.GetRequiredService<IKeyResetService>();
            await resetService.InitiateResetAsync("alice");
        }

        string token = ExtractTokenFromEmail(_emailSender.SentMessages.Single());

        // First completion: succeeds.
        using (IServiceScope firstScope = _services.CreateScope())
        {
            var resetService = firstScope.ServiceProvider.GetRequiredService<IKeyResetService>();
            await resetService.CompleteResetAsync(token);
        }

        // Second completion with the same token: throws.
        using (IServiceScope secondScope = _services.CreateScope())
        {
            var resetService = secondScope.ServiceProvider.GetRequiredService<IKeyResetService>();
            Func<Task> act = () => resetService.CompleteResetAsync(token);
            await act.Should().ThrowAsync<KeyResetException>();
        }
    }

    [Fact]
    public async Task RateLimit_KicksInAfterMaxAttempts()
    {
        using IServiceScope setupScope = _services.CreateScope();
        var keyManagement = setupScope.ServiceProvider.GetRequiredService<IKeyManagementService>();
        await keyManagement.GenerateKeyPairAsync("alice");

        // Three attempts allowed (the default).
        for (int i = 0; i < 3; i++)
        {
            using IServiceScope scope = _services.CreateScope();
            var resetService = scope.ServiceProvider.GetRequiredService<IKeyResetService>();
            KeyResetInitiationResult result = await resetService.InitiateResetAsync("alice");
            result.Should().Be(KeyResetInitiationResult.Success);
        }

        // Fourth attempt: rate limited.
        using (IServiceScope fourthScope = _services.CreateScope())
        {
            var resetService = fourthScope.ServiceProvider.GetRequiredService<IKeyResetService>();
            KeyResetInitiationResult result = await resetService.InitiateResetAsync("alice");
            result.Should().Be(KeyResetInitiationResult.RateLimited);
        }

        _emailSender.SentMessages.Should().HaveCount(3,
            "the fourth attempt should have been rejected before email sending");
    }

    [Fact]
    public async Task UnknownUser_ReturnsSuccessAndSendsNoEmail()
    {
        // Note: we do NOT call GenerateKeyPairAsync first, so this user
        // doesn't exist.
        using IServiceScope scope = _services.CreateScope();
        var resetService = scope.ServiceProvider.GetRequiredService<IKeyResetService>();

        KeyResetInitiationResult result = await resetService.InitiateResetAsync("nobody");

        result.Should().Be(KeyResetInitiationResult.Success);
        _emailSender.SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task AfterReset_OldPrivateKey_CannotDecryptDataEncryptedWithNewPublicKey()
    {
        using IServiceScope setupScope = _services.CreateScope();
        var keyManagement = setupScope.ServiceProvider.GetRequiredService<IKeyManagementService>();
        var encryption = setupScope.ServiceProvider.GetRequiredService<IEncryptionService>();

        KeyPair originalPair = await keyManagement.GenerateKeyPairAsync("alice");

        // Reset.
        using (IServiceScope initiateScope = _services.CreateScope())
        {
            await initiateScope.ServiceProvider.GetRequiredService<IKeyResetService>()
                .InitiateResetAsync("alice");
        }
        string token = ExtractTokenFromEmail(_emailSender.SentMessages.Single());
        KeyPair newPair;
        using (IServiceScope completeScope = _services.CreateScope())
        {
            newPair = await completeScope.ServiceProvider.GetRequiredService<IKeyResetService>()
                .CompleteResetAsync(token);
        }

        // Encrypt with the new public key.
        string ciphertext = await encryption.EncryptAsync("post-reset secret", newPair.PublicKey);

        // The OLD private key must fail to decrypt the new ciphertext.
        Func<Task> act = () => encryption.DecryptAsync(ciphertext, originalPair.PrivateKey);
        await act.Should().ThrowAsync<KrypteiaException>();

        // The new private key still works.
        string roundtripped = await encryption.DecryptAsync(ciphertext, newPair.PrivateKey);
        roundtripped.Should().Be("post-reset secret");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string ExtractTokenFromEmail(EmailMessage email)
    {
        const string marker = "token=";
        int start = email.BodyText.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        int end = email.BodyText.IndexOf('\n', start);
        return Uri.UnescapeDataString(email.BodyText[start..end].Trim());
    }

    /// <summary>
    /// Email sender that captures messages in memory for test inspection.
    /// </summary>
    private sealed class InMemoryEmailSender : IEmailSender
    {
        public List<EmailMessage> SentMessages { get; } = new();

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return Task.CompletedTask;
        }
    }
}