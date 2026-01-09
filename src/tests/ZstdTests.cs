using System.Diagnostics;
using Xunit.Abstractions;

namespace zstd.net.tests;

public class ZstdTests
{
    [Fact]
    public void VersionString()
    {
        var version = Zstd.VersionString();
        Assert.Equal("1.6.0", version);
    }

    // Base class for data-specific test groups
    public class DataTest
    {
        private const string ZstdCliPath = "/opt/homebrew/bin/zstd";

        private readonly byte[] data;
        private readonly ITestOutputHelper output;

        public DataTest(ITestOutputHelper output)
        {
            this.output = output;
            data = GenRandomData(100);
        }

        public static IEnumerable<object[]> OneShotTestParam
        {
            get
            {
                var lst = new List<object[]>();
                foreach (var size in new[] { 0, 1, 2, 3, 100, 1 << 10, 1 << 20, 1 << 24, 1 << 26 })
                {
                    for (var level = 1; level <= 19; level++)
                    {
                        lst.Add([size, level]);
                    }
                }

                return lst;
            }
        }

        public static IEnumerable<object[]> StreamTestParam
        {
            get
            {
                var lst = new List<object[]>();
                foreach (var size in new[] { 0, 1, 2, 3, 100, 1 << 10, 1 << 26 })
                {
                    foreach (var level in new[] { 1, 3, 5, 9, 13, 19 })
                    {
                        foreach (var nworkers in new[] { 0, 1, 4, 16 })
                        {
                            lst.Add([size, level, nworkers]);
                        }
                    }
                }

                return lst;
            }
        }


        [Theory]
        [MemberData(nameof(OneShotTestParam))]
        public void Test_OneShot(int size, int level)
        {
            TestOneShot(GenRandomData(size), level);
        }

        [Theory]
        [MemberData(nameof(StreamTestParam))]
        public void Test_MyStream(int size, int level, int nworkers)
        {
            TestStreamCompressDecompress(GenRandomData(size), level, nworkers);
        }

        [Fact]
        public void Test_StreamCompress_CliDecompress()
        {
            TestCompressWithStreamDecompressWithCli(data);
        }

        [Fact]
        public void Test_CliCompress_StreamDecompress()
        {
            TestCompressWithCliDecompressWithStream(data);
        }

        private byte[] GenRandomData(int size)
        {
            // generate some data
            var mem = new MemoryStream();
            using var writer = new StreamWriter(mem);
            long n = 0;
            while (true)
            {
                var b64 = Convert.ToBase64String(BitConverter.GetBytes(n));
                writer.WriteLine(b64);
                n++;
                if (mem.Position >= size)
                    break;
            }

            var _data = new byte[size];
            Array.Copy(mem.ToArray(), _data, size);
            return _data;
        }

        private static string WriteDataToFile(byte[] originalData)
        {
            var fname = Path.GetTempFileName();
            var fs = File.OpenWrite(fname);
            fs.Write(originalData, 0, originalData.Length);
            fs.Close();
            return fname;
        }

        // Helper: Compress with Zstd.CompressBytes, decompress with Zstd.DecompressBytes
        private static void TestOneShot(byte[] originalData, int level)
        {
            var compressed = Zstd.CompressBytes(originalData, level);
            var decompressed = Zstd.DecompressBytes(compressed);

            Assert.NotNull(compressed);
            Assert.NotNull(decompressed);
            Assert.Equal(originalData, decompressed);
        }

        // Helper: Compress with ZstdCompressStream, decompress with ZstdDecompressStream

        private void TestStreamCompressDecompress(byte[] data, int level, int nworkers)
        {
            Assert.NotNull(data);

            var dst_fname = Path.GetTempFileName();
            using (var out_fs = File.OpenWrite(dst_fname))
            using (var zstdStream = new ZstdCompressStream(out_fs, 1024 * 16, false, level, nworkers))
            {
                zstdStream.Write(data, 0, data.Length);
                // out_fs.Flush();
            }

            byte[] decompressed;
            using (var fs = File.OpenRead(dst_fname))
            using (var zstdStream = new ZstdDecompressStream(fs))
            using (var decompressedStream = new MemoryStream())
            {
                zstdStream.CopyTo(decompressedStream);
                decompressed = decompressedStream.ToArray();
            }

            Assert.NotNull(decompressed);
            Assert.Equal(data, decompressed);
        }

        // Helper: Compress with ZstdCompressStream, decompress with zstd CLI
        private static void TestCompressWithStreamDecompressWithCli(byte[] originalData)
        {
            string tempCompressed = Path.GetTempFileName();
            string tempDecompressed = Path.GetTempFileName();

            try
            {
                // Compress with our library
                using (var fileStream = File.Create(tempCompressed))
                {
                    using (var zstdStream = new ZstdCompressStream(fileStream, leaveOpen: false))
                    {
                        zstdStream.Write(originalData, 0, originalData.Length);
                    }

                    // fileStream.Flush();
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
        private static void TestCompressWithCliDecompressWithStream(byte[] originalData)
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
}