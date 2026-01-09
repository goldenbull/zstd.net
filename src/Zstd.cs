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
}