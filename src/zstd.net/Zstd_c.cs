using System.Runtime.InteropServices;

namespace zstd.net;

/***************************************
 *  Enums
 ***************************************/

/* Compression strategies, listed from fastest to strongest */
public enum ZSTD_strategy
{
    ZSTD_fast = 1,
    ZSTD_dfast = 2,
    ZSTD_greedy = 3,
    ZSTD_lazy = 4,
    ZSTD_lazy2 = 5,
    ZSTD_btlazy2 = 6,
    ZSTD_btopt = 7,
    ZSTD_btultra = 8,

    ZSTD_btultra2 = 9
    /* note : new strategies _might_ be added in the future.
               Only the order (from fast to strong) is guaranteed */
}

/* Compression parameters */
public enum ZSTD_cParameter
{
    /* compression parameters */
    ZSTD_c_compressionLevel = 100,
    ZSTD_c_windowLog = 101,
    ZSTD_c_hashLog = 102,
    ZSTD_c_chainLog = 103,
    ZSTD_c_searchLog = 104,
    ZSTD_c_minMatch = 105,
    ZSTD_c_targetLength = 106,
    ZSTD_c_strategy = 107,

    ZSTD_c_targetCBlockSize = 130,

    /* LDM mode parameters */
    ZSTD_c_enableLongDistanceMatching = 160,
    ZSTD_c_ldmHashLog = 161,
    ZSTD_c_ldmMinMatch = 162,
    ZSTD_c_ldmBucketSizeLog = 163,
    ZSTD_c_ldmHashRateLog = 164,

    /* frame parameters */
    ZSTD_c_contentSizeFlag = 200,
    ZSTD_c_checksumFlag = 201,
    ZSTD_c_dictIDFlag = 202,

    /* multi-threading parameters */
    ZSTD_c_nbWorkers = 400,
    ZSTD_c_jobSize = 401,
    ZSTD_c_overlapLog = 402,

    /* experimental parameters - DO NOT USE */
    ZSTD_c_experimentalParam1 = 500,
    ZSTD_c_experimentalParam2 = 10,
    ZSTD_c_experimentalParam3 = 1000,
    ZSTD_c_experimentalParam4 = 1001,
    ZSTD_c_experimentalParam5 = 1002,
    ZSTD_c_experimentalParam7 = 1004,
    ZSTD_c_experimentalParam8 = 1005,
    ZSTD_c_experimentalParam9 = 1006,
    ZSTD_c_experimentalParam10 = 1007,
    ZSTD_c_experimentalParam11 = 1008,
    ZSTD_c_experimentalParam12 = 1009,
    ZSTD_c_experimentalParam13 = 1010,
    ZSTD_c_experimentalParam14 = 1011,
    ZSTD_c_experimentalParam15 = 1012,
    ZSTD_c_experimentalParam16 = 1013,
    ZSTD_c_experimentalParam17 = 1014,
    ZSTD_c_experimentalParam18 = 1015,
    ZSTD_c_experimentalParam19 = 1016,
    ZSTD_c_experimentalParam20 = 1017
}

public enum ZSTD_ResetDirective
{
    ZSTD_reset_session_only = 1,
    ZSTD_reset_parameters = 2,
    ZSTD_reset_session_and_parameters = 3
}

/* Decompression parameters */
public enum ZSTD_dParameter
{
    ZSTD_d_windowLogMax = 100,

    /* experimental parameters - DO NOT USE */
    ZSTD_d_experimentalParam1 = 1000,
    ZSTD_d_experimentalParam2 = 1001,
    ZSTD_d_experimentalParam3 = 1002,
    ZSTD_d_experimentalParam4 = 1003,
    ZSTD_d_experimentalParam5 = 1004,
    ZSTD_d_experimentalParam6 = 1005
}

/* Streaming end directive */
public enum ZSTD_EndDirective
{
    ZSTD_e_continue = 0,
    ZSTD_e_flush = 1,
    ZSTD_e_end = 2
}

/***************************************
 *  Structs
 ***************************************/

