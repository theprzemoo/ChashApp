using ChashApp.Models;

namespace ChashApp.Services;

public sealed class CliRunner
{
    private readonly CryptoService _cryptoService;

    public CliRunner(CryptoService cryptoService)
    {
        _cryptoService = cryptoService;
    }

    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return 1;
        }

        var command = args[0].ToLowerInvariant();
        var path = args.ElementAtOrDefault(1);
        var password = args.ElementAtOrDefault(2);
        var kdf = ParseKdf(args.ElementAtOrDefault(3));

        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("Usage: chashapp <encrypt|decrypt|verify> <path> <password> [kdf]");
            return 2;
        }

        switch (command)
        {
            case "encrypt":
                await _cryptoService.EncryptFileAsync(path, password, kdf);
                Console.WriteLine($"Encrypted: {path}.chash");
                return 0;
            case "decrypt":
                var output = await _cryptoService.DecryptFileAsync(path, password);
                Console.WriteLine($"Decrypted: {output}");
                return 0;
            case "verify":
                _cryptoService.VerifyEncryptedPayload(path, password);
                Console.WriteLine("Integrity check passed.");
                return 0;
            default:
                Console.WriteLine("Unknown command.");
                return 3;
        }
    }

    private static KdfAlgorithm ParseKdf(string? raw)
    {
        return raw?.ToLowerInvariant() switch
        {
            "pbkdf2-sha512" => KdfAlgorithm.Pbkdf2Sha512,
            "scrypt" => KdfAlgorithm.ScryptSha256,
            "argon2id" => KdfAlgorithm.Argon2Id,
            _ => KdfAlgorithm.Pbkdf2Sha256
        };
    }
}
