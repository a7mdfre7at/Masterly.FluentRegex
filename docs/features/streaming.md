# Matching over streams

Find matches in text that is too large to load into a string: log files, HTTP bodies, decompressed
archives. The pattern reads the input in chunks and keeps only a bounded window of it in memory.

```csharp
public IEnumerable<StreamMatch> Matches(TextReader reader, StreamMatchOptions? options = null);
public IAsyncEnumerable<StreamMatch> MatchesAsync(TextReader reader, StreamMatchOptions? options = null,
    CancellationToken cancellationToken = default);
public IAsyncEnumerable<StreamMatch> MatchesAsync(Stream stream, Encoding? encoding = null,
    StreamMatchOptions? options = null, CancellationToken cancellationToken = default);
```

## Reading a file

```csharp
Pattern ip = Pattern.FromRegex(@"\b\d{1,3}(?:\.\d{1,3}){3}\b");

using StreamReader log = File.OpenText("app.log");
foreach (StreamMatch m in ip.Matches(log))
{
    Console.WriteLine($"{m.Index}: {m.Value}");
}
```

For a log that contains

```text
2026-11-02 10:00:01 login from 10.0.0.7
2026-11-02 10:00:05 login from 192.168.1.20
```

this prints

```text
31: 10.0.0.7
71: 192.168.1.20
```

`Index` is a `long` that counts characters from the start of the whole input, so it stays correct past
2 GB of text.

Note that many `CommonPatterns` members, such as `IPv4()`, are anchored with `^...$` because they
validate whole strings. To find them inside larger text, use an unanchored pattern as above.

## Reading a stream asynchronously

`MatchesAsync(Stream)` decodes the bytes (UTF-8 by default; a byte order mark takes precedence) and
leaves the stream open, so it works on request bodies and network streams you do not own.

```csharp
Pattern pair = Pattern.With
    .NamedGroup("key", Pattern.With.Word.Repeat.OneOrMore)
    .Literal("=")
    .NamedGroup("value", Pattern.With.Digit.Repeat.OneOrMore);

await foreach (StreamMatch m in pair.MatchesAsync(request.Body, cancellationToken: token))
{
    Console.WriteLine($"{m.Groups["key"]} -> {m.Groups["value"]}");
}
```

For the body `retries=3 timeout=30 mode=fast` this prints

```text
retries -> 3
timeout -> 30
```

`Groups` holds every group that participated in the match, keyed by name for named groups and by
number (`"1"`, `"2"`, ...) for unnamed ones. Group 0 is not included; use `Value`.

Cancellation is checked before every read and before every match. The pattern's
[`WithTimeout`](../../README.md) still applies to each match attempt.

## The exactness contract

Streamed results are **identical** to `pattern.Matches(wholeText)`, including empty matches,
as long as:

- every match, and every character the engine looks ahead at while trying a match, fits within
  `MaxMatchLength` characters of the position where the attempt starts, and
- lookbehinds, `\b` and `^` (with `RegexOptions.Multiline`) need at most `LookbehindContext`
  characters before that position.

A match is only reported once more than `MaxMatchLength` characters have been read after its
start, or the input has ended. `^` and `\A` never match in the middle of a stream.

| Option | Default | Meaning |
|---|---|---|
| `MaxMatchLength` | 4096 | Longest match (plus lookahead) that is guaranteed to be found exactly. |
| `LookbehindContext` | 256 | Characters kept before the scan position for lookbehinds, `\b` and `^`. |
| `BufferSize` | 65536 | Characters read per chunk. |

All three must be positive; setting zero or a negative value throws `ArgumentOutOfRangeException`.
Memory use is bounded by the options, not the input: the buffer grows to about twice `BufferSize` characters
(131,072 with the defaults), plus a string copy of the buffered text.

### Matches longer than `MaxMatchLength`

The library never emits a match that might have been cut off. If a match before the end of the
input is longer than `MaxMatchLength`, enumeration throws `InvalidOperationException`:

```csharp
StreamMatchOptions options = new StreamMatchOptions { MaxMatchLength = 16, BufferSize = 32 };
Pattern.With.Digit.Repeat.OneOrMore.Matches(reader, options).ToList();
```

```text
A match at index 0 is at least 32 characters long, more than StreamMatchOptions.MaxMatchLength (16),
so it may have been cut off at the end of the buffered input. Increase MaxMatchLength to at least the
longest match you expect.
```

Matches that end at the real end of the input are always exact, whatever their length.

## Limitations

- `RegexOptions.RightToLeft` and the `\G` anchor throw `NotSupportedException`: both depend on
  scanning the whole text at once.
- Greedy constructs such as `.*` look ahead as far as they can. On a stream, keep them bounded
  (`[^\n]*`, `.{0,200}`) so the lookahead stays within `MaxMatchLength`.
- A lookbehind that needs more than `LookbehindContext` characters can miss matches.
