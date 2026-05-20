using System.Runtime.InteropServices;
using AwesomeAssertions;
using Krypteia.Abstractions;
using Xunit;

namespace Krypteia.UnitTests;

/// <summary>
/// Tests for <see cref="FileMasterKeyProvider"/>. Each test uses a unique
/// temp directory so concurrent test runs don't interfere with each other.
/// </summary>
public class FileMasterKeyProviderTests : IDisposable
{
    private readonly string _tempDir;

    public FileMasterKeyProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "krypteia-test-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        // Required by CA1816: prevents finalizer from running if a subclass
        // adds one. We have no unmanaged resources so there's no other cleanup.
        GC.SuppressFinalize(this);
    }

    private FileMasterKeyProvider CreateSut(string currentKeyId = "v1") =>
        new(new FileMasterKeyProviderOptions
        {
            Directory = _tempDir,
            CurrentKeyId = currentKeyId,
        });

    [Fact]
    public async Task GetMasterKeyAsync_OnFirstRun_GeneratesKey()
    {
        // The directory doesn't exist yet — calling GetMasterKeyAsync should
        // create it and write a new key file for CurrentKeyId.
        FileMasterKeyProvider sut = CreateSut();

        byte[] key = await sut.GetMasterKeyAsync("v1");

        key.Should().HaveCount(32, "AES-256 keys are 32 bytes");
        File.Exists(Path.Combine(_tempDir, "v1.key")).Should().BeTrue();
    }

    [Fact]
    public async Task GetMasterKeyAsync_OnSecondRun_ReturnsSameKey()
    {
        FileMasterKeyProvider sut = CreateSut();

        byte[] firstRead = await sut.GetMasterKeyAsync("v1");
        byte[] secondRead = await sut.GetMasterKeyAsync("v1");

        secondRead.Should().Equal(firstRead);
    }

    [Fact]
    public async Task GetMasterKeyAsync_TwoInstances_SeeSameStoredKey()
    {
        // The whole point of the file-backed provider is that key material
        // survives process restarts. Simulate that by creating two providers
        // pointing at the same directory.
        FileMasterKeyProvider first = CreateSut();
        byte[] firstKey = await first.GetMasterKeyAsync("v1");

        FileMasterKeyProvider second = CreateSut();
        byte[] secondKey = await second.GetMasterKeyAsync("v1");

        secondKey.Should().Equal(firstKey);
    }

    [Fact]
    public async Task GetMasterKeyAsync_WithUnknownKeyId_ThrowsKrypteiaException()
    {
        // Initialize so the directory exists and v1 is written.
        FileMasterKeyProvider sut = CreateSut();
        _ = await sut.GetMasterKeyAsync("v1");

        // Now ask for a key that wasn't generated.
        Func<Task> act = () => sut.GetMasterKeyAsync("does-not-exist");

        await act.Should().ThrowAsync<KrypteiaException>();
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("path/with/slash")]
    [InlineData("path\\with\\backslash")]
    public async Task GetMasterKeyAsync_WithPathTraversalAttempt_ThrowsKrypteiaException(string maliciousKeyId)
    {
        FileMasterKeyProvider sut = CreateSut();

        Func<Task> act = () => sut.GetMasterKeyAsync(maliciousKeyId);

        await act.Should().ThrowAsync<KrypteiaException>();
    }

    [Fact]
    public void Constructor_WithEmptyDirectory_Throws()
    {
        // The discard (`_ =`) signals to the analyzer that we deliberately
        // created an object whose only purpose is to throw from the constructor.
        Action act = () => _ = new FileMasterKeyProvider(new FileMasterKeyProviderOptions
        {
            Directory = string.Empty,
            CurrentKeyId = "v1",
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyCurrentKeyId_Throws()
    {
        Action act = () => _ = new FileMasterKeyProvider(new FileMasterKeyProviderOptions
        {
            Directory = _tempDir,
            CurrentKeyId = string.Empty,
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task CurrentKeyId_ReflectsConfiguredValue()
    {
        FileMasterKeyProvider sut = CreateSut("custom-id");

        sut.CurrentKeyId.Should().Be("custom-id");

        // Verify it actually generates a file under that name.
        _ = await sut.GetMasterKeyAsync("custom-id");
        File.Exists(Path.Combine(_tempDir, "custom-id.key")).Should().BeTrue();
    }

    [Fact]
    public async Task GeneratedKeyFile_OnUnix_HasOwnerOnlyPermissions()
    {
        // Skip on Windows — the test is meaningless there.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        FileMasterKeyProvider sut = CreateSut();
        _ = await sut.GetMasterKeyAsync("v1");

        string keyPath = Path.Combine(_tempDir, "v1.key");
        UnixFileMode mode = File.GetUnixFileMode(keyPath);

        // 0600: owner read/write only, no group, no other.
        mode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}