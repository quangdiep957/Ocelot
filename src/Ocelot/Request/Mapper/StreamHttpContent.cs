﻿using Microsoft.AspNetCore.Http;
using System.Buffers;

namespace Ocelot.Request.Mapper;

public class StreamHttpContent : HttpContent
{
    private const int DefaultBufferSize = 65536;
    public const long UnknownLength = -1;
    private readonly HttpContext _context;

    public StreamHttpContent(HttpContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context,
        CancellationToken cancellationToken)
    {
        await CopyAsync(_context.Request.Body, stream, Headers.ContentLength ?? UnknownLength, false,
            cancellationToken);
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
    {
        await CopyAsync(_context.Request.Body, stream, Headers.ContentLength ?? UnknownLength, false,
            CancellationToken.None);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = -1;
        return false;
    }

    // this is used internally by HttpContent.ReadAsStreamAsync(...)
    protected override Task<Stream> CreateContentReadStreamAsync()
    {
        // Nobody should be calling this...
        throw new NotImplementedException();
    }

    private static async Task CopyAsync(Stream input, Stream output, long announcedContentLength,
        bool autoFlush, CancellationToken cancellation)
    {
        // for smaller payloads, avoid allocating a buffer that is larger than the announced content length
        var minBufferSize = announcedContentLength != UnknownLength && announcedContentLength < DefaultBufferSize
            ? (int)announcedContentLength
            : DefaultBufferSize;

        var buffer = ArrayPool<byte>.Shared.Rent(minBufferSize);
        long contentLength = 0;
        try
        {
            while (true)
            {
                // Issue a zero-byte read to the input stream to defer buffer allocation until data is available.
                // Note that if the underlying stream does not supporting blocking on zero byte reads, then this will
                // complete immediately and won't save any memory, but will still function correctly.
                var zeroByteReadTask = input.ReadAsync(Memory<byte>.Empty, cancellation);
                if (zeroByteReadTask.IsCompletedSuccessfully)
                {
                    // Consume the ValueTask's result in case it is backed by an IValueTaskSource
                    _ = zeroByteReadTask.Result;
                }
                else
                {
                    // Take care not to return the same buffer to the pool twice in case zeroByteReadTask throws
                    var bufferToReturn = buffer;
                    buffer = null;
                    ArrayPool<byte>.Shared.Return(bufferToReturn);

                    await zeroByteReadTask;

                    buffer = ArrayPool<byte>.Shared.Rent(minBufferSize);
                }

                var read = await input.ReadAsync(buffer.AsMemory(), cancellation);
                contentLength += read;

                // Normally this is enforced by the server, but it could get out of sync if something in the proxy modified the body.
                if (announcedContentLength != UnknownLength && contentLength > announcedContentLength)
                {
                    throw new InvalidOperationException("More bytes received than the specified Content-Length.");
                }

                // End of the source stream.
                if (read == 0)
                {
                    if (announcedContentLength == UnknownLength || contentLength == announcedContentLength)
                    {
                        return;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Sent {contentLength} request content bytes, but Content-Length promised {announcedContentLength}.");
                    }
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
                if (autoFlush)
                {
                    // HttpClient doesn't always flush outgoing data unless the buffer is full or the caller asks.
                    // This is a problem for streaming protocols like WebSockets and gRPC.
                    await output.FlushAsync(cancellation);
                }
            }
        }
        finally
        {
            if (buffer != null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
