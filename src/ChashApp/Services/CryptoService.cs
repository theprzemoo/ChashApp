using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChashApp.Models;
using Konscious.Security.Cryptography;

namespace ChashApp.Services;

public sealed class CryptoService
{
    private const string FileHeader = "CHASH1";
    private const string EnvelopeHeader = "CHENV1";
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int IterationsSha256 = 120_000;
    private const int IterationsSha512 = 150_000;
    private const int ScryptIterations = 16_384;
    private const int ScryptBlockSize = 8;
    private const int ScryptParallelism = 1;
    private const int ArgonIterations = 4;
    private const int ArgonMemoryKb = 65_536;
    private const int ArgonParallelism = 4;
    private const int SecureErasePasses = 3;

    public async Task EncryptFilesAsync(
        IReadOnlyList<string> inputFiles,
        string password,
        KdfAlgorithm algorithm,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        if (inputFiles.Count == 0)
        {
            throw new InvalidOperationException("No files selected.");
        }

        for (var index = 0; index < inputFiles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EncryptFileAsync(inputFiles[index], password, algorithm, cancellationToken);
            progress?.Report(((double)index + 1d) / inputFiles.Count);
        }
    }

    public async Task DecryptFilesAsync(
        IReadOnlyList<string> inputFiles,
        string password,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        if (inputFiles.Count == 0)
        {
            throw new InvalidOperationException("No files selected.");
        }

        for (var index = 0; index < inputFiles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DecryptFileAsync(inputFiles[index], password, cancellationToken);
            progress?.Report(((double)index + 1d) / inputFiles.Count);
        }
    }

    public async Task EncryptFileAsync(string inputPath, string password, KdfAlgorithm algorithm, CancellationToken cancellationToken = default)
    {
        EnsurePassword(password);
        EnsureFileExists(inputPath);

        var outputPath = GetEncryptedOutputPath(inputPath);
        var plainBytes = await File.ReadAllBytesAsync(inputPath, cancellationToken);
        var encrypted = EncryptBytes(plainBytes, password, algorithm);
        await File.WriteAllBytesAsync(outputPath, encrypted, cancellationToken);
    }

    public async Task<string> EncryptDirectoryAsSingleFileAsync(string directoryPath, string password, KdfAlgorithm algorithm, CancellationToken cancellationToken = default)
    {
        EnsurePassword(password);
        EnsureDirectoryExists(directoryPath);

        var outputPath = Path.Combine(
            Path.GetDirectoryName(directoryPath) ?? Environment.CurrentDirectory,
            $"{Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}.chash");

        var payload = await CreateFolderPayloadAsync(directoryPath, cancellationToken);
        var encrypted = EncryptBytes(payload, password, algorithm);
        await File.WriteAllBytesAsync(outputPath, encrypted, cancellationToken);
        return outputPath;
    }

    public async Task<string> DecryptFileAsync(string inputPath, string password, CancellationToken cancellationToken = default)
    {
        EnsurePassword(password);
        EnsureFileExists(inputPath);

        var encryptedBytes = await File.ReadAllBytesAsync(inputPath, cancellationToken);
        var plainBytes = DecryptBytes(encryptedBytes, password);

        if (TryReadEnvelope(plainBytes, out var header, out var payloadBytes) &&
            string.Equals(header?.Kind, "folder", StringComparison.OrdinalIgnoreCase))
        {
            var directoryPath = BuildExtractedDirectoryPath(inputPath, header!.Name);
            Directory.CreateDirectory(directoryPath);
            await ExtractZipPayloadAsync(payloadBytes, directoryPath, cancellationToken);
            return directoryPath;
        }

        var outputPath = BuildDecryptedFilePath(inputPath);
        await File.WriteAllBytesAsync(outputPath, plainBytes, cancellationToken);
        return outputPath;
    }

    public string GetEncryptedOutputPath(string inputPath) => $"{inputPath}.chash";

    public string GetPredictedDecryptedOutputPath(string inputPath) => BuildDecryptedFilePath(inputPath);

    public NoteCipherResult EncryptNote(string plainText, string password, KdfAlgorithm algorithm)
    {
        EnsurePassword(password);
        if (string.IsNullOrWhiteSpace(plainText))
        {
            throw new InvalidOperationException("Note cannot be empty.");
        }

        var encrypted = EncryptBytes(Encoding.UTF8.GetBytes(plainText), password, algorithm);
        return new NoteCipherResult
        {
            PlainText = plainText,
            CipherText = Convert.ToBase64String(encrypted)
        };
    }

    public NoteCipherResult DecryptNote(string cipherText, string password)
    {
        EnsurePassword(password);
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            throw new InvalidOperationException("Encrypted note cannot be empty.");
        }

