using Krypteia.Abstractions;
using Krypteia.AspNetCore;
using Krypteia.EntityFrameworkCore;
using Krypteia.KeyReset;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Native .NET 10 OpenAPI document generation.
builder.Services.AddOpenApi();

// ─────────────────────────────────────────────────────────────────────────────
// Krypteia configuration
// ─────────────────────────────────────────────────────────────────────────────

string keysDirectory = Path.Combine(AppContext.BaseDirectory, "keys");
string sqliteConnection = builder.Configuration.GetConnectionString("Krypteia")
    ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "krypteia.db")}";

builder.Services.AddDbContext<KrypteiaDbContext>(options =>
    options.UseSqlite(
        sqliteConnection,
        b => b.MigrationsAssembly("Krypteia.Samples.WebApi")));

builder.Services.AddKrypteia()
    .AddFileMasterKeyProvider(o =>
    {
        o.Directory = keysDirectory;
        o.CurrentKeyId = "v1";
    })
    .AddKrypteiaPersistence<KrypteiaDbContext>()
    .AddKrypteiaKeyReset(o =>
    {
        o.ResetUrlBase = "https://localhost:7160/api/keyreset/complete";
        o.FromAddress = "no-reply@krypteia.example";
        o.EmailSubject = "Reset your Krypteia encryption key";
    });

// Register the email sender based on the "EmailSender" config section.
// Default in appsettings.json is "Console" — change Provider to "Smtp" to
// use a real SMTP server. For SendGrid, reference the
// Krypteia.EmailSender.SendGrid package and call
// AddKrypteiaSendGridEmailSender(...) instead of this line.
builder.Services.AddKrypteiaEmailSender(builder.Configuration);

var app = builder.Build();

