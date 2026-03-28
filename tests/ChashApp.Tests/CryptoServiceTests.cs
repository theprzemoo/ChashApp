using System.Security.Cryptography;
using ChashApp.Models;
using ChashApp.Services;
using Xunit;

namespace ChashApp.Tests;

public sealed class CryptoServiceTests
{
    private readonly CryptoService _cryptoService = new();
    private const string Password = "Sup3r!Secure#Password";

    [Theory]
    [InlineData(KdfAlgorithm.Pbkdf2Sha256)]
    [InlineData(KdfAlgorithm.Pbkdf2Sha512)]
    [InlineData(KdfAlgorithm.ScryptSha256)]
    [InlineData(KdfAlgorithm.Argon2Id)]
    public void EncryptNote_ThenDecryptNote_RoundTrips(KdfAlgorithm algorithm)
    {
        var plainText = $"secret note for {algorithm}";

        var encrypted = _cryptoService.EncryptNote(plainText, Password, algorithm);
        var decrypted = _cryptoService.DecryptNote(encrypted.CipherText, Password);

        Assert.Equal(plainText, decrypted.PlainText);
    }

    [Theory]
    [InlineData(KdfAlgorithm.Pbkdf2Sha256)]
    [InlineData(KdfAlgorithm.Pbkdf2Sha512)]
    [InlineData(KdfAlgorithm.ScryptSha256)]
    [InlineData(KdfAlgorithm.Argon2Id)]
    public async Task EncryptFile_ThenDecryptFile_RoundTrips(KdfAlgorithm algorithm)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ChashApp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var inputPath = Path.Combine(directory, "sample.txt");
        await File.WriteAllTextAsync(inputPath, $"payload {algorithm}");

        await _cryptoService.EncryptFileAsync(inputPath, Password, algorithm);
        var decryptedPath = await _cryptoService.DecryptFileAsync($"{inputPath}.chash", Password);

        var decryptedContent = await File.ReadAllTextAsync(decryptedPath);
        Assert.Equal($"payload {algorithm}", decryptedContent);
    }

    [Fact]
    public void DecryptNote_WithWrongPassword_Throws()
    {
        var encrypted = _cryptoService.EncryptNote("private", Password, KdfAlgorithm.Argon2Id);

        Assert.Throws<CryptographicException>(() => _cryptoService.DecryptNote(encrypted.CipherText, "wrong-pass-123"));
    }

    [Fact]
    public void EvaluatePassword_ReturnsExcellentForComplexPassword()
    {
        var result = _cryptoService.EvaluatePassword("Sup3r!Secure#Password");

        Assert.Equal("excellent", result.Key);
        Assert.True(result.Score >= 90);
    }
}