        try
        {
            var data = Convert.FromBase64String(cipherText);
            var plainBytes = DecryptBytes(data, password);
            return new NoteCipherResult
            {
                CipherText = cipherText,
                PlainText = Encoding.UTF8.GetString(plainBytes)
            };
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Encrypted note must be valid Base64.", exception);
        }
    }

    private static byte[] EncryptBytes(byte[] plainBytes, string password, KdfAlgorithm algorithm)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var config = GetKdfConfig(algorithm);
        var key = DeriveKey(password, salt, algorithm, config);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes(FileHeader));
        writer.Write((int)algorithm);
        writer.Write(config.Iterations);
        writer.Write(config.MemoryOrCost);
        writer.Write(config.Parallelism);
        writer.Write(salt.Length);
        writer.Write(salt);
        writer.Write(nonce.Length);
        writer.Write(nonce);
        writer.Write(tag.Length);
        writer.Write(tag);
        writer.Write(cipherBytes.Length);
        writer.Write(cipherBytes);
        writer.Flush();

        return stream.ToArray();
    }

    private static byte[] DecryptBytes(byte[] encryptedBytes, string password)
    {
        using var stream = new MemoryStream(encryptedBytes);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var header = Encoding.ASCII.GetString(reader.ReadBytes(FileHeader.Length));
        if (!string.Equals(header, FileHeader, StringComparison.Ordinal))
        {
            throw new CryptographicException("Unsupported or corrupted file format.");
        }

        var algorithm = (KdfAlgorithm)reader.ReadInt32();
        var config = GetKdfConfig(
            algorithm,
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32());
        var salt = reader.ReadBytes(reader.ReadInt32());
        var nonce = reader.ReadBytes(reader.ReadInt32());
        var tag = reader.ReadBytes(reader.ReadInt32());
        var cipherBytes = reader.ReadBytes(reader.ReadInt32());
        var key = DeriveKey(password, salt, algorithm, config);
        var plainBytes = new byte[cipherBytes.Length];

        try
        {
            using var aes = new AesGcm(key, tag.Length);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            return plainBytes;
        }
        catch (CryptographicException exception)
        {
            throw new CryptographicException("Decryption failed. The password is incorrect or the data was modified.", exception);
        }
    }

    private static async Task<byte[]> CreateFolderPayloadAsync(string directoryPath, CancellationToken cancellationToken)
    {
        await using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(directoryPath, file);
                var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                await source.CopyToAsync(entryStream, cancellationToken);
            }
        }

        var header = new EnvelopeHeaderModel("folder", Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(header);
        var zipBytes = zipStream.ToArray();

        await using var payloadStream = new MemoryStream();
        await using var writer = new BinaryWriter(payloadStream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes(EnvelopeHeader));
        writer.Write(headerBytes.Length);
        writer.Write(headerBytes);
        writer.Write(zipBytes.Length);
        writer.Write(zipBytes);
        writer.Flush();
        return payloadStream.ToArray();
    }

    private static bool TryReadEnvelope(byte[] plainBytes, out EnvelopeHeaderModel? header, out byte[] payload)
    {
        header = null;
        payload = Array.Empty<byte>();

        try
        {
            using var stream = new MemoryStream(plainBytes);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var envelopeHeader = Encoding.ASCII.GetString(reader.ReadBytes(EnvelopeHeader.Length));
            if (!string.Equals(envelopeHeader, EnvelopeHeader, StringComparison.Ordinal))
            {
                return false;
            }

            var headerLength = reader.ReadInt32();
            var headerBytes = reader.ReadBytes(headerLength);
            var payloadLength = reader.ReadInt32();
            payload = reader.ReadBytes(payloadLength);
            header = JsonSerializer.Deserialize<EnvelopeHeaderModel>(headerBytes);
            return header is not null;
        }
        catch
        {
            header = null;
            payload = Array.Empty<byte>();
            return false;
        }
    }

    private static async Task ExtractZipPayloadAsync(byte[] payloadBytes, string destinationDirectory, CancellationToken cancellationToken)
    {
        await using var payloadStream = new MemoryStream(payloadBytes);
        using var archive = new ZipArchive(payloadStream, ZipArchiveMode.Read, leaveOpen: false);

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetPath = Path.Combine(destinationDirectory, entry.FullName);
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            await using var entryStream = entry.Open();
            await using var targetStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await entryStream.CopyToAsync(targetStream, cancellationToken);
        }
    }

    private static string BuildDecryptedFilePath(string inputPath)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var originalFileName = inputPath.EndsWith(".chash", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(inputPath)
            : Path.GetFileNameWithoutExtension(inputPath);

        if (inputPath.EndsWith(".chash", StringComparison.OrdinalIgnoreCase))
        {
            var originalPath = inputPath[..^6];
            if (!File.Exists(originalPath))
            {
                return originalPath;
            }

            var originalExtension = Path.GetExtension(originalPath);
            var originalName = Path.GetFileNameWithoutExtension(originalPath);
            return Path.Combine(directory, $"{originalName}.decrypted{originalExtension}");
        }

        var fileName = originalFileName;
        var extension = Path.GetExtension(inputPath);
        return Path.Combine(directory, $"{fileName}.decrypted{extension}");
    }

    private static string BuildExtractedDirectoryPath(string inputPath, string suggestedName)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var baseName = string.IsNullOrWhiteSpace(suggestedName)
            ? Path.GetFileNameWithoutExtension(inputPath)
            : suggestedName;
        var targetPath = Path.Combine(directory, baseName);
        if (!Directory.Exists(targetPath))
        {
            return targetPath;
        }

        var suffix = 1;
        while (Directory.Exists($"{targetPath}-decrypted-{suffix}"))
        {
            suffix++;
        }

        return $"{targetPath}-decrypted-{suffix}";
    }

    private static void EnsureFileExists(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The selected file does not exist.", path);
        }
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"The selected folder does not exist: {path}");
        }
    }

    private static void EnsurePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new InvalidOperationException("Password must contain at least 8 characters.");
        }
    }

    public PasswordStrengthInfo EvaluatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return new PasswordStrengthInfo("veryWeak", 8, "#F87171");
        }

        var score = 0;
        if (password.Length >= 8) score += 24;
        if (password.Length >= 12) score += 20;
        if (password.Any(char.IsLower)) score += 12;
        if (password.Any(char.IsUpper)) score += 12;
        if (password.Any(char.IsDigit)) score += 12;
        if (password.Any(ch => !char.IsLetterOrDigit(ch))) score += 20;

        return score switch
        {
            < 30 => new PasswordStrengthInfo("veryWeak", score, "#F87171"),
            < 50 => new PasswordStrengthInfo("weak", score, "#FB923C"),
            < 70 => new PasswordStrengthInfo("moderate", score, "#FACC15"),
            < 90 => new PasswordStrengthInfo("strong", score, "#4ADE80"),
            _ => new PasswordStrengthInfo("excellent", score, "#2DF0D0")
        };
    }

    public bool VerifyEncryptedPayload(string inputPath, string password)
    {
        EnsurePassword(password);
        EnsureFileExists(inputPath);
        var encryptedBytes = File.ReadAllBytes(inputPath);
        _ = DecryptBytes(encryptedBytes, password);
        return true;
    }

    public async Task SecureDeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureFileExists(path);
        var fileInfo = new FileInfo(path);
        var fileLength = fileInfo.Length;
        var buffer = new byte[81920];

        for (var pass = 0; pass < SecureErasePasses; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
            stream.Seek(0, SeekOrigin.Begin);
            var remaining = fileLength;

            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RandomNumberGenerator.Fill(buffer);
                var toWrite = (int)Math.Min(buffer.Length, remaining);
                await stream.WriteAsync(buffer.AsMemory(0, toWrite), cancellationToken);
                remaining -= toWrite;
            }

            await stream.FlushAsync(cancellationToken);
        }

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        File.SetCreationTimeUtc(path, DateTime.UtcNow);
        File.Delete(path);
    }

    private static byte[] DeriveKey(string password, byte[] salt, KdfAlgorithm algorithm, KdfConfig config)
    {
        return algorithm switch
        {
            KdfAlgorithm.Pbkdf2Sha512 => Rfc2898DeriveBytes.Pbkdf2(password, salt, config.Iterations, config.HashAlgorithm, 32),
            KdfAlgorithm.ScryptSha256 => Rfc2898DeriveBytes.Pbkdf2(password, salt, config.MemoryOrCost, HashAlgorithmName.SHA256, 32),
            KdfAlgorithm.Argon2Id => DeriveArgon2(password, salt, config),
            _ => Rfc2898DeriveBytes.Pbkdf2(password, salt, config.Iterations, config.HashAlgorithm, 32)
        };
    }

    private static byte[] DeriveArgon2(string password, byte[] salt, KdfConfig config)
    {
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = config.Parallelism,
            Iterations = config.Iterations,
            MemorySize = config.MemoryOrCost
        };

        return argon2.GetBytes(32);
    }

    private static KdfConfig GetKdfConfig(KdfAlgorithm algorithm, int? iterationsOverride = null, int? memoryOrCostOverride = null, int? parallelismOverride = null)
    {
        return algorithm switch
        {
            KdfAlgorithm.Pbkdf2Sha512 => new KdfConfig(HashAlgorithmName.SHA512, iterationsOverride ?? IterationsSha512, 0, 0),
            KdfAlgorithm.ScryptSha256 => new KdfConfig(HashAlgorithmName.SHA256, iterationsOverride ?? ScryptBlockSize, memoryOrCostOverride ?? ScryptIterations, parallelismOverride ?? ScryptParallelism),
            KdfAlgorithm.Argon2Id => new KdfConfig(HashAlgorithmName.SHA256, iterationsOverride ?? ArgonIterations, memoryOrCostOverride ?? ArgonMemoryKb, parallelismOverride ?? ArgonParallelism),
            _ => new KdfConfig(HashAlgorithmName.SHA256, iterationsOverride ?? IterationsSha256, 0, 0)
        };
    }

    private sealed record KdfConfig(HashAlgorithmName HashAlgorithm, int Iterations, int MemoryOrCost, int Parallelism);
    private sealed record EnvelopeHeaderModel(string Kind, string Name);
}