// Ensure the SQLite schema exists. For real apps, use EF Core migrations.
using (IServiceScope scope = app.Services.CreateScope())
{
    KrypteiaDbContext db = scope.ServiceProvider.GetRequiredService<KrypteiaDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// ─────────────────────────────────────────────────────────────────────────────
// Key generation endpoints — three delivery options for the new private key
// ─────────────────────────────────────────────────────────────────────────────

app.MapPost("/api/users/{userId}/keys", async (
    string userId,
    IKeyManagementService keyManagement) =>
{
    KeyPair pair = await keyManagement.GenerateKeyPairAsync(userId);

    return Results.Ok(new KeyGenerationResponse(
        UserId: userId,
        PublicKey: pair.PublicKey,
        PrivateKey: pair.PrivateKey,
        Version: pair.Version,
        Warning: "Save the private key NOW. It will not be retrievable from this endpoint again."));
})
.WithName("GenerateKeyPairJson")
.WithSummary("Generate a key pair and return it as JSON.");

app.MapPost("/api/users/{userId}/keys/download", async (
    string userId,
    IKeyManagementService keyManagement) =>
{
    KeyPair pair = await keyManagement.GenerateKeyPairAsync(userId);

    byte[] privateKeyBytes = System.Text.Encoding.UTF8.GetBytes(pair.PrivateKey);
    string filename = $"{userId}-private-key-v{pair.Version}.pem";

    return Results.File(
        privateKeyBytes,
        contentType: "application/x-pem-file",
        fileDownloadName: filename);
})
.WithName("GenerateKeyPairDownload")
.WithSummary("Generate a key pair and return the private key as a .pem download.");

app.MapPost("/api/users/{userId}/keys/display", async (
    string userId,
    IKeyManagementService keyManagement) =>
{
    KeyPair pair = await keyManagement.GenerateKeyPairAsync(userId);

    return Results.Ok(new KeyGenerationDisplayResponse(
        UserId: userId,
        PublicKey: pair.PublicKey,
        PrivateKey: pair.PrivateKey,
        Version: pair.Version,
        DisplayInstructions: "Render PrivateKey in a read-only textarea with a copy-to-clipboard button. Tell the user to save it in a password manager before closing the page."));
})
.WithName("GenerateKeyPairDisplay")
.WithSummary("Generate a key pair intended for client-side display + copy UX.");

// ─────────────────────────────────────────────────────────────────────────────
// Key reset endpoints — the email-token flow
// ─────────────────────────────────────────────────────────────────────────────

app.MapPost("/api/keyreset/initiate", async (
    KeyResetInitiateRequest request,
    IKeyResetService keyReset,
    HttpContext http) =>
{
    string? ip = http.Connection.RemoteIpAddress?.ToString();
    KeyResetInitiationResult result = await keyReset.InitiateResetAsync(request.UserId, ip);

    return result switch
    {
        KeyResetInitiationResult.Success => Results.Accepted(value: new
        {
            message = "If an account with that identifier exists, a reset link has been sent."
        }),
        KeyResetInitiationResult.RateLimited => Results.StatusCode(429),
        _ => Results.StatusCode(500),
    };
})
.WithName("InitiateKeyReset")
.WithSummary("Request a key reset. The user receives an email with a one-time link.");

app.MapPost("/api/keyreset/complete", async (
    KeyResetCompleteRequest request,
    IKeyResetService keyReset,
    HttpContext http) =>
{
    string? ip = http.Connection.RemoteIpAddress?.ToString();

    try
    {
        KeyPair newPair = await keyReset.CompleteResetAsync(request.Token, ip);

        return Results.Ok(new KeyResetCompleteResponse(
            PublicKey: newPair.PublicKey,
            PrivateKey: newPair.PrivateKey,
            Version: newPair.Version,
            Warning: "Save the new private key NOW. Your old key and any data encrypted under it cannot be recovered unless a re-encryption service was configured."));
    }
    catch (KeyResetException)
    {
        return Results.BadRequest(new { error = "The reset link is invalid or expired." });
    }
})
.WithName("CompleteKeyReset")
.WithSummary("Complete a key reset using a token from the reset email.");

// ─────────────────────────────────────────────────────────────────────────────
// Encryption endpoints
// ─────────────────────────────────────────────────────────────────────────────

app.MapGet("/api/users/{userId}/publickey", async (
    string userId,
    IKeyManagementService keyManagement) =>
{
    string? publicKey = await keyManagement.GetPublicKeyAsync(userId);
    return publicKey is null
        ? Results.NotFound()
        : Results.Ok(new { userId, publicKey });
})
.WithName("GetPublicKey")
.WithSummary("Retrieve a user's current public key.");

app.MapPost("/api/encrypt", async (
    EncryptRequest request,
    IEncryptionService encryption,
    IKeyManagementService keyManagement) =>
{
    string? publicKey = await keyManagement.GetPublicKeyAsync(request.UserId);
    if (publicKey is null)
    {
        return Results.NotFound(new { error = "User has no key on file." });
    }

    string ciphertext = await encryption.EncryptAsync(request.Plaintext, publicKey);
    return Results.Ok(new { ciphertext });
})
.WithName("Encrypt")
.WithSummary("Encrypt a value with a user's stored public key.");

app.MapPost("/api/decrypt", async (
    DecryptRequest request,
    IEncryptionService encryption) =>
{
    string plaintext = await encryption.DecryptAsync(request.Ciphertext, request.PrivateKey);
    return Results.Ok(new { plaintext });
})
.WithName("Decrypt")
.WithSummary("Decrypt a value using a caller-supplied private key.");

app.Run();

// ─────────────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────────────

internal sealed record KeyGenerationResponse(
    string UserId, string PublicKey, string PrivateKey, int Version, string Warning);

internal sealed record KeyGenerationDisplayResponse(
    string UserId, string PublicKey, string PrivateKey, int Version, string DisplayInstructions);

internal sealed record KeyResetInitiateRequest(string UserId);
internal sealed record KeyResetCompleteRequest(string Token);
internal sealed record KeyResetCompleteResponse(
    string PublicKey, string PrivateKey, int Version, string Warning);

internal sealed record EncryptRequest(string UserId, string Plaintext);
internal sealed record DecryptRequest(string Ciphertext, string PrivateKey);