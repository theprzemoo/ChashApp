using BenchmarkDotNet.Attributes;
using ChashApp.Models;
using ChashApp.Services;

namespace ChashApp.Benchmarks;

[MemoryDiagnoser]
public class CryptoBenchmarks
{
    private readonly CryptoService _cryptoService = new();
    private string _plainText = string.Empty;
    private const string Password = "Sup3r!Secure#Password";

    [GlobalSetup]
    public void Setup()
    {
        _plainText = new string('A', 32_000);
    }

    [Benchmark]
    public string Encrypt_Pbkdf2Sha256()
        => _cryptoService.EncryptNote(_plainText, Password, KdfAlgorithm.Pbkdf2Sha256).CipherText;

    [Benchmark]
    public string Encrypt_Scrypt()
        => _cryptoService.EncryptNote(_plainText, Password, KdfAlgorithm.ScryptSha256).CipherText;

    [Benchmark]
    public string Encrypt_Argon2Id()
        => _cryptoService.EncryptNote(_plainText, Password, KdfAlgorithm.Argon2Id).CipherText;
}
