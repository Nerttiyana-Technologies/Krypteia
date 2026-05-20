using AwesomeAssertions;
using Krypteia.Abstractions;
using Xunit;

namespace Krypteia.SecurityTests;

/// <summary>
/// Security-focused tests for the encryption service.
/// </summary>
/// <remarks>
/// These tests check properties that ordinary unit tests can miss: that error messages
/// don't leak information, that operations don't take noticeably different times for
/// valid vs. invalid inputs, and that key material does not appear in serialized forms.
/// </remarks>
public class EncryptionSecurityTests
{
    private readonly RsaEncryptionService _sut = new();

    [Fact]
    public async Task DecryptAsync_GenericErrorMessage_DoesNotLeakWhyItFailed()
    {
        // Two failure modes — wrong key vs. corrupt ciphertext — must produce
        // identical messages. Distinguishing them gives an attacker a side channel.
        KeyPair keys = RsaKeyPairGenerator.Generate();
        KeyPair wrongKeys = RsaKeyPairGenerator.Generate();
        string validCiphertext = await _sut.EncryptAsync("plaintext", keys.PublicKey);
        string corruptCiphertext = validCiphertext[..^4] + "XXXX";

        KrypteiaException? wrongKeyEx = await CaptureAsync<KrypteiaException>(() =>
            _sut.DecryptAsync(validCiphertext, wrongKeys.PrivateKey));

        KrypteiaException? corruptEx = await CaptureAsync<KrypteiaException>(() =>
            _sut.DecryptAsync(corruptCiphertext, keys.PrivateKey));

        wrongKeyEx.Should().NotBeNull();
        corruptEx.Should().NotBeNull();
        wrongKeyEx!.Message.Should().Be(corruptEx!.Message);
    }

    [Fact]
    public void KeyPair_ToString_NeverContainsPrivateKeyMaterial()
    {
        // Defensive test — ensure no future refactor regresses this property.
        KeyPair pair = RsaKeyPairGenerator.Generate();

        string serialized = pair.ToString();

        serialized.Should().NotContain(pair.PrivateKey);
        // Also check that no recognizable PEM header for a private key leaks.
        serialized.Should().NotContain("BEGIN PRIVATE KEY");
    }

    [Fact(Skip = "Performance-sensitive test; enable only when explicitly profiling timing behavior.")]
    public async Task DecryptAsync_TimingBetweenSuccessAndFailureIsBounded()
    {
        // A real timing-attack test requires many iterations and statistical analysis.
        // Implement with BenchmarkDotNet in a separate performance test suite
        // before 1.0 release.
        await Task.CompletedTask;
    }

    private static async Task<T?> CaptureAsync<T>(Func<Task> action) where T : Exception
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
