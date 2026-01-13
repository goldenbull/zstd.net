# zstd.net

dotnet wrapper for zstd using interop.

## how

[zstd](https://github.com/facebook/zstd) has three header files: `zstd.h`, `zstd_errors.h` and `zdict.h`

I tanslated the first two header files into C# in an almost line-by-line manner. **TODO:** `zdict.h` is not translated yet.

Also the offical souce code gives some examples like [streaming_compression.c](https://github.com/facebook/zstd/blob/dev/examples/streaming_compression.c) and [streaming_decompression.c](https://github.com/facebook/zstd/blob/dev/examples/streaming_decompression.c), which are good references to implement the corresponding C# logic.

Quite easy.

## multi-thread compression

The offical repo release contains only x86 and x64 DLL for windows, and the multi-thread compression is not enabled in these DLLs, so I build the native library files from source in MacOS, Linux and Windows.

## x86 and arm32 are not supported yet

But it's quite easy to support, just add the dll/so file to the correct folder inside `native` folder. `NativeLibrary.SetDllImportResolver` is used to search for the native lib file. See [here](https://github.com/goldenbull/zstd.net/blob/830d11bccb93aa17f796465d2db89b03e9cc6a49/src/zstd.net/Zstd_c.cs#L160) for the path lookup logic.
