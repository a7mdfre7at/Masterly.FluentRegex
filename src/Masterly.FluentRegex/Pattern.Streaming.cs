#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Masterly.FluentRegex.Profiling;
using Masterly.FluentRegex.Streaming;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex;

/// <summary>
/// Matching over text that does not fit in memory: readers and streams are scanned in chunks.
/// </summary>
public partial class Pattern
{
    private static readonly Encoding DefaultStreamEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Finds every match in text read from <paramref name="reader"/>, holding only a bounded window of the
    /// text in memory.
    /// </summary>
    /// <param name="reader">The text to scan. It is read to the end but not disposed.</param>
    /// <param name="options">Buffering limits; see <see cref="StreamMatchOptions"/> for the exactness contract.</param>
    /// <returns>
    /// The matches in input order, identical to <see cref="Matches(string)"/> on the whole text whenever every
    /// match fits within <see cref="StreamMatchOptions.MaxMatchLength"/> characters. The sequence is lazy:
    /// the reader is consumed as it is enumerated. It can be enumerated only once, because the reader cannot be
    /// read again; a second enumeration throws <see cref="InvalidOperationException"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="NotSupportedException">
    /// The pattern uses <see cref="RegexOptions.RightToLeft"/> or the <c>\G</c> anchor.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// During enumeration: a match is longer than <see cref="StreamMatchOptions.MaxMatchLength"/> and might be
    /// truncated.
    /// </exception>
    /// <example>
    /// Pattern ip = Pattern.FromRegex(@"\b\d{1,3}(?:\.\d{1,3}){3}\b");
    /// using StreamReader log = File.OpenText("app.log");
    /// foreach (StreamMatch m in ip.Matches(log))
    ///     Console.WriteLine($"{m.Index}: {m.Value}");
    /// </example>
    public IEnumerable<StreamMatch> Matches(TextReader reader, StreamMatchOptions? options = null)
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        return StreamMatchEnumerator.Enumerate(CreateStreamScanner(options), reader);
    }

    /// <summary>
    /// Asynchronously finds every match in text read from <paramref name="reader"/>, holding only a bounded
    /// window of the text in memory.
    /// </summary>
    /// <param name="reader">The text to scan. It is read to the end but not disposed.</param>
    /// <param name="options">Buffering limits; see <see cref="StreamMatchOptions"/> for the exactness contract.</param>
    /// <param name="cancellationToken">Cancels reading. It is checked before every read and every match.</param>
    /// <returns>
    /// The matches in input order, identical to <see cref="Matches(string)"/> on the whole text. The sequence can be
    /// enumerated only once; a second enumeration throws <see cref="InvalidOperationException"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="NotSupportedException">
    /// The pattern uses <see cref="RegexOptions.RightToLeft"/> or the <c>\G</c> anchor.
    /// </exception>
    /// <example>
    /// await foreach (StreamMatch m in pattern.MatchesAsync(reader, cancellationToken: token))
    ///     Console.WriteLine(m.Value);
    /// </example>
    public IAsyncEnumerable<StreamMatch> MatchesAsync(
        TextReader reader,
        StreamMatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        return StreamMatchEnumerator.EnumerateAsync(CreateStreamScanner(options), reader, cancellationToken);
    }

    /// <summary>
    /// Asynchronously decodes <paramref name="stream"/> and finds every match, holding only a bounded window of
    /// the text in memory. The stream is left open.
    /// </summary>
    /// <param name="stream">A readable stream of encoded text.</param>
    /// <param name="encoding">
    /// The text encoding. Defaults to UTF-8. A byte order mark at the start of the stream takes precedence.
    /// </param>
    /// <param name="options">Buffering limits; see <see cref="StreamMatchOptions"/> for the exactness contract.</param>
    /// <param name="cancellationToken">Cancels reading. It is checked before every read and every match.</param>
    /// <returns>
    /// The matches in input order, with indexes counted in characters, not bytes. The sequence can be enumerated only
    /// once; a second enumeration throws <see cref="InvalidOperationException"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="stream"/> is not readable.</exception>
    /// <exception cref="NotSupportedException">
    /// The pattern uses <see cref="RegexOptions.RightToLeft"/> or the <c>\G</c> anchor.
    /// </exception>
    public IAsyncEnumerable<StreamMatch> MatchesAsync(
        Stream stream,
        Encoding? encoding = null,
        StreamMatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        StreamMatchScanner scanner = CreateStreamScanner(options);
        Encoding effectiveEncoding = encoding ?? DefaultStreamEncoding;
        return StreamMatchEnumerator.EnumerateAsync(scanner, stream, effectiveEncoding, cancellationToken);
    }

    private StreamMatchScanner CreateStreamScanner(StreamMatchOptions? options)
    {
        EnsureStreamable();
        return new StreamMatchScanner(CachedRegex, options ?? new StreamMatchOptions());
    }

    private void EnsureStreamable()
    {
        if ((_options & RegexOptions.RightToLeft) != 0)
        {
            throw new NotSupportedException(
                "Streaming does not support RegexOptions.RightToLeft: " +
                "the input is scanned left to right as it arrives.");
        }

        if (UsesContiguousMatchAnchor())
        {
            throw new NotSupportedException(
                @"Streaming does not support the \G anchor: it depends on where each scan restarts, " +
                "which differs from whole-text matching.");
        }
    }

    private bool UsesContiguousMatchAnchor()
    {
        if (!RegexParser.TryParse(ToString(), _options, out RegexTree tree))
        {
            return false;
        }

        foreach (RegexNode node in RegexNodeWalker.DescendantsAndSelf(tree.Root))
        {
            if (node is AnchorNode anchor && anchor.AnchorKind == AnchorKind.ContiguousMatch)
            {
                return true;
            }
        }

        return false;
    }
}
