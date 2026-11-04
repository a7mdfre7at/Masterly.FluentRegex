Masterly.FluentRegex 3.2.0

A correctness release with more than 30 fixes, found while every feature was run end to end for the new
documentation site (https://a7mdfre7at.github.io/Masterly.FluentRegex/). Highlights: CommonPatterns.PhoneNumber()
and Time12() match what their docs promise; Explain() describes conditionals, escapes and inline options
correctly; validation uses the pattern's options; literals keep matching under IgnorePatternWhitespace; structs
work with Extract<T>; redaction no longer leaves part of an overlapping value visible; the source generator and
analyzers no longer crash and every rule can be configured in .editorconfig; EF Core Replace reads $12 like .NET;
and the ASP.NET Core route constraint works with a pre-registered registry.
Breaking: Repeat.Optional directly after a quantifier now makes the repetition optional, (?:\d+)?, instead of
lazy; use .Lazy for a lazy quantifier. See CHANGELOG.md for every change.
