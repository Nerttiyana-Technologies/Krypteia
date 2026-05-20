using AwesomeAssertions;
using Krypteia.Abstractions;
using Xunit;

namespace Krypteia.UnitTests;

public class RsaEncryptionServiceTests
{
    private readonly RsaEncryptionService _sut = new();

    [Fact]
    public async Task EncryptAsync_ThenDecryptAsync_RoundtripsCorrectly()
    {
        // Arrange
        KeyPair keys = RsaKeyPairGenerator.Generate();
        const string plaintext = "user-ssn-123-45-6789";

        // Act
        string ciphertext = await _sut.EncryptAsync(plaintext, keys.PublicKey);
        string roundtripped = await _sut.DecryptAsync(ciphertext, keys.PrivateKey);

        // Assert
        roundtripped.Should().Be(plaintext);
        ciphertext.Should().NotBe(plaintext, "ciphertext must differ from plaintext");
    }

    [Fact]
    public async Task EncryptAsync_ProducesDifferentCiphertextEachTime()
    {
        // OAEP is randomized — encrypting the same plaintext twice must yield different ciphertexts.
        KeyPair keys = RsaKeyPairGenerator.Generate();
        const string plaintext = "sensitive";

        string first = await _sut.EncryptAsync(plaintext, keys.PublicKey);
        string second = await _sut.EncryptAsync(plaintext, keys.PublicKey);

        first.Should().NotBe(second);
    }

    [Fact]
    public async Task DecryptAsync_WithWrongPrivateKey_ThrowsKrypteiaException()
    {
        KeyPair keysA = RsaKeyPairGenerator.Generate();
        KeyPair keysB = RsaKeyPairGenerator.Generate();
        string ciphertext = await _sut.EncryptAsync("hello", keysA.PublicKey);

        Func<Task> act = () => _sut.DecryptAsync(ciphertext, keysB.PrivateKey);

        await act.Should()
            .ThrowAsync<KrypteiaException>()
            .WithMessage("Decryption failed.");
    }

    [Fact]
    public async Task EncryptAsync_WithNullPlaintext_ThrowsArgumentNullException()
    {
        KeyPair keys = RsaKeyPairGenerator.Generate();

        Func<Task> act = () => _sut.EncryptAsync(null!, keys.PublicKey);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EncryptAsync_WithNullPublicKey_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _sut.EncryptAsync("plaintext", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EncryptAsync_WithCancelledToken_ThrowsOperationCancelled()
    {
        KeyPair keys = RsaKeyPairGenerator.Generate();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => _sut.EncryptAsync("plaintext", keys.PublicKey, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("Hello, world!")]
    [InlineData("Unicode: नमस्ते 你好 مرحبا")]
    public async Task EncryptAsync_HandlesVariousPlaintexts(string plaintext)
    {
        KeyPair keys = RsaKeyPairGenerator.Generate();

        string ciphertext = await _sut.EncryptAsync(plaintext, keys.PublicKey);
        string decrypted = await _sut.DecryptAsync(ciphertext, keys.PrivateKey);

        decrypted.Should().Be(plaintext);
    }
}
