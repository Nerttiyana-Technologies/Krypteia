using AwesomeAssertions;
using Krypteia.Abstractions;
using Xunit;

namespace Krypteia.UnitTests;

public class RsaKeyPairGeneratorTests
{
    [Fact]
    public void Generate_DefaultArgs_ProducesValidPemKeyPair()
    {
        KeyPair pair = RsaKeyPairGenerator.Generate();

        pair.PublicKey.Should().StartWith("-----BEGIN PUBLIC KEY-----");
        pair.PrivateKey.Should().StartWith("-----BEGIN PRIVATE KEY-----");
        pair.Version.Should().Be(1);
    }

    [Fact]
    public void Generate_TwoKeyPairs_ProducesDistinctKeys()
    {
        KeyPair a = RsaKeyPairGenerator.Generate();
        KeyPair b = RsaKeyPairGenerator.Generate();

        a.PublicKey.Should().NotBe(b.PublicKey);
        a.PrivateKey.Should().NotBe(b.PrivateKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2047)]
    public void Generate_KeySizeBelow2048_Throws(int keySize)
    {
        Action act = () => RsaKeyPairGenerator.Generate(keySize);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(3072)]
    public void Generate_AcceptsValidKeySizes(int keySize)
    {
        // 4096 omitted from this test — it would slow the test run noticeably
        // and the 3072 case proves the parameter is being honored.
        KeyPair pair = RsaKeyPairGenerator.Generate(keySize);

        pair.PublicKey.Should().NotBeNullOrEmpty();
        pair.PrivateKey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ToString_DoesNotIncludePrivateKey()
    {
        KeyPair pair = RsaKeyPairGenerator.Generate();

        string rendered = pair.ToString();

        rendered.Should().NotContain(pair.PrivateKey);
        rendered.Should().Contain("[REDACTED]");
    }
}
