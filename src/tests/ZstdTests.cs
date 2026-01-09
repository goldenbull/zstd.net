using System.Diagnostics;

namespace zstd.net.tests;

public class ZstdTests
{
    private const string ZstdCliPath = "/opt/homebrew/bin/zstd";

    [Fact]
    public void VersionString_ReturnsNonEmptyString()
    {
        var version = Zstd.VersionString();
        Assert.NotNull(version);
        Assert.NotEmpty(version);
        Assert.Equal("1.6.0", version);
    }

    [Fact]
    public void CompressAndDecompress_EmptyData_WithBytes()
    {
        byte[] originalData = Array.Empty<byte>();
        TestCompressDecompressWithBytes(originalData);
    }

    [Fact]
    public void CompressAndDecompress_EmptyData_WithStream()
    {
        byte[] originalData = Array.Empty<byte>();
        TestCompressDecompressWithStream(originalData);
    }

    [Fact]
    public void CompressWithStream_DecompressWithCli_EmptyData()
    {
        byte[] originalData = Array.Empty<byte>();
        TestCompressWithStreamDecompressWithCli(originalData);
    }

    [Fact]
    public void CompressWithCli_DecompressWithStream_EmptyData()
    {
        byte[] originalData = Array.Empty<byte>();
        TestCompressWithCliDecompressWithStream(originalData);
    }

    // Helper: Compress with Zstd.CompressBytes, decompress with Zstd.DecompressBytes
    private void TestCompressDecompressWithBytes(byte[] originalData)
    {
        byte[] compressed = Zstd.CompressBytes(originalData);
        byte[] decompressed = Zstd.DecompressBytes(compressed);

        Assert.NotNull(compressed);
        Assert.NotNull(decompressed);
        Assert.Equal(originalData, decompressed);
    }

    // Helper: Compress with ZstdCompressStream, decompress with ZstdDecompressStream
    private void TestCompressDecompressWithStream(byte[] originalData)
    {
        byte[] compressed;
        using (var compressedStream = new MemoryStream())
        {
            using (var zstdStream = new ZstdCompressStream(compressedStream, leaveOpen: true))
            {
                zstdStream.Write(originalData, 0, originalData.Length);
            }
            compressed = compressedStream.ToArray();
        }

        byte[] decompressed;
        using (var compressedStream = new MemoryStream(compressed))
        using (var zstdStream = new ZstdDecompressStream(compressedStream))
        using (var decompressedStream = new MemoryStream())
        {
            zstdStream.CopyTo(decompressedStream);
            decompressed = decompressedStream.ToArray();
        }

        Assert.NotNull(compressed);
        Assert.NotNull(decompressed);
        Assert.Equal(originalData, decompressed);
    }

    // Helper: Compress with ZstdCompressStream, decompress with zstd CLI
    private void TestCompressWithStreamDecompressWithCli(byte[] originalData)
    {
        string tempCompressed = Path.GetTempFileName();
        string tempDecompressed = Path.GetTempFileName();

        try
        {
            // Compress with our library
            using (var fileStream = File.Create(tempCompressed))
            {
                using (var zstdStream = new ZstdCompressStream(fileStream, leaveOpen: true))
                {
                    zstdStream.Write(originalData, 0, originalData.Length);
                }
                // Ensure file is flushed to disk
                fileStream.Flush();
            }

            // Decompress with zstd CLI
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = ZstdCliPath,
                Arguments = $"-d \"{tempCompressed}\" -o \"{tempDecompressed}\" -f",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            process?.WaitForExit();

            Assert.True(process?.ExitCode == 0, $"zstd CLI failed: {process?.StandardError.ReadToEnd()}");

            byte[] decompressed = File.ReadAllBytes(tempDecompressed);
            Assert.Equal(originalData, decompressed);
        }
        finally
        {
            if (File.Exists(tempCompressed)) File.Delete(tempCompressed);
            if (File.Exists(tempDecompressed)) File.Delete(tempDecompressed);
        }
    }

    // Helper: Compress with zstd CLI, decompress with ZstdDecompressStream
    private void TestCompressWithCliDecompressWithStream(byte[] originalData)
    {
        string tempOriginal = Path.GetTempFileName();
        string tempCompressed = Path.GetTempFileName();

        try
        {
            // Write original data
            File.WriteAllBytes(tempOriginal, originalData);

            // Compress with zstd CLI
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = ZstdCliPath,
                Arguments = $"\"{tempOriginal}\" -o \"{tempCompressed}\" -f",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            process?.WaitForExit();

            Assert.True(process?.ExitCode == 0, $"zstd CLI failed: {process?.StandardError.ReadToEnd()}");

            // Decompress with our library
            byte[] decompressed;
            using (var fileStream = File.OpenRead(tempCompressed))
            using (var zstdStream = new ZstdDecompressStream(fileStream))
            using (var decompressedStream = new MemoryStream())
            {
                zstdStream.CopyTo(decompressedStream);
                decompressed = decompressedStream.ToArray();
            }

            Assert.Equal(originalData, decompressed);
        }
        finally
        {
            if (File.Exists(tempOriginal)) File.Delete(tempOriginal);
            if (File.Exists(tempCompressed)) File.Delete(tempCompressed);
        }
    }
}