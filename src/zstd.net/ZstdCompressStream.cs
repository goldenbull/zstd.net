namespace zstd.net;

/// <summary>
/// Provides streaming compression using Zstandard.
/// </summary>
public sealed class ZstdCompressStream : Stream
{
    private IntPtr _cstream;
    private bool _disposed;

    private readonly Stream _stream;
    private readonly int _bufferSize;
    private readonly bool _leaveOpen;
    private readonly int _compressionLevel;
    private readonly int _nThreads;
    private readonly byte[] _outputBuffer;

    private ZSTD_inBuffer _inBuffer;
    private ZSTD_outBuffer _outBuffer;

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

        if (bufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be positive");

        _bufferSize = bufferSize;
        _leaveOpen = leaveOpen;
        _compressionLevel = compressionLevel;
        _nThreads = nThreads;
        _outputBuffer = new byte[bufferSize];

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
            fixed (byte* outputPtr = _outputBuffer)
            {
                // Set up input buffer
                _inBuffer.src = inputPtr;
                _inBuffer.size = (nuint)count;
                _inBuffer.pos = 0;

                // Set up output buffer
                _outBuffer.dst = outputPtr;
                _outBuffer.size = (nuint)_bufferSize;

                // Compress until all input is consumed
                bool finished;
                do
                {
                    // Reset output buffer position
                    _outBuffer.pos = 0;

                    // Compress with ZSTD_e_continue mode
                    var outBuffer = _outBuffer;
                    var inBuffer = _inBuffer;
                    nuint remaining = ZstdNative.ZSTD_compressStream2(_cstream, &outBuffer, &inBuffer, ZSTD_EndDirective.ZSTD_e_continue);
                    if (ZstdNative.ZSTD_isError(remaining) != 0)
                    {
                        var errorCode = ZstdNative.ZSTD_getErrorCode(remaining);
                        throw new InvalidOperationException($"Compression failed: {Zstd.GetErrorString(errorCode)}");
                    }

                    // Write compressed output to the underlying stream
                    if (_outBuffer.pos > 0)
                    {
                        _stream.Write(_outputBuffer, 0, (int)_outBuffer.pos);
                    }

                    // We're finished when we've consumed all the input
                    finished = (_inBuffer.pos == _inBuffer.size);
                } while (!finished);

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
                // End the compression stream with ZSTD_e_end
                FlushInternal(ZSTD_EndDirective.ZSTD_e_end);

                if (!_leaveOpen)
                {
                    _stream?.Dispose();
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
            fixed (byte* outputPtr = _outputBuffer)
            {
                // Set up input buffer (empty for flush/end)
                _inBuffer.src = null;
                _inBuffer.size = 0;
                _inBuffer.pos = 0;

                // Set up output buffer
                _outBuffer.dst = outputPtr;
                _outBuffer.size = (nuint)_bufferSize;

                // Keep flushing/ending until remaining == 0
                nuint remaining;
                do
                {
                    // Reset output buffer position
                    _outBuffer.pos = 0;

                    // Flush or end based on the directive
                    var outBuffer = _outBuffer;
                    var inBuffer = _inBuffer;
                    remaining = ZstdNative.ZSTD_compressStream2(_cstream, &outBuffer, &inBuffer, endDirective);
                    _outBuffer = outBuffer;
                    _inBuffer = inBuffer;

                    if (ZstdNative.ZSTD_isError(remaining) != 0)
                    {
                        var errorCode = ZstdNative.ZSTD_getErrorCode(remaining);
                        var operation = endDirective == ZSTD_EndDirective.ZSTD_e_end ? "End compression" : "Flush";
                        throw new InvalidOperationException($"{operation} failed: {Zstd.GetErrorString(errorCode)}");
                    }

                    // Write output to the underlying stream
                    if (_outBuffer.pos > 0)
                    {
                        _stream.Write(_outputBuffer, 0, (int)_outBuffer.pos);
                    }
                } while (remaining > 0);

                // Flush the underlying stream
                _stream.Flush();
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ZstdCompressStream));
    }
}