namespace zstd.net;

/// <summary>
/// Provides streaming decompression using Zstandard.
/// </summary>
public sealed class ZstdDecompressStream : Stream
{
    private IntPtr _dstream;
    private bool _disposed;

    private readonly Stream _stream;
    private readonly int _inputBufferSize;
    private readonly int _outputBufferSize;
    private readonly bool _leaveOpen;
    private readonly byte[] _inputBuffer;
    private readonly byte[] _outputBuffer;

    private ZSTD_inBuffer _inBuffer;
    private ZSTD_outBuffer _outBuffer;
    private int _outputBufferPos;
    private int _outputBufferAvailable;
    private bool _endOfStream;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZstdDecompressStream"/> class.
    /// </summary>
    /// <param name="stream">The underlying stream to read compressed data from.</param>
    /// <param name="leaveOpen">True to leave the stream open after disposing; otherwise, false.</param>
    public ZstdDecompressStream(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;

        // Use recommended buffer sizes
        _inputBufferSize = (int)ZstdNative.ZSTD_DStreamInSize();
        _outputBufferSize = (int)ZstdNative.ZSTD_DStreamOutSize();
        _inputBuffer = new byte[_inputBufferSize];
        _outputBuffer = new byte[_outputBufferSize];

        _dstream = ZstdNative.ZSTD_createDStream();
        if (_dstream == IntPtr.Zero)
        {
            throw new OutOfMemoryException("Failed to create ZSTD decompression stream");
        }

        // Initialize the decompression stream
        nuint initResult = ZstdNative.ZSTD_initDStream(_dstream);
        if (ZstdNative.ZSTD_isError(initResult) != 0)
        {
            ZstdNative.ZSTD_freeDStream(_dstream);
            _dstream = IntPtr.Zero;
            var errorCode = ZstdNative.ZSTD_getErrorCode(initResult);
            throw new InvalidOperationException($"Failed to initialize decompression stream: {Zstd.GetErrorString(errorCode)}");
        }

        _outputBufferPos = 0;
        _outputBufferAvailable = 0;
        _endOfStream = false;
    }

    // Stream properties
    public override bool CanRead => !_disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException("ZstdDecompressStream does not support seeking");

    public override long Position
    {
        get => throw new NotSupportedException("ZstdDecompressStream does not support seeking");
        set => throw new NotSupportedException("ZstdDecompressStream does not support seeking");
    }

    // Stream methods
    public override int Read(byte[] buffer, int offset, int count)
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

        if (count == 0 || _endOfStream)
            return 0;

        int totalBytesRead = 0;

        while (totalBytesRead < count && !_endOfStream)
        {
            // If we have data in the output buffer, copy it first
            if (_outputBufferAvailable > 0)
            {
                int bytesToCopy = Math.Min(_outputBufferAvailable, count - totalBytesRead);
                Array.Copy(_outputBuffer, _outputBufferPos, buffer, offset + totalBytesRead, bytesToCopy);
                _outputBufferPos += bytesToCopy;
                _outputBufferAvailable -= bytesToCopy;
                totalBytesRead += bytesToCopy;
                continue;
            }

            // Need to decompress more data
            if (!DecompressNextChunk())
            {
                _endOfStream = true;
                break;
            }
        }

        return totalBytesRead;
    }

    private bool DecompressNextChunk()
    {
        unsafe
        {
            // Read more compressed data if needed
            if (_inBuffer.pos >= _inBuffer.size)
            {
                int bytesRead = _stream.Read(_inputBuffer, 0, _inputBufferSize);
                if (bytesRead == 0)
                {
                    return false; // End of input stream
                }

                fixed (byte* inputPtr = _inputBuffer)
                {
                    _inBuffer.src = inputPtr;
                    _inBuffer.size = (nuint)bytesRead;
                    _inBuffer.pos = 0;
                }
            }

            fixed (byte* inputPtr = _inputBuffer)
            fixed (byte* outputPtr = _outputBuffer)
            {
                // Update input buffer pointer (in case it wasn't set above)
                if (_inBuffer.src == null)
                {
                    _inBuffer.src = inputPtr;
                }

                // Set up output buffer
                _outBuffer.dst = outputPtr;
                _outBuffer.size = (nuint)_outputBufferSize;
                _outBuffer.pos = 0;

                // Decompress
                var inBuffer = _inBuffer;
                var outBuffer = _outBuffer;
                nuint result = ZstdNative.ZSTD_decompressStream(_dstream, &outBuffer, &inBuffer);
                _inBuffer = inBuffer;
                _outBuffer = outBuffer;

                if (ZstdNative.ZSTD_isError(result) != 0)
                {
                    var errorCode = ZstdNative.ZSTD_getErrorCode(result);
                    throw new InvalidOperationException($"Decompression failed: {Zstd.GetErrorString(errorCode)}");
                }

                // Update output buffer state
                _outputBufferPos = 0;
                _outputBufferAvailable = (int)_outBuffer.pos;

                return _outputBufferAvailable > 0;
            }
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("ZstdDecompressStream does not support writing");
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException("ZstdDecompressStream does not support seeking");
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException("ZstdDecompressStream does not support seeking");
    }

    public override void Flush()
    {
        // No-op for decompression stream
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (!_leaveOpen)
                {
                    _stream?.Dispose();
                }
            }

            if (_dstream != IntPtr.Zero)
            {
                ZstdNative.ZSTD_freeDStream(_dstream);
                _dstream = IntPtr.Zero;
            }

            _disposed = true;
        }

        base.Dispose(disposing);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ZstdDecompressStream));
    }
}
