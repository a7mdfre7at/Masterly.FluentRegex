---
id: streaming
title: "Streaming Large Input"
description: "Find matches in files, network streams and request bodies of any size while holding only a small, bounded window of the text in memory."
---

`pattern.Matches(string)` needs the whole input in one string. That is fine for a form field, but not for a 3 GB log file, an HTTP request body or a decompressed archive. The streaming methods read the input in chunks from a `TextReader` or a `Stream`, keep only a bounded window of it in memory, and return the matches one by one as they are found. Use them whenever the input is too large to load, or arrives over time.

Streaming is part of the core `Masterly.FluentRegex` package and works on every target it supports: .NET Standard 2.1, .NET 8 and .NET 10. `StreamMatch` and `StreamMatchOptions` live in the `Masterly.FluentRegex` namespace.

:::tip[The short version]

- `pattern.Matches(reader)` scans a `TextReader` synchronously and returns an `IEnumerable<StreamMatch>`.
- `pattern.MatchesAsync(reader)` and `pattern.MatchesAsync(stream)` scan asynchronously and return an `IAsyncEnumerable<StreamMatch>`.
- The results are identical to `pattern.Matches(wholeText)` as long as every match fits in `MaxMatchLength` characters (4096 by default). A longer match throws instead of being silently cut off.

:::

## The streaming methods

There are three methods on `Pattern`. They all find matches only; there is no streaming `Replace` or `Split`.

| Method | Input | Returns | Use it when |
|--------|-------|---------|-------------|
| `Matches(TextReader reader, StreamMatchOptions? options = null)` | Any `TextReader` (`StreamReader`, `StringReader`, ...) | `IEnumerable<StreamMatch>` | You are in synchronous code, for example a console tool reading a local file. |
| `MatchesAsync(TextReader reader, StreamMatchOptions? options = null, CancellationToken cancellationToken = default)` | Any `TextReader` | `IAsyncEnumerable<StreamMatch>` | You already have a reader and want non-blocking reads and cancellation. |
| `MatchesAsync(Stream stream, Encoding? encoding = null, StreamMatchOptions? options = null, CancellationToken cancellationToken = default)` | A readable `Stream` of encoded text | `IAsyncEnumerable<StreamMatch>` | You have raw bytes: a request body, a network stream, a `FileStream`, a `GZipStream`. |

All three:

