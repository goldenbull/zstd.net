using zstd.net;

namespace examples;

static class Program
{
    static void Compress1()
    {
        var src_fname = "bigdata.bin";
        var src_fs = File.OpenRead(src_fname);

        var dst_fname = "compressed-9.zst";
        using var out_fs = File.OpenWrite(dst_fname);

        using var zstdStream = new ZstdCompressStream(out_fs, 1024 * 16, false, 9, 16);
        src_fs.CopyTo(out_fs);
    }

    static void Decompress1()
    {
        var src_fname = "compressed-9.zst";
        var src_fs = File.OpenRead(src_fname);

        var dst_fname = "compressed-9.bin";
        using var out_fs = File.OpenWrite(dst_fname);

        using var zstdStream = new ZstdDecompressStream(src_fs);
        zstdStream.CopyTo(out_fs);
    }

    static void Main(string[] args)
    {
        // Compress1();
        Decompress1();
    }
}