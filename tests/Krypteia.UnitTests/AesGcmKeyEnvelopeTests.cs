using AwesomeAssertions;
using Krypteia.Abstractions;
using Xunit;

namespace Krypteia.UnitTests;

public class AesGcmKeyEnvelopeTests
{
    private readonly InMemoryMasterKeyProvider _masterKeys = new();
    private readonly AesGcmKeyEnvelope _sut;

    public AesGcmKeyEnvelopeTests()
    {
        _sut = new AesGcmKeyEnvelope(_masterKeys);
    }

    [Fact]
    public async Task WrapAsync_ThenUnwrapAsync_RoundtripsPrivateKey()
    {
        KeyPair keys = RsaKeyPairGenerator.Generate();

        string wrapped = await _sut.WrapAsync(keys.PrivateKey, _masterKeys.CurrentKeyId);
        string unwrapped = await _sut.UnwrapAsync(wrapped, _masterKeys.CurrentKeyId);

        unwrapped.Should().Be(keys.PrivateKey);
    }

    [Fact]
    public async Task WrapAsync_ProducesDifferentCiphertextEachTime()
    {
        // Fresh random nonce each call means the same plaintext under the
        // same master key must produce different wrapped output.
        KeyPair keys = RsaKeyPairGenerator.Generate();

        string first = await _sut.WrapAsync(keys.PrivateKey, _masterKeys.CurrentKeyId);
        string second = await _sut.WrapAsync(keys.PrivateKey, _masterKeys.CurrentKeyId);

        first.Should().NotBe(second);
    }

    [Fact]
    public async Task UnwrapAsync_WithWrongMasterKey_ThrowsKrypteiaException()
    {
        // Wrap with one master key, attempt to unwrap with another.
        KeyPair keys = RsaKeyPairGenerator.Generate();
        string wrapped = await _sut.WrapAsync(keys.PrivateKey, _masterKeys.CurrentKeyId);

        // Add a second master key with a different ID.
        var otherMasterKeys = new InMemoryMasterKeyProvider("v2");
        var otherEnvelope = new AesGcmKeyEnvelope(otherMasterKeys);

        Func<Task> act = () => otherEnvelope.UnwrapAsync(wrapped, "v2");

        await act.Should()
            .ThrowAsync<KrypteiaException>()
            .WithMessage("Key unwrap failed.");
    }

    [Fact]
    public async Task UnwrapAsync_WithTamperedCiphertext_ThrowsKrypteiaException()
    {
        // AES-GCM authenticates ciphertext: any byte flip must cause failure.
        KeyPair keys = RsaKeyPairGenerator.Generate();
        string wrapped = await _sut.WrapAsync(keys.PrivateKey, _masterKeys.CurrentKeyId);

        // Flip a bit somewhere inside the Base64 payload.
        // Decode, flip a byte deep enough to land in the ciphertext, re-encode.
        byte[] bytes = Convert.FromBase64String(wrapped);
        bytes[^1] ^= 0x01;
        string tampered = Convert.ToBase64String(bytes);

        Func<Task> act = () => _sut.UnwrapAsync(tampered, _masterKeys.CurrentKeyId);

        await act.Should().ThrowAsync<KrypteiaException>();
    }

    [Fact]
    public async Task UnwrapAsync_WithMalformedBase64_ThrowsKrypteiaException()
    {
        Func<Task> act = () => _sut.UnwrapAsync("not-valid-base64!!!", _masterKeys.CurrentKeyId);

        await act.Should().ThrowAsync<KrypteiaException>();
    }

    [Fact]
    public async Task UnwrapAsync_WithTruncatedBlob_ThrowsKrypteiaException()
    {
        // A blob shorter than version + nonce + tag (29 bytes) is structurally invalid.
        byte[] tooShort = new byte[10];
        string blob = Convert.ToBase64String(tooShort);

        Func<Task> act = () => _sut.UnwrapAsync(blob, _masterKeys.CurrentKeyId);

        await act.Should().ThrowAsync<KrypteiaException>();
    }

    [Fact]
    public async Task UnwrapAsync_WithUnknownVersionByte_ThrowsKrypteiaException()
    {
        // Build a blob with the version byte set to something unsupported.
        byte[] blob = new byte[1 + 12 + 16 + 10];
        blob[0] = 0xFF; // unsupported version
        string b64 = Convert.ToBase64String(blob);

        Func<Task> act = () => _sut.UnwrapAsync(b64, _masterKeys.CurrentKeyId);

        await act.Should().ThrowAsync<KrypteiaException>();
    }

    [Fact]
    public async Task WrapAsync_WithNullPrivateKey_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _sut.WrapAsync(null!, _masterKeys.CurrentKeyId);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task WrapAsync_WithNullMasterKeyId_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _sut.WrapAsync("dummy-pem", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task WrapAsync_WithUnknownMasterKeyId_ThrowsKrypteiaException()
    {
        Func<Task> act = () => _sut.WrapAsync("dummy-pem", "no-such-key-id");

        await act.Should().ThrowAsync<KrypteiaException>();
    }
}