- Return the matches in input order.
- Are lazy. Nothing is read until you start enumerating. After that the input is read one chunk (`BufferSize` characters) at a time, and only when the next match needs more text.
- Check their arguments at the call, not at enumeration: a `null` input, an unreadable stream or an unsupported pattern throws as soon as you call the method (see [Errors and when they are thrown](#errors-and-when-they-are-thrown)).
- Never dispose the input. They read it up to its end when you enumerate the whole sequence, and stop reading when you stop enumerating. You own the reader or stream and should dispose it yourself.
- Can be enumerated only once, because the input cannot be read again. A second `foreach` (or a second `First()`, `Count()` and so on) throws `InvalidOperationException`. Call `ToList()` when you need the matches more than once, or call the method again with a new reader.
- Use the pattern's options (`IgnoreCase`, `Multiline`, ...) and its timeout.

## Scanning a file

Open the file with a `StreamReader` and pass it to `Matches`. Enumerate the result with `foreach` like any other sequence.

```csharp
using System;
using System.IO;
using Masterly.FluentRegex;

Pattern ip = Pattern.FromRegex(@"\b\d{1,3}(?:\.\d{1,3}){3}\b");

using StreamReader log = File.OpenText("app.log");
foreach (StreamMatch match in ip.Matches(log))
{
    Console.WriteLine($"{match.Index}: {match.Value}");
}
```

For an `app.log` that contains

```text
2026-11-02 10:00:01 login from 10.0.0.7
2026-11-02 10:00:05 login from 192.168.1.20
```

this prints

```text
31: 10.0.0.7
71: 192.168.1.20
```

- `Index` counts characters from the start of the whole input, not from the start of the current chunk. It is a `long`, so it stays correct past 2 GB of text.
- The pattern is unanchored on purpose. A pattern you use for validation is usually anchored with `^...$`, and an anchored pattern finds nothing inside a larger text (see [Anchored patterns find nothing](#anchored-patterns-find-nothing)).
- The memory used does not depend on the size of the file. See [Memory and performance](#memory-and-performance).

## Scanning asynchronously

Use the async methods in ASP.NET Core, background services and anywhere else a blocked thread is expensive. Enumerate them with `await foreach`.

### From a TextReader

```csharp
using System.Threading;

// ip is the pattern from "Scanning a file"; cancellationToken comes from your application
using StreamReader log = File.OpenText("app.log");
await foreach (StreamMatch match in ip.MatchesAsync(log, cancellationToken: cancellationToken))
{
    Console.WriteLine(match);
}

// prints:
// 31: "10.0.0.7"
// 71: "192.168.1.20"
```

`StreamMatch.ToString()` returns the index and the quoted value, which is handy in logs and while debugging.

### From a Stream

When you have bytes rather than text, pass the `Stream` directly. The method decodes it for you and leaves it open, so it is safe to use on streams you do not own, such as `HttpRequest.Body`.

```csharp
using System.Text;

Pattern pair = Pattern.With
    .NamedGroup("key", Pattern.With.Word.Repeat.OneOrMore)
    .Literal("=")
    .NamedGroup("value", Pattern.With.Digit.Repeat.OneOrMore);
// (?<key>\w+)=(?<value>\d+)

// body and cancellationToken come from your application (for example HttpRequest.Body and HttpContext.RequestAborted)
await foreach (StreamMatch match in pair.MatchesAsync(body, cancellationToken: cancellationToken))
{
    Console.WriteLine($"{match.Groups["key"]} -> {match.Groups["value"]}");
}
```

For the body `retries=3 timeout=30 mode=fast` this prints

```text
retries -> 3
timeout -> 30
```

- `mode=fast` is not a match, because the value must be digits.
- After the loop the stream is still open (`body.CanRead` is `true`). The method wraps the stream in a `StreamReader` internally and disposes only that reader.
- A stream that cannot be read (for example one that is already disposed) throws `ArgumentException` with the message `The stream must be readable. (Parameter 'stream')` as soon as you call `MatchesAsync`, before you enumerate anything.
- There is no synchronous `Stream` overload. In synchronous code, wrap the stream in a `StreamReader` yourself and call `Matches(reader)`.

### Choosing the encoding

The `Stream` overload decodes UTF-8 by default. Pass another `Encoding` when the bytes use one.

```csharp
// stream holds the text "id=7, id=88" encoded as UTF-16 (little endian) with no byte order mark
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

await foreach (StreamMatch match in digits.MatchesAsync(stream, Encoding.Unicode))
{
    Console.WriteLine(match);
}

// prints:
// 3: "7"
// 9: "88"
```

- A byte order mark at the start of the stream wins over the encoding you pass. The same UTF-16 bytes with a BOM in front give the same two matches even when you pass `Encoding.UTF8`.
- The byte order mark is not part of the text, so it does not shift `Index`.
- Indexes are counted in characters (UTF-16 code units), never in bytes. In `héllo 12 wörld 345` read as UTF-8, the matches are at 6 and 15, even though `é` and `ö` take two bytes each. A character outside the Basic Multilingual Plane, such as an emoji, counts as two characters, exactly as in a .NET `string`.

:::warning

The wrong encoding does not throw; it just gives wrong results. The UTF-16 bytes above, decoded with the default UTF-8 and no BOM, produce three matches: `6: "7"`, `18: "8"` and `20: "8"`. Every second character is a `NUL`, so `88` is split in two. If the producer of the bytes does not write a BOM, pass the encoding explicitly.

:::

## Reading a match

Each result is a `StreamMatch`. It is a small, self-contained object: unlike `System.Text.RegularExpressions.Match`, it holds only its own text and group values, so the buffered input it came from can be released.

| Member | Type | What it gives you |
|--------|------|-------------------|
| `Index` | `long` | Zero-based position of the match in the whole input, in characters |
| `Length` | `int` | Length of the match in characters (the same as `Value.Length`) |
| `Value` | `string` | The matched text |
| `Groups` | `IReadOnlyDictionary<string, string>` | The value of every capture group that took part in the match |
| `ToString()` | `string` | The index and the quoted value, such as `3: "42"` |

`Groups` is keyed by name for named groups and by number (`"1"`, `"2"`, ...) for unnamed groups. Group 0, the whole match, is not included; use `Value`. A group that did not take part in the match is absent, not an empty string.

```csharp
using System.Linq;

Pattern setting = Pattern.FromRegex(@"(?<key>[a-z]+)=(?<value>\d+)?(;)?");

using StringReader reader = new StringReader("a=1; b=");
foreach (StreamMatch match in setting.Matches(reader))
{
    string groups = string.Join(", ", match.Groups
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => $"{group.Key}={group.Value}"));
    Console.WriteLine($"{match.Value} -> {groups}");
}

// prints:
// a=1; -> 1=;, key=a, value=1
// b= -> key=b
```

Because optional groups can be absent, read them with `TryGetValue` rather than the indexer, which throws `KeyNotFoundException` for a missing key:

```csharp
Pattern setting = Pattern.FromRegex(@"(?<key>[a-z]+)=(?<value>\d+)?");

using StringReader reader = new StringReader("a=1 b=");
foreach (StreamMatch match in setting.Matches(reader))
{
    string value = match.Groups.TryGetValue("value", out string? found) ? found : "(none)";
    Console.WriteLine($"{match.Groups["key"]}: {value}");
}

// prints:
// a: 1
// b: (none)
```

- A group inside a repetition keeps only its last value, as `Group.Value` does in .NET. Individual captures are not available on a stream match.
- `Groups` never contains the key `"0"`.
- To bind matches to your own types, collect the values from `Groups`. The typed extraction API works on strings (see [Typed Extraction](./typed-extraction.md)).

## The exactness contract

Streaming promises the **same** matches as `pattern.Matches(wholeText)`: the same values, the same indexes, the same groups, and the same empty matches, in the same order. The library's tests check this by comparing both on random input read in pieces of one to seven characters.

```csharp
Pattern anyAs = Pattern.FromRegex("a*");
string text = "baab";

using StringReader reader = new StringReader(text);
string streamed = string.Join(" | ", anyAs.Matches(reader).Select(m => $"{m.Index}:'{m.Value}'"));
string whole = string.Join(" | ", anyAs.Matches(text).Select(m => $"{m.Index}:'{m.Value}'"));

Console.WriteLine(streamed); // prints: 0:'' | 1:'aa' | 3:'' | 4:''
Console.WriteLine(whole);    // prints: 0:'' | 1:'aa' | 3:'' | 4:''
```

The promise holds as long as two conditions are met:

1. **Every match fits in `MaxMatchLength` characters.** This includes every character the engine looks at while it tries a match, not only the characters it keeps. A greedy `.*` looks at everything up to the end of the line (or of the input with `RegexOptions.Singleline`).
2. **Lookbehinds, `\b` and `^` need at most `LookbehindContext` characters** before the position where an attempt starts.

How the library keeps its side of the promise:

- A match is reported only once more than `MaxMatchLength` characters have been read after its start, or the input has ended. Until then it could still grow, so it is held back.
- A match that is longer than `MaxMatchLength` and does not end at the end of the input is never reported. Enumeration throws `InvalidOperationException` instead (see [Matches longer than MaxMatchLength](#matches-longer-than-maxmatchlength)).
- `^` and `\A` never match in the middle of the input, and `$`, `\Z` and `\z` never match at the end of a chunk that is not the end of the input.

## Options

`StreamMatchOptions` controls how much text is buffered. Pass it as the `options` argument; `null` means the defaults.

```csharp
StreamMatchOptions options = new StreamMatchOptions
{
    MaxMatchLength = 16_384,
    LookbehindContext = 512,
    BufferSize = 131_072
};

// ip is the pattern from "Scanning a file"; reader comes from your application
foreach (StreamMatch match in ip.Matches(reader, options))
{
    Console.WriteLine(match);
}
```

| Option | Default | Meaning |
|--------|---------|---------|
| `MaxMatchLength` | `4096` | The longest match, including the engine's lookahead, that is guaranteed to be found exactly. Also how far behind the end of the buffered text the scanner waits before it reports a match. |
| `LookbehindContext` | `256` | How many characters before the scan position are kept, so lookbehinds, `\b` and `^` with `RegexOptions.Multiline` can see earlier text. |
| `BufferSize` | `65536` | How many characters are read from the input at a time. |

- All three must be positive. Setting zero or a negative value throws `ArgumentOutOfRangeException` from the property setter, before any scan starts. For `options.BufferSize = 0` the message is `BufferSize must be positive. (Parameter 'BufferSize')` followed by `Actual value was 0.` on a second line.
- The values are copied when you call `Matches` or `MatchesAsync`. Changing the options object afterwards does not affect a scan that has already been set up, and one options object can be shared by many scans.
- Size `MaxMatchLength` for the longest match you expect, plus whatever a greedy construct looks at. Size `LookbehindContext` for your longest lookbehind.

### Matches longer than MaxMatchLength

The library never reports a match that might have been cut off at the end of the buffered text. When a match that is not at the end of the input is longer than `MaxMatchLength`, enumeration throws:

```csharp
StreamMatchOptions options = new StreamMatchOptions
{
    MaxMatchLength = 16,
    BufferSize = 32
};

Pattern number = Pattern.With.Digit.Repeat.OneOrMore;

// a run of 100 digits, then more text
using StringReader reader = new StringReader(new string('1', 100) + " tail " + new string('x', 200));
try
{
    foreach (StreamMatch match in number.Matches(reader, options))
    {
        Console.WriteLine(match);
    }
}
catch (InvalidOperationException exception)
{
    Console.WriteLine(exception.Message);
}

// prints: A match at index 0 is at least 32 characters long, more than StreamMatchOptions.MaxMatchLength (16),
// so it may have been cut off at the end of the buffered input. Increase MaxMatchLength to at least the
// longest match you expect.
```

- The message names the index of the match in the whole input and how long it was when the scanner gave up ("at least", because it may continue past the buffer).
- The exception is thrown during enumeration, at the point the long match is reached. The matches before it have already been returned.
- The fix is to raise `MaxMatchLength`, or to bound the pattern (`\d{1,64}` instead of `\d+`) so a match can never be that long.

### Long matches at the end of the input

A match that ends at the real end of the input is always exact, whatever its length. Once the input is complete there is nothing left that could extend it, so it is reported instead of throwing.

```csharp
StreamMatchOptions options = new StreamMatchOptions { MaxMatchLength = 10 };

using StringReader reader = new StringReader("x" + new string('1', 100));
foreach (StreamMatch match in Pattern.With.Digit.Repeat.OneOrMore.Matches(reader, options))
{
    Console.WriteLine($"{match.Index} {match.Length}"); // prints: 1 100
}
```

The same is true for a whole input that is shorter than `BufferSize`: it is read in one chunk, the input is complete, and every match is exact. This is why a too-small `MaxMatchLength` often goes unnoticed in tests with short inputs and only shows up on large files. Test with an input longer than `BufferSize`, or set a small `BufferSize` in the test.

### Greedy constructs and lookahead

The exception protects you from a match that is too long. It cannot protect you from a match whose **lookahead** went past the buffered text. A greedy `.*` keeps reading until the end of what it can see and then backs up to the last place the rest of the pattern fits. On a stream, "what it can see" is the buffer, not the whole input.

```csharp
using System.Text.RegularExpressions;

Pattern block = Pattern.FromRegex("BEGIN.*END", RegexOptions.Singleline);
string text = "BEGIN a END" + new string('x', 30) + "END";
StreamMatchOptions small = new StreamMatchOptions { MaxMatchLength = 16, BufferSize = 32 };

using StringReader reader = new StringReader(text);
Console.WriteLine(block.Matches(reader, small).Single().Length); // prints: 11
Console.WriteLine(block.Match(text).Length);                     // prints: 44
```

The whole-text match runs to the second `END`, 44 characters. The streamed match stopped at the first `END` because the second one had not been read yet, and at 11 characters it was short enough to be reported. Nothing throws.

:::warning

Keep the lookahead of every construct inside `MaxMatchLength`. On a stream, prefer bounded or line-limited constructs: `[^\n]*` instead of `.*` with `Singleline`, `.{0,200}` instead of `.*`, and a lazy `.*?` when you want the nearest terminator. With the defaults, `.*` without `Singleline` is safe as long as your lines are shorter than 4096 characters.

:::

When the greedy match is longer than `MaxMatchLength`, you get the exception instead. On a 100,000-character input, `BEGIN.*END` over a 5,010-character block throws `A match at index 0 is at least 5010 characters long, more than StreamMatchOptions.MaxMatchLength (4096), ...` with the defaults, and returns the 5,010-character match with `MaxMatchLength = 16_384`.

### Lookbehind context

Lookbehinds, `\b` and `^` (with `RegexOptions.Multiline`) look at text before the position where an attempt starts. The scanner keeps `LookbehindContext` characters of that text. When a lookbehind needs more, it sees a cut-off view and can silently miss a match.

```csharp
Pattern amount = Pattern.FromRegex(@"(?<=Total amount due: )\d+");
string text = new string('-', 50) + " Total amount due: 125 " + new string('-', 50);

StreamMatchOptions tooSmall = new StreamMatchOptions { BufferSize = 8, MaxMatchLength = 16, LookbehindContext = 4 };
using StringReader first = new StringReader(text);
Console.WriteLine(amount.Matches(first, tooSmall).Count()); // prints: 0

StreamMatchOptions enough = new StreamMatchOptions { BufferSize = 8, MaxMatchLength = 16, LookbehindContext = 32 };
using StringReader second = new StringReader(text);
Console.WriteLine(string.Join(", ", amount.Matches(second, enough))); // prints: 69: "125"
```

- The lookbehind here needs 19 characters. With 4 kept it fails; with 32 it works.
- The default of 256 covers ordinary lookbehinds. An unbounded lookbehind such as `(?<=START.*)` can need any amount of context; avoid it on streams.
- The tiny `BufferSize` in this sample only forces the input to arrive in several chunks. With the default `BufferSize`, this short input would be read in one chunk and the problem would not show.

### Anchors in a stream

| Anchor | On a stream |
|--------|-------------|
| `^` and `\A` (no `Multiline`) | Match only at the very start of the input |
| `^` with `RegexOptions.Multiline` | Matches at the start of every line, across chunk boundaries |
| `$`, `\Z` and `\z` (no `Multiline`) | Match only at the real end of the input (`$` and `\Z` also just before a final `\n`), never at the end of a chunk |
| `$` with `RegexOptions.Multiline` | Matches before every `\n`, and at the end of the input |
| `\b` | Works across chunk boundaries (it needs one character of context) |
| `\G` | Not supported; throws `NotSupportedException` |

```csharp
Pattern errorLine = Pattern.FromRegex(@"^ERROR\b.*", RegexOptions.Multiline);
StreamMatchOptions tiny = new StreamMatchOptions { BufferSize = 8, MaxMatchLength = 64, LookbehindContext = 8 };
string text = "INFO ok\nERROR disk full\nwarn: ERROR not at start\nERROR again";

using StringReader reader = new StringReader(text);
foreach (StreamMatch match in errorLine.Matches(reader, tiny))
{
    Console.WriteLine(match);
}

// prints:
// 8: "ERROR disk full"
// 49: "ERROR again"
```

Even though the input arrives eight characters at a time, `^` matches only at real line starts. `.*` here stops at the end of the line, so its lookahead is bounded by the line length.

### Anchored patterns find nothing

Many ready-made patterns validate a whole string, so they are anchored with `^...$`. Streaming finds matches **inside** a large text, so an anchored pattern finds nothing unless the whole input is a single match.

```csharp
// CommonPatterns.IPv4() renders ^(?:(25[0-5]|...))\.(?:(25[0-5]|...))\. ... $
using StringReader reader = new StringReader("from 10.0.0.7 and 10.0.0.8");
Console.WriteLine(CommonPatterns.IPv4().Matches(reader).Count()); // prints: 0
```

Use an unanchored pattern with word boundaries instead, as in [Scanning a file](#scanning-a-file). The ready-made patterns are described in [Common Patterns](./common-patterns.md).

## Memory and performance

The scanner keeps one growable `char[]` buffer, holding the text it still needs: up to `LookbehindContext` characters before the scan position, the text not yet scanned, and room for the next chunk. Text that has been scanned is discarded as new chunks arrive, so memory does not grow with the size of the input.

- In a test run over 21.5 million characters, the buffer settled at 131,072 characters (twice `BufferSize`) with the default options, and at 8,192 characters with `BufferSize = 4096, MaxMatchLength = 1024, LookbehindContext = 64`. A `char` is two bytes.
- The regex engine runs on a `string`, so the scanner also keeps one string copy of the buffered text, rebuilt after every chunk. Budget roughly twice the buffer, plus the read buffers of your `StreamReader`.
- Each `StreamMatch` allocates its `Value` string and, when groups took part, a small dictionary. Matches without groups share one empty dictionary.
- After each chunk, the last `MaxMatchLength` characters are scanned again, because an attempt there did not have enough text to be final. With the defaults that is about 6% extra work (4,096 of 65,536). Keep `BufferSize` well above `MaxMatchLength`; raising `MaxMatchLength` close to `BufferSize` makes the scanner re-scan more and read in more, smaller steps.
- The buffer cannot grow beyond `int.MaxValue` characters. Reaching that limit throws `InvalidOperationException` with `The streaming buffer cannot grow beyond 2 GB of characters.` In practice the `MaxMatchLength` check fires long before.

```csharp
// ip is the pattern from "Scanning a file"; accessLog is the path of a large file from your application
using StreamReader reader = File.OpenText(accessLog);
long addresses = ip.Matches(reader).LongCount();
```

`LongCount()` enumerates the sequence without keeping the matches, so this counts every address in a file of any size in bounded memory. Calling `ToList()` instead keeps every match, and the list grows with the number of matches.

:::tip

For a string that is already in memory, do not wrap it in a `StringReader` to save memory. Use `pattern.Matches(text)`, or `pattern.EnumerateMatches(span)` on .NET 8 and later, which allocates nothing per match (see [Matching](./matching.md)). Streaming pays for chunking and re-scanning; it only wins when the input is not in memory yet.

:::

## Cancellation and timeouts

The async methods accept a `CancellationToken`. It is checked before every read and before every match, and passed on to `TextReader.ReadAsync`. You can also attach a token with `WithCancellation`; both tokens are honored.

```csharp
using System.Threading.Tasks; // WithCancellation for IAsyncEnumerable<T>

using CancellationTokenSource cancellation = new CancellationTokenSource();
using StringReader reader = new StringReader("1 2 3 4 5");
int seen = 0;

try
{
    await foreach (StreamMatch match in Pattern.With.Digit.MatchesAsync(reader).WithCancellation(cancellation.Token))
    {
        seen++;
        if (seen == 2)
        {
            cancellation.Cancel();
        }
    }
}
catch (OperationCanceledException exception)
{
    Console.WriteLine($"{exception.GetType().Name} after {seen}"); // prints: OperationCanceledException after 2
}
```

- A token that is already cancelled throws `OperationCanceledException` on the first `MoveNextAsync`, before anything is read.
- The synchronous `Matches(TextReader)` has no token. Stop it by breaking out of the `foreach`.

The pattern's timeout (see [ReDoS Safety](./redos-safety.md)) applies to every match attempt on the buffered text. A catastrophic pattern throws `RegexMatchTimeoutException` from the enumeration, exactly as it would on a string:

```csharp
// the analyzer reports FRX004 (ReDoS risk) for this pattern at compile time
Pattern risky = Pattern.FromRegex(@"(a+)+$").WithTimeout(TimeSpan.FromMilliseconds(50));

using StringReader reader = new StringReader(new string('a', 40) + "!");
try
{
    risky.Matches(reader).ToList();
}
catch (RegexMatchTimeoutException exception)
{
    Console.WriteLine(exception.GetType().Name); // prints: RegexMatchTimeoutException
}
```

:::note

Input from a stream is usually input you do not control. Give every pattern you stream untrusted data through a timeout, or use a non-backtracking pattern. See [ReDoS Safety](./redos-safety.md).

:::

## Errors and when they are thrown

Some errors are thrown when you call the method, others only while you enumerate the result.

| Exception | When | Cause |
|-----------|------|-------|
| `ArgumentNullException` | At the call | `reader` or `stream` is `null` |
| `ArgumentException` | At the call | The stream is not readable (`CanRead` is `false`) |
| `NotSupportedException` | At the call | The pattern uses `RegexOptions.RightToLeft` or the `\G` anchor |
| `ArgumentOutOfRangeException` | When you set an option | `MaxMatchLength`, `LookbehindContext` or `BufferSize` is zero or negative |
| `InvalidOperationException` | During enumeration | A match is longer than `MaxMatchLength` and does not end at the end of the input |
| `InvalidOperationException` | At the start of a second enumeration | The same sequence is enumerated again |
| `RegexMatchTimeoutException` | During enumeration | A match attempt exceeded the pattern's timeout |
| `OperationCanceledException` | During async enumeration | The cancellation token was cancelled |
| Any exception from the reader | During enumeration | For example `IOException` or a decoder error, passed through unchanged |

The `NotSupportedException` messages explain why:

```csharp
using StringReader reader = new StringReader("123");
try
{
    Pattern.FromRegex(@"\d", RegexOptions.RightToLeft).Matches(reader);
}
catch (NotSupportedException exception)
{
    Console.WriteLine(exception.Message);
}

// prints: Streaming does not support RegexOptions.RightToLeft: the input is scanned left to right as it arrives.
```

For `\G` the message is `Streaming does not support the \G anchor: it depends on where each scan restarts, which differs from whole-text matching.`

## Enumerate the result once

A streaming sequence is tied to one pass over its reader. The scan state is created when you call `Matches` or `MatchesAsync`, not when you enumerate, so enumerating the same sequence again **continues** where the previous enumeration stopped instead of starting over.

```csharp
using System.Collections.Generic;

Pattern number = Pattern.With.Digit.Repeat.OneOrMore;

using StringReader reader = new StringReader("1 22 333 4444");
IEnumerable<StreamMatch> matches = number.Matches(reader);

Console.WriteLine(matches.First());  // prints: 0: "1"
Console.WriteLine(matches.First());  // prints: 2: "22"
Console.WriteLine(matches.Count());  // prints: 2
Console.WriteLine(matches.Count());  // prints: 0
```

- Run one LINQ operator or one `foreach` over a streaming sequence. If you need the matches more than once, materialize them first with `ToList()` (and accept that the list holds them all), or call `Matches` again on a fresh reader.
- Do not read from the reader yourself while a scan is in progress. The scanner reads ahead by up to `BufferSize` characters, so the reader's position says nothing about where the scan is.

## When to stream

| Your input | Use |
|------------|-----|
| A string you already have | `pattern.Matches(text)` or `pattern.EnumerateMatches(span)` (see [Matching](./matching.md)) |
| A file small enough to read into memory | Either. `File.ReadAllText` plus `Matches(text)` is simpler and gives you full `Match` objects. |
| A file too large for memory | `Matches(reader)` or `MatchesAsync(reader)` |
| An HTTP request body, a socket, a pipe | `MatchesAsync(stream)` |
| A compressed file | `MatchesAsync(new GZipStream(file, CompressionMode.Decompress))` |
| A query against a database column | [Entity Framework Core](./entity-framework-core.md), which runs the pattern in the database |

## Limitations

- `RegexOptions.RightToLeft` and the `\G` anchor throw `NotSupportedException`. Both depend on scanning the whole text at once.
- Constructs that look further ahead than `MaxMatchLength` can return a different, shorter match without an error. Keep greedy constructs bounded (see [Greedy constructs and lookahead](#greedy-constructs-and-lookahead)).
- A lookbehind that needs more than `LookbehindContext` characters can miss matches without an error (see [Lookbehind context](#lookbehind-context)).
- Streaming finds matches only. To redact or rewrite a large input, process it line by line with the string APIs (see [Fluent Replacements](./fluent-replacements.md) and [PII Redaction](./pii-redaction.md)).
- A `StreamMatch` has the match value and the last value of each group, but no `Captures`, no group positions and no `NextMatch()`.
- Indexes are in characters. To get byte offsets, you must track them yourself from the encoding.
- The scan ends when the reader reports the end of its input (a read that returns no characters). It does not wait for more data, so it cannot follow a file that is still being written, like `tail -f`.
- The [command-line tool](./command-line-tool.md) reads its input files whole; it does not use streaming.

## Related pages

- [Matching](./matching.md): `IsMatch`, `Match`, `Matches` and span-based matching on strings.
- [ReDoS Safety](./redos-safety.md): timeouts and patterns that cannot backtrack catastrophically.
- [Quantifiers](./quantifiers.md): bounded, lazy and possessive repetition.
- [Assertions](./assertions.md): lookaheads, lookbehinds and anchors.
- [Best Practices](./best-practices.md): general advice for building and using patterns.