[StructLayout(LayoutKind.Sequential)]
public struct ZSTD_bounds
{
    public nuint error;
    public int lowerBound;
    public int upperBound;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ZSTD_inBuffer
{
    public void* src;
    public nuint size;
    public nuint pos;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ZSTD_outBuffer
{
    public void* dst;
    public nuint size;
    public nuint pos;
}

/***************************************
 *  Opaque Types (represented as IntPtr)
 ***************************************/
// ZSTD_CCtx - Compression context
// ZSTD_DCtx - Decompression context
// ZSTD_CDict - Compression dictionary
// ZSTD_DDict - Decompression dictionary
// ZSTD_CStream - Compression stream (same as ZSTD_CCtx >= v1.3.0)
// ZSTD_DStream - Decompression stream (same as ZSTD_DCtx >= v1.3.0)
/***************************************
 *  Native P/Invoke Methods
 ***************************************/

internal static unsafe partial class ZstdNative
{
    private const string LibraryName = "zstd";

    // ===== Constants =====
    public const int ZSTD_VERSION_MAJOR = 1;
    public const int ZSTD_VERSION_MINOR = 6;
    public const int ZSTD_VERSION_RELEASE = 0;

    public const int ZSTD_VERSION_NUMBER =
        (ZSTD_VERSION_MAJOR * 100 * 100 + ZSTD_VERSION_MINOR * 100 + ZSTD_VERSION_RELEASE);

    public const int ZSTD_CLEVEL_DEFAULT = 3;

    public const uint ZSTD_MAGICNUMBER = 0xFD2FB528;
    public const uint ZSTD_MAGIC_DICTIONARY = 0xEC30A437;
    public const uint ZSTD_MAGIC_SKIPPABLE_START = 0x184D2A50;
    public const uint ZSTD_MAGIC_SKIPPABLE_MASK = 0xFFFFFFF0;

    public const int ZSTD_BLOCKSIZELOG_MAX = 17;
    public const int ZSTD_BLOCKSIZE_MAX = (1 << ZSTD_BLOCKSIZELOG_MAX);

    public const ulong ZSTD_CONTENTSIZE_UNKNOWN = unchecked(0UL - 1);
    public const ulong ZSTD_CONTENTSIZE_ERROR = unchecked(0UL - 2);

    // ===== Version Functions =====

    [LibraryImport(LibraryName)]
    internal static partial uint ZSTD_versionNumber();

    [LibraryImport(LibraryName)]
    internal static partial IntPtr ZSTD_versionString();

    // ===== Simple Core API =====

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_compress(void* dst, nuint dstCapacity, void* src, nuint srcSize, int compressionLevel);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_decompress(void* dst, nuint dstCapacity, void* src, nuint compressedSize);

    // ===== Decompression helper functions =====

    [LibraryImport(LibraryName)]
    internal static partial ulong ZSTD_getFrameContentSize(void* src, nuint srcSize);

    [LibraryImport(LibraryName)]
    internal static partial ulong ZSTD_getDecompressedSize(void* src, nuint srcSize);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_findFrameCompressedSize(void* src, nuint srcSize);

    // ===== Compression helper functions =====

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_compressBound(nuint srcSize);

    // ===== Error helper functions =====

    [LibraryImport(LibraryName)]
    internal static partial uint ZSTD_isError(nuint result);

    [LibraryImport(LibraryName)]
    internal static partial ZSTD_ErrorCode ZSTD_getErrorCode(nuint functionResult);

    [LibraryImport(LibraryName)]
    internal static partial IntPtr ZSTD_getErrorName(nuint result);

    [LibraryImport(LibraryName)]
    internal static partial int ZSTD_minCLevel();

    [LibraryImport(LibraryName)]
    internal static partial int ZSTD_maxCLevel();

    [LibraryImport(LibraryName)]
    internal static partial int ZSTD_defaultCLevel();

    // ===== Explicit context =====

    [LibraryImport(LibraryName)]
    internal static partial IntPtr ZSTD_createCCtx();

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_freeCCtx(IntPtr cctx);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_compressCCtx(IntPtr cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, int compressionLevel);

    [LibraryImport(LibraryName)]
    internal static partial IntPtr ZSTD_createDCtx();

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_freeDCtx(IntPtr dctx);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_decompressDCtx(IntPtr dctx, void* dst, nuint dstCapacity, void* src, nuint srcSize);

    // ===== Advanced compression API =====

    [LibraryImport(LibraryName)]
    internal static partial ZSTD_bounds ZSTD_cParam_getBounds(ZSTD_cParameter cParam);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_CCtx_setParameter(IntPtr cctx, ZSTD_cParameter param, int value);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_CCtx_setPledgedSrcSize(IntPtr cctx, ulong pledgedSrcSize);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_CCtx_reset(IntPtr cctx, ZSTD_ResetDirective reset);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_compress2(IntPtr cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize);

    // ===== Advanced decompression API =====

    [LibraryImport(LibraryName)]
    internal static partial ZSTD_bounds ZSTD_dParam_getBounds(ZSTD_dParameter dParam);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_DCtx_setParameter(IntPtr dctx, ZSTD_dParameter param, int value);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_DCtx_reset(IntPtr dctx, ZSTD_ResetDirective reset);

    // ===== Streaming =====

    [LibraryImport(LibraryName)]
    internal static partial IntPtr ZSTD_createCStream();

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_freeCStream(IntPtr zcs);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_compressStream2(IntPtr cctx, ZSTD_outBuffer* output, ZSTD_inBuffer* input, ZSTD_EndDirective endOp);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_CStreamInSize();

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_CStreamOutSize();

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_initCStream(IntPtr zcs, int compressionLevel);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_compressStream(IntPtr zcs, ZSTD_outBuffer* output, ZSTD_inBuffer* input);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_flushStream(IntPtr zcs, ZSTD_outBuffer* output);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_endStream(IntPtr zcs, ZSTD_outBuffer* output);

    [LibraryImport(LibraryName)]
    internal static partial IntPtr ZSTD_createDStream();

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_freeDStream(IntPtr zds);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_initDStream(IntPtr zds);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_decompressStream(IntPtr zds, ZSTD_outBuffer* output, ZSTD_inBuffer* input);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_DStreamInSize();

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_DStreamOutSize();

    // ===== Simple dictionary API =====

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_compress_usingDict(IntPtr ctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, void* dict, nuint dictSize,
                                                          int compressionLevel);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_decompress_usingDict(IntPtr dctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, void* dict, nuint dictSize);

    // ===== Bulk processing dictionary API =====

    [LibraryImport(LibraryName)]
    internal static partial IntPtr ZSTD_createCDict(void* dictBuffer, nuint dictSize, int compressionLevel);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_freeCDict(IntPtr cdict);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_compress_usingCDict(IntPtr cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, IntPtr cdict);

    [LibraryImport(LibraryName)]
    internal static partial IntPtr ZSTD_createDDict(void* dictBuffer, nuint dictSize);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_freeDDict(IntPtr ddict);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_decompress_usingDDict(IntPtr dctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, IntPtr ddict);

    // ===== Dictionary helper functions =====

    [LibraryImport(LibraryName)]
    internal static partial uint ZSTD_getDictID_fromDict(void* dict, nuint dictSize);

    [LibraryImport(LibraryName)]
    internal static partial uint ZSTD_getDictID_fromCDict(IntPtr cdict);

    [LibraryImport(LibraryName)]
    internal static partial uint ZSTD_getDictID_fromDDict(IntPtr ddict);

    [LibraryImport(LibraryName)]
    internal static partial uint ZSTD_getDictID_fromFrame(void* src, nuint srcSize);

    // ===== Advanced dictionary and prefix API =====

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_CCtx_loadDictionary(IntPtr cctx, void* dict, nuint dictSize);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_CCtx_refCDict(IntPtr cctx, IntPtr cdict);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_CCtx_refPrefix(IntPtr cctx, void* prefix, nuint prefixSize);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_DCtx_loadDictionary(IntPtr dctx, void* dict, nuint dictSize);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_DCtx_refDDict(IntPtr dctx, IntPtr ddict);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_DCtx_refPrefix(IntPtr dctx, void* prefix, nuint prefixSize);

    // ===== Memory management =====

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_sizeof_CCtx(IntPtr cctx);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_sizeof_DCtx(IntPtr dctx);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_sizeof_CStream(IntPtr zcs);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_sizeof_DStream(IntPtr zds);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_sizeof_CDict(IntPtr cdict);

    [LibraryImport(LibraryName)]
    internal static partial nuint ZSTD_sizeof_DDict(IntPtr ddict);
}