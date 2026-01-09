using System.Runtime.InteropServices;

namespace zstd.net;

public static class Zstd
{
    /***************************************
     *  some helper functions, converting char* to string
     ***************************************/

    public static string VersionString()
    {
        return Marshal.PtrToStringAnsi(ZstdNative.ZSTD_versionString()) ?? "unknown";
    }

    public static string GetErrorName(nuint result)
    {
        return Marshal.PtrToStringAnsi(ZstdNative.ZSTD_getErrorName(result)) ?? "unknown error";
    }

    public static string GetErrorString(ZSTD_ErrorCode code)
    {
        return Marshal.PtrToStringAnsi(ZstdNative.ZSTD_getErrorString(code)) ?? "unknown error";
    }

    /***************************************
     *  Simple compression/decompression
     ***************************************/

    public static byte[] CompressBytes(byte[] data, int compressionLevel = ZstdNative.ZSTD_CLEVEL_DEFAULT)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        unsafe
        {
            // Calculate maximum compressed size
            nuint maxCompressedSize = ZstdNative.ZSTD_compressBound((nuint)data.Length);
            byte[] compressed = new byte[maxCompressedSize];

            fixed (byte* srcPtr = data)
            fixed (byte* dstPtr = compressed)
            {
                nuint compressedSize = ZstdNative.ZSTD_compress(
                    dstPtr,
                    maxCompressedSize,
                    srcPtr,
                    (nuint)data.Length,
                    compressionLevel);

                if (ZstdNative.ZSTD_isError(compressedSize) != 0)
                {
                    var errorCode = ZstdNative.ZSTD_getErrorCode(compressedSize);
                    throw new InvalidOperationException($"Compression failed: {GetErrorString(errorCode)}");
                }

                // Resize to actual compressed size
                Array.Resize(ref compressed, (int)compressedSize);
                return compressed;
            }
        }
    }

    public static byte[] DecompressBytes(byte[] compressedData)
    {
        if (compressedData == null)
            throw new ArgumentNullException(nameof(compressedData));

        unsafe
        {
            fixed (byte* srcPtr = compressedData)
            {
                // Get the decompressed size from the frame header
                ulong decompressedSize = ZstdNative.ZSTD_getFrameContentSize(srcPtr, (nuint)compressedData.Length);

                if (decompressedSize == ZstdNative.ZSTD_CONTENTSIZE_UNKNOWN)
                    throw new InvalidOperationException("Decompressed size is unknown");

                if (decompressedSize == ZstdNative.ZSTD_CONTENTSIZE_ERROR)
                    throw new InvalidOperationException("Invalid compressed data");

                byte[] decompressed = new byte[decompressedSize];

                fixed (byte* dstPtr = decompressed)
                {
                    nuint result = ZstdNative.ZSTD_decompress(
                        dstPtr,
                        (nuint)decompressedSize,
                        srcPtr,
                        (nuint)compressedData.Length);

                    if (ZstdNative.ZSTD_isError(result) != 0)
                    {
                        var errorCode = ZstdNative.ZSTD_getErrorCode(result);
                        throw new InvalidOperationException($"Decompression failed: {GetErrorString(errorCode)}");
                    }

                    return decompressed;
                }
            }
        }
    }
}