namespace zstd.net;

/// <summary>
/// Provides streaming compression using Zstandard.
/// user will pass the input bytes when calling Write() method
/// internally, we use _inBuffer and _outBuffer to point to the input bytes and _outBytes,
/// then invoke native lib to do the compression.
/// </summary>
public sealed class ZstdCompressStream : Stream
{
    private readonly Stream _stream;
    private readonly int _bufferSize;
    private readonly bool _leaveOpen;
    private readonly byte[] _outBytes;

    private IntPtr _cstream;
    private ZSTD_inBuffer _inBuffer;
    private ZSTD_outBuffer _outBuffer;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZstdCompressStream"/> class.
    /// </summary>
    /// <param name="stream">The underlying stream to write compressed data to.</param>
    /// <param name="bufferSize">The buffer size for compression operations. Default is 1MB.</param>
    /// <param name="leaveOpen">True to leave the stream open after disposing; otherwise, false.</param>
    /// <param name="compressionLevel">The compression level to use.</param>
    /// <param name="nThreads">The number of threads to use for compression.</param>
    public ZstdCompressStream(Stream stream, int bufferSize = 1 << 20, bool leaveOpen = false, int compressionLevel = ZstdNative.ZSTD_CLEVEL_DEFAULT,
                              int nThreads = 1)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));

        if (bufferSize <= 8192)
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be bigger then 8K");

        _bufferSize = bufferSize;
        _leaveOpen = leaveOpen;
        _outBytes = new byte[bufferSize];

        _cstream = ZstdNative.ZSTD_createCStream();
        if (_cstream == IntPtr.Zero)
        {
            throw new OutOfMemoryException("Failed to create ZSTD compression stream");
        }

        // Set compression parameters
        var levelResult = ZstdNative.ZSTD_CCtx_setParameter(_cstream, ZSTD_cParameter.ZSTD_c_compressionLevel, compressionLevel);
        if (ZstdNative.ZSTD_isError(levelResult) != 0)
        {
            ZstdNative.ZSTD_freeCStream(_cstream);
            _cstream = IntPtr.Zero;
            var errorCode = ZstdNative.ZSTD_getErrorCode(levelResult);
            throw new InvalidOperationException($"Failed to set compression level: {Zstd.GetErrorString(errorCode)}");
        }

        // Enable checksum
        var checksumResult = ZstdNative.ZSTD_CCtx_setParameter(_cstream, ZSTD_cParameter.ZSTD_c_checksumFlag, 1);
        if (ZstdNative.ZSTD_isError(checksumResult) != 0)
        {
            ZstdNative.ZSTD_freeCStream(_cstream);
            _cstream = IntPtr.Zero;
            var errorCode = ZstdNative.ZSTD_getErrorCode(checksumResult);
            throw new InvalidOperationException($"Failed to enable checksum: {Zstd.GetErrorString(errorCode)}");
        }

        // Set number of workers for multi-threaded compression
        if (nThreads > 1)
        {
            var workerResult = ZstdNative.ZSTD_CCtx_setParameter(_cstream, ZSTD_cParameter.ZSTD_c_nbWorkers, nThreads);
            if (ZstdNative.ZSTD_isError(workerResult) != 0)
            {
                // Library doesn't support multithreading, continue with single-threaded mode
                // This is not fatal, just a fallback
            }
        }
    }

    // Stream properties
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => !_disposed;

    public override long Length => throw new NotSupportedException("ZstdCompressStream does not support seeking");

    public override long Position
    {
        get => throw new NotSupportedException("ZstdCompressStream does not support seeking");
        set => throw new NotSupportedException("ZstdCompressStream does not support seeking");
    }

    // Stream methods
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("ZstdCompressStream does not support reading");
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException("ZstdCompressStream does not support seeking");
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException("ZstdCompressStream does not support seeking");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();

        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (offset + count > buffer.Length)
            throw new ArgumentException("The sum of offset and count is larger than the buffer length");

        if (count == 0)
            return;

        unsafe
        {
            fixed (byte* inputPtr = &buffer[offset])
            {
                // Set up input buffer
                _inBuffer.src = inputPtr;
                _inBuffer.size = (nuint)count;
                _inBuffer.pos = 0;

                // Compress until all input is consumed
                CompressAndWriteLoop(ZSTD_EndDirective.ZSTD_e_continue);

                // Verify all input was consumed
                if (_inBuffer.pos != _inBuffer.size)
                {
                    throw new InvalidOperationException("Failed to consume all input data");
                }
            }
        }
    }

    public override void Flush()
    {
        ThrowIfDisposed();
        FlushInternal(ZSTD_EndDirective.ZSTD_e_flush);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // End the compression frame with ZSTD_e_end
                FlushInternal(ZSTD_EndDirective.ZSTD_e_end);

                if (!_leaveOpen)
                {
                    _stream.Dispose();
                }
            }

            if (_cstream != IntPtr.Zero)
            {
                ZstdNative.ZSTD_freeCStream(_cstream);
                _cstream = IntPtr.Zero;
            }

            _disposed = true;
        }

        base.Dispose(disposing);
    }

    private void FlushInternal(ZSTD_EndDirective endDirective)
    {
        unsafe
        {
            // Set up input buffer (empty for flush/end)
            _inBuffer.src = null;
            _inBuffer.size = 0;
            _inBuffer.pos = 0;

            // Keep flushing/ending until remaining == 0
            CompressAndWriteLoop(endDirective);

            // Flush the underlying stream
            _stream.Flush();
        }
    }

    /// <summary>
    /// compress all data in _inBuffer into _outBuffer
    /// </summary>
    private void CompressAndWriteLoop(ZSTD_EndDirective endDirective)
    {
        unsafe
        {
            fixed (byte* outputPtr = _outBytes)
            {
                // Set up output buffer
                _outBuffer.dst = outputPtr;
                _outBuffer.size = (nuint)_bufferSize;

                bool finished;
                do
                {
                    // Reset output buffer position
                    _outBuffer.pos = 0;

                    // Compress with the specified directive
                    var outBuffer = _outBuffer;
                    var inBuffer = _inBuffer;
                    var remaining = ZstdNative.ZSTD_compressStream2(_cstream, &outBuffer, &inBuffer, endDirective);
                    _outBuffer = outBuffer;
                    _inBuffer = inBuffer;

                    if (ZstdNative.ZSTD_isError(remaining) != 0)
                    {
                        var errorCode = ZstdNative.ZSTD_getErrorCode(remaining);
                        throw new InvalidOperationException($"{endDirective} failed: {Zstd.GetErrorString(errorCode)}");
                    }

                    // Write compressed output to the underlying stream
                    if (_outBuffer.pos > 0)
                    {
                        _stream.Write(_outBytes, 0, (int)_outBuffer.pos);
                    }

                    if (endDirective == ZSTD_EndDirective.ZSTD_e_continue)
                    {
                        // For ZSTD_e_continue (Write), continue until all input is consumed
                        finished = _inBuffer.pos >= _inBuffer.size;
                    }
                    else
                    {
                        // For ZSTD_e_flush/ZSTD_e_end (Flush/Dispose), continue until remaining == 0
                        finished = remaining == 0;
                    }
                } while (!finished);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ZstdCompressStream));
    }
}