#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.FluentRegex.Streaming;

/// <summary>Pumps a reader into a <see cref="StreamMatchScanner"/> and yields its matches.</summary>
internal static class StreamMatchEnumerator
{
    private const bool DetectEncodingFromByteOrderMarks = true;
    private const bool LeaveStreamOpen = true;

    /// <summary>Reads <paramref name="reader"/> synchronously and yields every match.</summary>
    public static IEnumerable<StreamMatch> Enumerate(StreamMatchScanner scanner, TextReader reader)
    {
        char[] chunk = new char[scanner.BufferSize];
        while (true)
        {
            StreamMatch? match = scanner.Next();
            if (match != null)
            {
                yield return match;
            }
            else if (scanner.IsFinished)
            {
                yield break;
            }
            else
            {
                Fill(scanner, reader, chunk);
            }
        }
    }

    /// <summary>Reads <paramref name="reader"/> asynchronously and yields every match.</summary>
    public static async IAsyncEnumerable<StreamMatch> EnumerateAsync(
        StreamMatchScanner scanner,
        TextReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        char[] chunk = new char[scanner.BufferSize];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StreamMatch? match = scanner.Next();
            if (match != null)
            {
                yield return match;
            }
            else if (scanner.IsFinished)
            {
                yield break;
            }
            else
            {
                await FillAsync(scanner, reader, chunk, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Decodes <paramref name="stream"/> and yields every match, leaving the stream open.</summary>
    public static async IAsyncEnumerable<StreamMatch> EnumerateAsync(
        StreamMatchScanner scanner,
        Stream stream,
        Encoding encoding,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using StreamReader reader = new StreamReader(
            stream, encoding, DetectEncodingFromByteOrderMarks, scanner.BufferSize, LeaveStreamOpen);
        await foreach (StreamMatch match in EnumerateAsync(scanner, reader, cancellationToken).ConfigureAwait(false))
        {
            yield return match;
        }
    }

    private static void Fill(StreamMatchScanner scanner, TextReader reader, char[] chunk)
    {
        int filled = 0;
        bool endOfInput = false;
        while (!endOfInput && filled < chunk.Length)
        {
            int read = reader.Read(chunk, filled, chunk.Length - filled);
            endOfInput = read == 0;
            filled += read;
        }

        Deliver(scanner, chunk, filled, endOfInput);
    }

    private static async Task FillAsync(
        StreamMatchScanner scanner,
        TextReader reader,
        char[] chunk,
        CancellationToken cancellationToken)
    {
        int filled = 0;
        bool endOfInput = false;
        while (!endOfInput && filled < chunk.Length)
        {
            Memory<char> free = chunk.AsMemory(filled, chunk.Length - filled);
            int read = await reader.ReadAsync(free, cancellationToken).ConfigureAwait(false);
            endOfInput = read == 0;
            filled += read;
        }

        Deliver(scanner, chunk, filled, endOfInput);
    }

    private static void Deliver(StreamMatchScanner scanner, char[] chunk, int filled, bool endOfInput)
    {
        scanner.Append(chunk, filled);
        if (endOfInput)
        {
            scanner.CompleteInput();
        }
    }
}
