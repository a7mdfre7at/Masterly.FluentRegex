---
id: sql-dialects
title: "SQL Regex Dialects"
description: "Translate a pattern for PostgreSQL, MySQL, MariaDB, SQL Server 2025 and SQLite, or write a WHERE condition, with the exact .NET meaning."
---

`ToSqlCondition` and `ToFlavor` translate a `Pattern` into the regex dialect of a database, so the same rule can run in a `WHERE` clause and mean exactly what it means in .NET. The supported engines are PostgreSQL, MySQL 8+, MariaDB, SQL Server 2025 / Azure SQL and SQLite (through EF Core). Use this page when you write raw SQL, use Dapper, or want to check what a pattern becomes before you send it to a database. For LINQ queries, the [Entity Framework Core](./entity-framework-core.md) package uses these translations for you.

No database engine treats `$`, `.`, `\d`, `\w`, `\b` or case-insensitivity the way .NET does. The translator therefore writes all of them out explicitly. When a construct cannot be expressed on an engine, it throws a `NotSupportedException` that lists every problem. It never emits a regex that silently means something else.

:::tip[What you need]

- Everything on this page is in the core `Masterly.FluentRegex` package, in the `Masterly.FluentRegex` namespace. It works on every target the package supports: .NET Standard 2.1, .NET 8 and .NET 10.
- The translation does not need a database connection, and the library never opens one.

:::

## Writing a WHERE condition

`ToSqlCondition(columnSql, flavor)` returns a boolean SQL expression that tests a column against the pattern with the engine's own regex operator.

```csharp
using Masterly.FluentRegex;

Pattern productCode = Pattern.FromRegex(@"^[A-Z]{3}-[0-9]{4}$");

string postgres = productCode.ToSqlCondition("\"Code\"", RegexFlavor.PostgreSql);
// "Code" ~ '\A[A-Z]{3}-[0-9]{4}(?=\n?\Z)'
string mySql = productCode.ToSqlCondition("Code", RegexFlavor.MySql);
// REGEXP_LIKE(Code, '(?-i)\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\z)')
string mariaDb = productCode.ToSqlCondition("Code", RegexFlavor.MariaDb);
// Code REGEXP '(?-i)\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\z)'
string sqlServer = productCode.ToSqlCondition("[Code]", RegexFlavor.SqlServer);
// REGEXP_LIKE([Code], N'\A[A-Z]{3}-[0-9]{4}\n?\z')
string sqlite = productCode.ToSqlCondition("Code", RegexFlavor.Sqlite);
// Code REGEXP '^[A-Z]{3}-[0-9]{4}$'
```

- `columnSql` is inserted **verbatim**. Quote the column yourself in the style of your database (`"Code"`, `` `Code` ``, `[Code]`). Never build it from user input.
- `$` is rewritten, for example as `(?=\n?\Z)` on PostgreSQL, because .NET's `$` also matches before a final newline. The engines' own `$` does not (see [How .NET constructs are written](#how-net-constructs-are-written)).
- MySQL and MariaDB patterns start with `(?-i)`. Without it, both engines would match case-insensitively under the default collations (see [Case sensitivity and collations](#case-sensitivity-and-collations)).
- The pattern part is the SQL string literal from `ToLiteral()`. Quotes are doubled. For MySQL and MariaDB, backslashes are doubled too.

| Flavor | Engine | Condition | Literal |
|--------|--------|-----------|---------|
| `RegexFlavor.PostgreSql` | PostgreSQL advanced regular expressions (ARE) | `column ~ '...'` | `'...'` |
| `RegexFlavor.MySql` | MySQL 8.0+ (ICU) | `REGEXP_LIKE(column, '...')` | `'...'`, backslashes doubled |
| `RegexFlavor.MariaDb` | MariaDB (PCRE2) | `column REGEXP '...'` | `'...'`, backslashes doubled |
| `RegexFlavor.SqlServer` | SQL Server 2025 and Azure SQL (RE2) | `REGEXP_LIKE(column, N'...')` | `N'...'` (national literal) |
| `RegexFlavor.Sqlite` | SQLite `regexp()` registered by EF Core (.NET's own engine) | `column REGEXP '...'` | `'...'` |

`RegexFlavor` also has `JavaScript` and `JsonSchema`. Those are not SQL flavors; see [Exporting Patterns](./export.md).

### Building the pattern fluently

`ToSqlCondition` works on any pattern, however you build it.

```csharp
Pattern pin = Pattern.With.StartOfLine.Digit.Repeat.Times(4).EndOfLine;

string mySql = pin.ToSqlCondition("Pin", RegexFlavor.MySql);
// REGEXP_LIKE(Pin, '(?-i)\\A\\p{Nd}{4}(?=\\n?\\z)')
string sqlServer = pin.ToSqlCondition("[Pin]", RegexFlavor.SqlServer);
// REGEXP_LIKE([Pin], N'\A\p{Nd}{4}\n?\z')
```

`Digit` (`\d`) means every Unicode decimal digit in .NET, so the translation keeps that meaning: `\p{Nd}` on engines that know Unicode categories, and the explicit list of every digit range on PostgreSQL. If you only want `0-9`, say so with `WithAsciiDigits()`. The SQL gets much shorter:

```csharp
// pin is from the example above
int unicodeLength = pin.ToFlavor(RegexFlavor.PostgreSql).Pattern.Length;   // 487
string asciiOnly = pin.WithAsciiDigits().ToSqlCondition("\"Pin\"", RegexFlavor.PostgreSql);
// "Pin" ~ '\A[0-9]{4}(?=\n?\Z)'
```

:::tip

Use `WithAsciiDigits()` or the `DigitMode.Ascii` overloads of `CommonPatterns` (for example `CommonPatterns.ZipCode(DigitMode.Ascii)`) whenever your data only holds ASCII digits. This matters most on PostgreSQL, which has no `\p{..}` classes, and on SQL Server, which rejects patterns longer than 4,000 characters (see [Digits in every script](./unicode.md)).

:::

## Translating a pattern with ToFlavor

`ToFlavor(flavor, options)` returns a `FlavorConversion`: the translated pattern plus everything you need to use it safely. `ToSqlCondition` is a thin wrapper around it.

```csharp
FlavorConversion mySql = Pattern.FromRegex(@"^[0-9]+$").ToFlavor(RegexFlavor.MySql);

RegexFlavor flavor = mySql.Flavor;             // MySql
string pattern = mySql.Pattern;                // (?-i)\A[0-9]+(?=\n?\z)
string literal = mySql.ToLiteral();            // '(?-i)\\A[0-9]+(?=\\n?\\z)'
string flags = mySql.Flags;                    // "" (always empty for SQL flavors)
bool isSql = mySql.IsSql;                      // true
bool isExact = mySql.IsExact;                  // true
bool preservesExtent = mySql.PreservesMatchExtent; // true
```

| Member | Type | Meaning |
|--------|------|---------|
| `Flavor` | `RegexFlavor` | The target dialect. |
| `Pattern` | `string` | The translated regex, without quotes, delimiters or flags. Use it as a query **parameter**. |
| `ToLiteral()` | `string` | `Pattern` as a SQL string literal, ready to paste into SQL text. `ToString()` returns the same. |
| `Flags` | `string` | Always empty for SQL flavors: every option is written into `Pattern`. |
| `IsSql` | `bool` | True for the five SQL flavors. |
| `Warnings` | `IReadOnlyList<string>` | Differences in which strings match. Always empty for SQL flavors (see [The translation contract](#the-translation-contract)). |
| `IsExact` | `bool` | True when `Warnings` is empty: the engine accepts exactly the strings .NET accepts. |
| `MatchExtentWarnings` | `IReadOnlyList<string>` | Differences in which substring a match covers or what a group captures. Never null. |
| `PreservesMatchExtent` | `bool` | True when `MatchExtentWarnings` is empty. Matters for `regexp_substr`, `REGEXP_REPLACE` and other extraction. |
| `GroupNumbers` | `IReadOnlyDictionary<string, int>` | The engine's group number for each .NET group, by name and by .NET number (see [Group numbers](#group-numbers)). |

`ToFlavor` takes an optional `FlavorOptions`. The SQL flavors ignore it: they always keep the .NET meaning.

```csharp
FlavorOptions loose = new FlavorOptions { PreserveLineSemantics = false, PreserveUnicodeSemantics = false };

string withOptions = Pattern.FromRegex(@"^\d+$").ToFlavor(RegexFlavor.MySql, loose).Pattern;
// (?-i)\A\p{Nd}+(?=\n?\z)   (the same as without options)
```

### Literal or parameter

The two outputs serve different purposes. Mixing them up is the most common mistake.

| You are writing | Use | Why |
|-----------------|-----|-----|
| SQL text with the pattern inside it | `ToLiteral()` or `ToSqlCondition(...)` | Quotes and, for MySQL and MariaDB, backslashes are escaped for the SQL parser. |
| A query parameter (`@pattern`, `$1`, `?`) | `Pattern` | The driver sends the value as is. A literal would put the quotes and doubled backslashes into the regex itself. |

:::warning

`ToLiteral()` makes assumptions about how the server parses string literals:

- **MySQL and MariaDB**: backslashes are doubled because their string literals process C escapes. This assumes `sql_mode` does not contain `NO_BACKSLASH_ESCAPES` (the default). With that mode on, the doubled backslashes reach the regex engine and change the pattern. Use a parameter in that case.
- **PostgreSQL**: the literal assumes `standard_conforming_strings = on`, the default since PostgreSQL 9.1.
- **SQL Server**: the literal is a national literal, `N'...'`. A plain `'...'` literal is `varchar`, and SQL Server 2025 silently truncates a `varchar` pattern to 4,000 characters before `REGEXP_LIKE` sees it.

:::

## Running the query

### Raw SQL and Dapper

This sample runs end to end against an in-memory SQLite database with `Microsoft.Data.Sqlite` and Dapper. EF Core registers SQLite's `regexp()` function for you. With plain `Microsoft.Data.Sqlite`, you register it yourself the same way.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dapper;
using Masterly.FluentRegex;
using Microsoft.Data.Sqlite;

Pattern productCode = Pattern.FromRegex(@"^[A-Z]{3}-[0-9]{4}$");

using SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

// EF Core registers regexp() for you; with plain Microsoft.Data.Sqlite, register it the same way
connection.CreateFunction("regexp", (string pattern, string? input) =>
    input != null && Regex.IsMatch(input, pattern, RegexOptions.NonBacktracking, TimeSpan.FromSeconds(1)));

connection.Execute("CREATE TABLE Products (Code TEXT); INSERT INTO Products VALUES ('ABC-1234'), ('abc-1234'), ('ABC-12345');");

// 1. The condition inside the SQL text
string condition = productCode.ToSqlCondition("Code", RegexFlavor.Sqlite);
List<string> inline = connection.Query<string>($"SELECT Code FROM Products WHERE {condition}").ToList();
// ABC-1234

// 2. The pattern as a parameter
string pattern = productCode.ToFlavor(RegexFlavor.Sqlite).Pattern;
List<string> parameterized = connection.Query<string>(
    "SELECT Code FROM Products WHERE Code REGEXP @pattern", new { pattern }).ToList();
// ABC-1234
```

- SQLite calls `regexp(pattern, input)` for `input REGEXP pattern`, so the pattern is the first argument.
- `abc-1234` does not match: the pattern is case-sensitive, as it is in .NET.
- `ABC-12345` does not match: `$` only allows a final newline after the four digits, nothing else.

### ADO.NET parameters on other databases

The same idea works with any `DbConnection` (Npgsql, MySqlConnector, Microsoft.Data.SqlClient and so on). Translate for the right flavor and pass `Pattern` as the parameter value.

```csharp
using System;
using System.Data.Common;
using System.Globalization;
using Masterly.FluentRegex;

static int CountValidEmails(DbConnection connection)
{
    FlavorConversion email = CommonPatterns.Email().ToFlavor(RegexFlavor.PostgreSql);

    using DbCommand command = connection.CreateCommand();
    command.CommandText = "SELECT count(*) FROM customers WHERE \"Email\" ~ @pattern";

    DbParameter parameter = command.CreateParameter();
    parameter.ParameterName = "pattern";
    parameter.Value = email.Pattern; // the bare pattern, never ToLiteral()
    command.Parameters.Add(parameter);

    return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
}
```

:::note

This sample needs a PostgreSQL server, so it was compiled but not run for this page. The operator per engine is listed in [Writing a WHERE condition](#writing-a-where-condition). On SQL Server, keep the parameter within `nvarchar(4000)`: that is the largest pattern argument `REGEXP_LIKE` accepts.

:::

## The translation contract

These rules hold for every SQL flavor.

- **Options live in the pattern.** `Flags` is always empty, so the result can always be used as `column OPERATOR 'pattern'`.
- **Acceptance is exact, or the call throws.** A returned SQL conversion has no `Warnings`, so `IsExact` is always true. Anything the engine cannot express with the same meaning throws one `NotSupportedException` that lists every obstacle with its position (see [Unsupported constructs and errors](#unsupported-constructs-and-errors)).
- **Exact means exact for the Basic Multilingual Plane.** For input text in U+0000 to U+FFFF, the engine accepts exactly the strings .NET accepts. Supplementary characters, such as emoji, are outside this promise (see [Supplementary characters](#supplementary-characters)).
- **Extent is reported separately.** `MatchExtentWarnings` lists every way the matched substring or a group's capture could differ, for strings both sides accept. Plain tests (`~`, `REGEXP_LIKE`, `REGEXP`) are not affected (see [Match extent and captures](#match-extent-and-captures)).
- **Case-insensitivity follows the current culture** at translation time, like .NET's `Regex` (see [Culture](#culture)).
- **Every literal is pure ASCII.** Non-ASCII and control characters are written as escapes (`é` becomes `\u00E9` on PostgreSQL and `\x{00E9}` on the other engines), so the SQL text is safe in any client encoding. SQLite is the exception: it gets the .NET pattern text unchanged.
- **`FlavorOptions` is ignored.** There is no "loose" mode for SQL.

## How .NET constructs are written

The engines disagree with .NET on line endings, word characters and case. The translator spells these out. Every cell below is real output. SQLite is not in the table because it runs .NET's own engine and gets the pattern unchanged (see [SQLite](#sqlite-through-ef-core)).

| .NET | PostgreSQL | MySQL, MariaDB | SQL Server |
|------|------------|----------------|------------|
| `^`, `\A` | `\A` | `\A` | `\A` |
| `$`, `\Z` | `(?=\n?\Z)` | `(?=\n?\z)` | `\n?\z` (only at the end of the pattern) |
| `\z` | `\Z` | `\z` | `\z` |
| `(?m)^x$` | `(?:\A\|(?<=\n))x(?=\n\|\Z)` | `(?:\A\|(?<=\n))x(?=\n\|\z)` | `(?m:^)x(?m:$)` |
| `.` | `[^\n]` | `[^\n]` | `.` |
| `.` under `(?s)` | `.` | `(?s:.)` | `(?s:.)` |
| `\d` | explicit ranges of every Unicode digit | `\p{Nd}` | `\p{Nd}` |
| `\w` | explicit ranges | `[\p{L}\p{Mn}\p{Nd}\p{Pc}]` | `[\p{L}\p{Mn}\p{Nd}\p{Pc}]` |
| `\s` | `[\u0009-\u000D \u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]` | `[\t\n\x{000B}\f\r\x{0085}\p{Z}]` | `[\t\n\x{000B}\f\r\x{0085}\p{Z}]` |
| `[^\W\d]` | explicit ranges | `[\p{L}\p{Mn}\p{Pc}]` | `[\p{L}\p{Mn}\p{Pc}]` |
| `\b` | lookarounds over the explicit word ranges | lookarounds over `[\p{L}\p{Mn}\p{Nd}\p{Pc}\x{200C}\x{200D}]` | unsupported (ASCII `\b` only under `RegexOptions.ECMAScript`) |
| `\p{Lu}` | explicit ranges | `\p{Lu}` | `\p{Lu}` |
| `\p{IsGreek}` | `[\u0370-\u03FF]` | `[\x{0370}-\x{03FF}]` | `[\x{0370}-\x{03FF}]` |
| `(?i)hello` | `[Hh][Ee][Ll][Ll][Oo]` | `(?-i)[Hh][Ee][Ll][Ll][Oo]` | `[Hh][Ee][Ll][Ll][Oo]` |
| `[:alpha:]` | `[\:ahlp]` | `[\:alpha\:]` | `[\:alpha\:]` |
| `(?<n>a)\k<n>` | `(a)\1` | MySQL `(?<n>a)\k<n>`, MariaDB `(?<n>a)\g{1}` | unsupported |
| `a{300}` | `(?:a{255}a{45})` | `a{300}` | `a{300}` |
| `é` | `\u00E9` | `\x{00E9}` | `\x{00E9}` |
| `😀` (written in the pattern) | `\U0001F600` | `\x{1F600}` | `\x{1F600}` |

The MySQL and MariaDB column omits the `(?-i)` prefix that starts every pattern, except in the `(?i)hello` row.

- **Large sets are compacted** with the Unicode categories they fully contain. `[^\W\d]` becomes `[\p{L}\p{Mn}\p{Pc}]` instead of kilobytes of ranges. PostgreSQL has no `\p{..}`, so it always gets explicit ranges.
- **`:`, `=` and `.` are escaped inside classes.** In .NET, `[:alpha:]` means "one of `:`, `a`, `l`, `p`, `h`". ICU (MySQL) would read it as the POSIX class of letters, and PCRE2 (MariaDB) rejects `[:...]`, `[=...]` and `[....]`.
- **Unicode blocks** such as `\p{IsGreek}` are .NET-only names, so they always become explicit ranges.

## Engine notes

### PostgreSQL

PostgreSQL's advanced regular expressions (ARE) power the `~` operator and `regexp_like`. The translation relies on these engine facts:

- `\b` is a backspace, so word boundaries are written as lookarounds.
- `$` never matches before a trailing newline, and `.` and `[^...]` match newlines by default.
- `\d`, `\s` and `\w` depend on the database locale, so they are written as explicit ranges.
- There are no `\p{..}` classes, no named groups and no atomic groups.
- Embedded options such as `(?i)` are only allowed at the very start, so case-insensitivity is expanded into classes.
- Repeat bounds go up to 255.

```csharp
string repeat = Pattern.FromRegex("a{300}").ToFlavor(RegexFlavor.PostgreSql).Pattern;      // (?:a{255}a{45})
string lazy = Pattern.FromRegex("x{2,300}?").ToFlavor(RegexFlavor.PostgreSql).Pattern;     // (?:x{2}x{0,255}?x{0,43}?)
string named = Pattern.FromRegex(@"(?<n>a)\k<n>").ToFlavor(RegexFlavor.PostgreSql).Pattern; // (a)\1
string accent = Pattern.FromRegex("café").ToFlavor(RegexFlavor.PostgreSql).Pattern;        // caf\u00E9
string digitAfter = Pattern.FromRegex(@"(a)\1\x30").ToFlavor(RegexFlavor.PostgreSql).Pattern; // (a)\1\u0030
```

- **Large repeats** of a single character or class are split into chunks of at most 255. A large repeat of a group, such as `(?:a){300}`, is unsupported.
- **Named groups** lose their names and become numbered groups. Use `GroupNumbers` to find them.
- **Explicit classes get long.** With no `\p{..}`, `\w` alone becomes 5,575 characters and `\bx` becomes 22,372, because each `\b` holds the word class four times. PostgreSQL has no length limit, but prefer ASCII classes (`[A-Za-z0-9_]`, `WithAsciiDigits()`) when your data allows it.
- **A digit after a backreference** is escaped, so `\1` followed by `0` never reads as `\10`.
- **Parentheses inside lookarounds** never capture in PostgreSQL. Such groups are written as `(?:...)` and left out of `GroupNumbers`. A backreference inside a lookaround is unsupported.
- **Groups reset on every repeat iteration.** On `abab`, `^(?:(a)|(b))+\1\2$` matches in .NET, where group 1 keeps the `a` from an earlier iteration, but not in PostgreSQL. A backreference to a group that an iteration can skip is therefore unsupported. Patterns whose group is set by every iteration, such as `^(a)*\1$`, translate exactly: `\A(a)*\1(?=\n?\Z)`.
- **Match preference differs**: PostgreSQL picks the longest (or shortest) match, not the first alternative. Only extraction is affected (see [Match extent and captures](#match-extent-and-captures)).

### MySQL

MySQL 8.0 and later use ICU regular expressions for `REGEXP_LIKE`, `REGEXP` and the other `REGEXP_*` functions.

```csharp
string named = Pattern.FromRegex(@"(?<n>a)\k<n>").ToFlavor(RegexFlavor.MySql).Pattern;
// (?-i)(?<n>a)\k<n>
string underscore = Pattern.FromRegex(@"(?<my_group>a)\k<my_group>").ToFlavor(RegexFlavor.MySql).Pattern;
// (?-i)(a)\1
string lookbehind = Pattern.FromRegex(@"(?<=a{1,2})b").ToFlavor(RegexFlavor.MySql).Pattern;
// (?-i)(?<=a{1,2})b
string atomic = Pattern.FromRegex(@"(?>a+)b").ToFlavor(RegexFlavor.MySql).Pattern;
// (?-i)(?>a+)b
```

- Every pattern starts with `(?-i)` (see [Case sensitivity and collations](#case-sensitivity-and-collations)).
- ICU's `\w`, `\s`, `\b` and line terminators differ from .NET, so they are written as `\p{..}` classes and explicit `\n`.
- Group names are kept when ICU accepts them: ASCII letters and digits, starting with a letter. Other names, such as `my_group`, are dropped in favor of numbers.
- Lookarounds and atomic groups are supported. A lookbehind must have a **bounded** length: `(?<=a{1,2})` works, `(?<=a+)` is unsupported.
- Repeat counts go up to 16,777,215.
- ICU can fail at query time with "Timeout exceeded in regular expression match", or overflow its backtrack stack, on some lazy loops whose body can match empty text. That is an error, never a wrong answer, but keep such loops out of patterns you send to MySQL.

### MariaDB

MariaDB uses PCRE2 for `REGEXP`, `REGEXP_SUBSTR` and `REGEXP_REPLACE`.

```csharp
string named = Pattern.FromRegex(@"(?<n>a)\k<n>").ToFlavor(RegexFlavor.MariaDb).Pattern;
// (?-i)(?<n>a)\g{1}
string underscore = Pattern.FromRegex(@"(?<my_group>a)\k<my_group>").ToFlavor(RegexFlavor.MariaDb).Pattern;
// (?-i)(?<my_group>a)\g{1}
string lookbehind = Pattern.FromRegex(@"(?<=ab|cde)x").ToFlavor(RegexFlavor.MariaDb).Pattern;
// (?-i)(?<=ab|cde)x
string maxRepeat = Pattern.FromRegex("a{65535}").ToFlavor(RegexFlavor.MariaDb).Pattern;
// (?-i)a{65535}
```

- Every pattern starts with `(?-i)`, as on MySQL.
- PCRE2's `\d`, `\w` and `\b` are ASCII-only by default, so they are written as `\p{..}` classes.
- Backreferences are written `\g{n}`, which avoids the `\10` octal ambiguity.
- Group names are kept when PCRE2 accepts them: up to 32 ASCII letters, digits and underscores, not starting with a digit.
- Each top-level branch of a lookbehind must have a **fixed** length. `(?<=ab|cde)` works (each branch is fixed), `(?<=a{1,2})` is unsupported.
- Repeat counts go up to 65,535.

### SQL Server 2025 and Azure SQL

`REGEXP_LIKE` and the other `REGEXP_*` functions in SQL Server 2025 and Azure SQL are based on RE2. RE2 has no lookarounds, no backreferences, no atomic groups and no conditionals, so those constructs are unsupported.

```csharp
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

string digits = Pattern.FromRegex(@"^\d{4}$").ToFlavor(RegexFlavor.SqlServer).Pattern;
// \A\p{Nd}{4}\n?\z
string named = Pattern.FromRegex(@"(?<year>\d{4})-(\d{2})").ToFlavor(RegexFlavor.SqlServer).Pattern;
// (?P<year>\p{Nd}{4})-(\p{Nd}{2})
string multiline = Pattern.FromRegex("(?m)^x$").ToFlavor(RegexFlavor.SqlServer).Pattern;
// (?m:^)x(?m:$)
string asciiBoundary = Pattern.FromRegex(@"\bx\d\B").WithOptions(RegexOptions.ECMAScript).ToFlavor(RegexFlavor.SqlServer).Pattern;
// \bx[0-9]\B
string literal = Pattern.FromRegex("it's").ToFlavor(RegexFlavor.SqlServer).ToLiteral();
// N'it''s'
```

- **`$` must end the pattern.** RE2 has no lookahead, so .NET's `$` (end, or before a final newline) is written `\n?\z`, which consumes the newline. That is only correct when nothing follows `$`: `a$b` is unsupported. It also changes the match extent (see [Match extent and captures](#match-extent-and-captures)).
- **No Unicode `\b`.** RE2's `\b` is ASCII-only and there are no lookarounds to build .NET's. `\b` is accepted only under `RegexOptions.ECMAScript`, where .NET's `\b` is ASCII too.
- **Named groups** become `(?P<name>...)` when the name is ASCII letters, digits and underscores, not starting with a digit.
- **Repeat counts** go up to 1,000.
- **Pattern length** is limited to 4,000 UTF-16 code units, because the pattern argument is at most `nvarchar(4000)`. A longer translation throws at translation time. Category compaction keeps most patterns far below the limit, but `\p{Cn}` (4,465 characters, because RE2 has no `Cn` category) and `[\p{L}-[a-z]]` (5,597 characters) are too long.
- **Collation is ignored.** `REGEXP_LIKE` matches case-sensitively by default regardless of the column collation, so no prefix is needed.

### SQLite through EF Core

SQLite has no built-in regex engine. The EF Core SQLite provider registers a `regexp()` function that calls `Regex.IsMatch(input, pattern, RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(1000))` in your application process. The translation is therefore the .NET pattern itself, with its options written as an inline prefix.

```csharp
string options = Pattern.FromRegex("a b")
    .WithOptions(RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace)
    .ToFlavor(RegexFlavor.Sqlite).Pattern;
// (?ix)a b
string compiled = Pattern.FromRegex(@"\d+").WithOptions(RegexOptions.Compiled).ToFlavor(RegexFlavor.Sqlite).Pattern;
// \d+
```

| Option | SQLite translation |
|--------|--------------------|
| `IgnoreCase`, `Multiline`, `Singleline`, `ExplicitCapture`, `IgnorePatternWhitespace` | Inline prefix `(?imsnx)` (only the letters that apply). |
| `Compiled` | Dropped: it changes speed, not meaning. |
| `CultureInvariant` | Dropped when it cannot change the result; otherwise `NotSupportedException` (see [Culture](#culture)). |
| `RightToLeft`, `ECMAScript`, `NonBacktracking` | `NotSupportedException`: they have no inline form. |

- Constructs the non-backtracking engine rejects throw at translation time instead of at query time: backreferences, lookarounds, atomic groups, conditionals, balancing groups and `\G`.
- `GroupNumbers` maps every group to its own .NET number, because the engine is the same.
- `MatchExtentWarnings` is always empty.
- SQLite string literals do not process backslashes, so `ToLiteral()` only doubles quotes.
- `regexp()` runs on the application side, so case-insensitive matching uses the culture of the thread that runs the query.

## Case sensitivity and collations

### The (?-i) prefix on MySQL and MariaDB

MySQL and MariaDB match `REGEXP` case-insensitively whenever the collation is case-insensitive, which includes the defaults (`utf8mb4_0900_ai_ci` on MySQL 8.4, `utf8mb4_uca1400_ai_ci` on MariaDB 11.4). Every translated pattern for these engines therefore starts with `(?-i)`, which turns case folding off. The pattern then behaves the same under every collation.

```csharp
string plain = Pattern.FromRegex("hello").ToFlavor(RegexFlavor.MariaDb).Pattern;   // (?-i)hello
```

Without the prefix, the engines would fold far more than you expect. On MySQL 8.4 under `_ci` collations, `E` matches `e`, the Kelvin sign matches `k`, `ß` matches `ss`, and even `\p{Lu}` matches lowercase letters. MariaDB folds case under every collation except `_bin` and binary strings, including `utf8mb4_uca1400_as_cs`.

:::note

Accent-insensitive collations (`_ai`) never reach `REGEXP` on either engine: `é` does not match `e`, with or without `(?-i)`. Neither engine applies canonical equivalence, so precomposed `é` and `e` followed by a combining accent stay different, as in .NET. To match with or without accents, rewrite the pattern with `IgnoreDiacritics()` before translating (see [Unicode and Scripts](./unicode.md)).

:::

PostgreSQL's `~` is case-sensitive, and SQL Server's `REGEXP_LIKE` ignores the column collation and is case-sensitive by default, so their patterns need no prefix.

### Case-insensitive patterns

No engine folds case the way .NET does, so case-insensitive parts are expanded into exact character classes. SQLite keeps `(?i)` because it runs .NET's engine.

```csharp
Pattern hello = Pattern.FromRegex("(?i)hello");

string postgres = hello.ToFlavor(RegexFlavor.PostgreSql).Pattern;   // [Hh][Ee][Ll][Ll][Oo]
string mySql = hello.ToFlavor(RegexFlavor.MySql).Pattern;           // (?-i)[Hh][Ee][Ll][Ll][Oo]
string sqlServer = hello.ToFlavor(RegexFlavor.SqlServer).Pattern;   // [Hh][Ee][Ll][Ll][Oo]
string sqlite = hello.ToFlavor(RegexFlavor.Sqlite).Pattern;         // (?i)hello

string kelvin = Pattern.FromRegex("(?i)k").ToFlavor(RegexFlavor.MySql).Pattern;   // (?-i)[Kk\x{212A}]
```

- The classes contain every character .NET treats as equal, such as the Kelvin sign (U+212A) for `k`.
- A **case-insensitive backreference**, such as `(?i)(a)\1`, is unsupported on every regex-engine flavor: no engine compares a backreference the way .NET does.

### Culture

.NET's case-insensitive matching depends on the culture. The translation uses the same case equivalences .NET's `Regex` would use if it were created now: those of `CultureInfo.CurrentCulture` at the time of the call, or the invariant ones under `RegexOptions.CultureInvariant`. Only three tables exist: invariant, Turkish and Azerbaijani (dotted and dotless i), and every other culture.

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern letterI = Pattern.FromRegex("(?i)^i$");

CultureInfo.CurrentCulture = new CultureInfo("en-US");
string english = letterI.ToFlavor(RegexFlavor.PostgreSql).Pattern;   // \A[Ii\u0130](?=\n?\Z)

CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
string turkish = letterI.ToFlavor(RegexFlavor.PostgreSql).Pattern;   // \A[i\u0130](?=\n?\Z)

string invariant = letterI.WithOptions(RegexOptions.CultureInvariant).ToFlavor(RegexFlavor.PostgreSql).Pattern;
// \A[Ii](?=\n?\Z)
```

:::tip

A translation captures the culture current when you make it. Translate and match under the same culture, or add `RegexOptions.CultureInvariant` so the result never depends on the thread that runs your code. `Pattern.IsMatch` uses the culture current when the pattern first matches, because its `Regex` is cached.

:::

SQLite is different: `regexp()` runs in your process with the culture of the querying thread, and `CultureInvariant` has no inline form. It is dropped when no case-insensitive part can match `i`, `I`, `İ` (U+0130) or `ı` (U+0131), the only letters whose case equivalents depend on the culture. Otherwise the translation throws:

```csharp
Pattern pin = Pattern.FromRegex("pin").WithOptions(RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

try
{
    pin.ToFlavor(RegexFlavor.Sqlite);
}
catch (NotSupportedException ex)
{
    Console.WriteLine(ex.Message);
}
// prints:
// The pattern cannot be converted to SQLite without changing its meaning:
//   - RegexOptions.CultureInvariant (has no inline form; the EF Core SQLite regexp() function only receives the pattern text)
```

The same options on `abc` translate to `(?i)abc`, because no letter in it depends on the culture.

## Group numbers

.NET numbers unnamed groups before named ones. Every SQL engine numbers groups left to right, and PostgreSQL has no named groups at all. `GroupNumbers` maps each .NET group, by name and by its .NET number written as a string, to its 1-based number in the translated regex.

```csharp
Pattern date = Pattern.FromRegex(@"(?<year>\d{4})-(\d{2})");

IReadOnlyDictionary<string, int> postgres = date.ToFlavor(RegexFlavor.PostgreSql).GroupNumbers;
// { ["year"] = 1, ["2"] = 1, ["1"] = 2 }
IReadOnlyDictionary<string, int> sqlite = date.ToFlavor(RegexFlavor.Sqlite).GroupNumbers;
// { ["year"] = 2, ["2"] = 2, ["1"] = 1 }
```

- In .NET, the unnamed `(\d{2})` is group 1 and `year` is group 2. In PostgreSQL, `year` comes first, so it is group 1.
- On SQLite, every group keeps its .NET number, because the engine is .NET's.
- The whole match (.NET group 0) is always group 0 and is not in the dictionary.
- On PostgreSQL, a group inside a lookaround cannot capture and is left out: `(?=(a))(b)` translates to `(?=(?:a))(b)` with `{ ["2"] = 1 }`. MySQL keeps both: `{ ["1"] = 1, ["2"] = 2 }`.

Use `GroupNumbers` when you extract a group in SQL, for example with `regexp_substr(..., n)` on PostgreSQL or `REGEXP_SUBSTR(..., n)` on SQL Server.

## Match extent and captures

`IsExact` is about **which strings match**. When you extract or replace text, it also matters **which substring** a match covers and **what each group captures**, and the engines resolve choices differently. `MatchExtentWarnings` lists every difference that can occur, and `PreservesMatchExtent` is true when there is none.

```csharp
FlavorConversion postgres = Pattern.FromRegex("(a|ab)").ToFlavor(RegexFlavor.PostgreSql);
bool isExact = postgres.IsExact;                     // true: the same strings contain a match
bool preservesExtent = postgres.PreservesMatchExtent; // false: on "ab", .NET matches "a", PostgreSQL "ab"

bool ranges = Pattern.FromRegex(@"(\d+)-(\d+)").ToFlavor(RegexFlavor.PostgreSql).PreservesMatchExtent; // true
bool endOfLine = Pattern.FromRegex("^a$").ToFlavor(RegexFlavor.SqlServer).PreservesMatchExtent;        // false
bool endOfText = Pattern.FromRegex(@"^a\z").ToFlavor(RegexFlavor.SqlServer).PreservesMatchExtent;      // true
bool canBeEmpty = Pattern.FromRegex("a*").ToFlavor(RegexFlavor.MySql).PreservesMatchExtent;            // false
```

Each warning explains the difference in plain words:

```csharp
foreach (string warning in Pattern.FromRegex("^a$").ToFlavor(RegexFlavor.SqlServer).MatchExtentWarnings)
{
    Console.WriteLine(warning);
}
// prints (one line):
// '$' or '\Z' at position 2 is written '\n?\z', which consumes a final newline that .NET leaves outside the match: on "a\n", replacing a$ gives "X" in SQL Server and "X\n" in .NET.
```

| Engine | When the extent can differ |
|--------|----------------------------|
| PostgreSQL | It takes the longest (or shortest) match, decided by the first quantifier, while .NET takes the first alternative that succeeds: `(a\|ab)` replaced in `ab` gives `X` (.NET `Xb`). Groups also reset on every repeat iteration: `(?:(a)\|b)+` on `ab` leaves group 1 unset (.NET `a`). |
| MySQL | A pattern that can match the empty string: `REGEXP_REPLACE` returns an empty input unchanged. Also a group in a repeat whose body can match empty text. |
| MariaDB | A pattern that can match the empty string: `REGEXP_REPLACE` never replaces an empty match and stops at the first one (`REGEXP_REPLACE('abc', '^', '-')` is `abc`). Also a group in a repeat whose body can match empty text. |
| SQL Server | Every `$` and `\Z`, because `\n?\z` consumes the final newline. Also a repeat whose body can match empty text: `(a?)+` on `aa` captures `a` in RE2 and an empty string in .NET. |
| SQLite | Never: it runs .NET's own engine. |

- The analysis is conservative. A warning means a difference is **possible**, not that it happens for every input. No warning means no difference.
- Warnings never affect a plain test with `~`, `REGEXP_LIKE` or `REGEXP`.
- The [Entity Framework Core](./entity-framework-core.md) package refuses to translate group extraction and replacement when `PreservesMatchExtent` is false.
- On SQL Server, anchor with `\z` instead of `$` when the extent matters and the data has no trailing newline.

## Unsupported constructs and errors

### Exceptions

| Exception | When |
|-----------|------|
| `NotSupportedException` | The pattern uses constructs the engine cannot express with the same meaning. The message lists all of them, each with its position in the .NET pattern. |
| `ArgumentNullException` | `ToSqlCondition` got a null `columnSql`. |
| `ArgumentException` | `ToSqlCondition` got a flavor that is not a SQL flavor: `RegexFlavor.JavaScript`, `RegexFlavor.JsonSchema` or an undefined value. The message names it, for example "JavaScript is not a SQL flavor; use ToFlavor for it." |
| `ArgumentOutOfRangeException` | `ToFlavor` got a value that is not a defined `RegexFlavor` ("Unknown regex flavor."). |

```csharp
Pattern pattern = Pattern.FromRegex(@"(?=a)(b)\1(?>c)");

try
{
    pattern.ToFlavor(RegexFlavor.SqlServer);
}
catch (NotSupportedException ex)
{
    Console.WriteLine(ex.Message);
}
// prints:
// The pattern cannot be converted to SQL Server without changing its meaning:
//   - lookaround '(?=...)' at position 0
//   - backreference at position 8
//   - atomic group '(?>...)' at position 10
```

The translator visits the whole pattern before it throws, so you see every problem at once.

### What each engine rejects

| Construct | PostgreSQL | MySQL | MariaDB | SQL Server | SQLite |
|-----------|:----------:|:-----:|:-------:|:----------:|:------:|
| `RegexOptions.RightToLeft` | No | No | No | No | No |
| Lookahead, lookbehind | Yes | Bounded lookbehind | Fixed-length lookbehind branches | No | No |
| Backreferences | Yes, with limits | Yes | Yes | No | No |
| Case-insensitive backreference | No | No | No | No | No |
| Atomic groups `(?>...)` | No | Yes | Yes | No | No |
| Conditionals, balancing groups, `\G` | No | No | No | No | No |
| Unicode `\b` | Yes | Yes | Yes | No | Yes |
| `$` or `\Z` followed by more pattern | Yes | Yes | Yes | No | Yes |
| Largest repeat count | 255 (larger repeats of one character or class are split) | 16,777,215 | 65,535 | 1,000 | As in .NET |
| Longest translated pattern | No limit | No limit | No limit | 4,000 | No limit |
| Group names kept | None (all numbered) | ASCII letters and digits, starting with a letter | Up to 32 ASCII letters, digits, `_` | ASCII letters, digits, `_` | All |

A group whose name the engine does not accept is not an error: it is written as a numbered group, and `GroupNumbers` still finds it by name.

Some rules apply to the four regex-engine flavors (PostgreSQL, MySQL, MariaDB and SQL Server). SQLite runs .NET's own engine, so it accepts duplicate group names, lone surrogates and empty classes exactly as .NET does:

- **Quantified zero-width constructs** such as `(?=a)*` are unsupported. PostgreSQL and ICU reject them.
- **A backreference to an undefined group, or to a group that does not entirely precede it** (`\1(a)`), is unsupported.
- **Duplicate group names**, such as `(?<x>a)|(?<x>b)`, are unsupported.
- **A lone surrogate** in the pattern is unsupported, because every engine matches whole code points. This includes a quantified emoji such as `😀+`: in .NET, the `+` repeats only the second half of the surrogate pair.
- **A class that matches nothing**, or only lone surrogates, is unsupported.
- **Under `RegexOptions.ECMAScript`**, a backreference to a group that may not have participated (`^(a)?\1b$`) is unsupported: .NET then matches the empty string, while every database engine fails.

### Falling back to .NET

When a pattern cannot run in the database, filter the rows in .NET with `IsMatch` instead. A small helper keeps that decision in one place:

```csharp
static string? TryToSqlCondition(Pattern pattern, string columnSql, RegexFlavor flavor)
{
    try
    {
        return pattern.ToSqlCondition(columnSql, flavor);
    }
    catch (NotSupportedException)
    {
        return null; // fall back to filtering in .NET with pattern.IsMatch
    }
}

string? password = TryToSqlCondition(CommonPatterns.StrongPassword(), "[Password]", RegexFlavor.SqlServer);
// null: StrongPassword uses lookaheads
string? color = TryToSqlCondition(CommonPatterns.HexColor(DigitMode.Ascii), "\"Color\"", RegexFlavor.PostgreSql);
// "Color" ~ '\A#([0-9A-Fa-f]{8}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{4}|[0-9A-Fa-f]{3})(?=\n?\Z)'
```

If you only need a coarse prefilter that an index can use, see [SQL LIKE](./sql-like.md), which compiles a pattern to `LIKE` and proves whether it is exact or a superset.

## Supplementary characters

The exactness promise covers input text in the Basic Multilingual Plane (U+0000 to U+FFFF). The engines match **code points**, while .NET sees a supplementary character, such as an emoji or U+1D7CE MATHEMATICAL BOLD DIGIT ZERO, as two UTF-16 code units. For input that contains such characters:

- `.`, negated classes and `\P{..}` match the character once in the database, and twice (one half at a time) in .NET.
- `\d`, `\w` and `\p{..}` can match a supplementary digit or letter that .NET does not.
- `\b` can see a boundary next to such a character where .NET does not.

A supplementary character **written in the pattern** is translated as one code point, so it matches exactly:

```csharp
string emoji = Pattern.FromRegex("😀").ToFlavor(RegexFlavor.PostgreSql).Pattern;   // \U0001F600
```

## Common patterns in SQL

All 30 parameterless `CommonPatterns` members, and every strictness of `Email` and `Url`, translate to PostgreSQL, MySQL and MariaDB. On SQL Server and SQLite, these throw:

| Member | Why it cannot run on SQL Server or SQLite |
|--------|-------------------------------------------|
| `StrongPassword()` | Four lookaheads, one per character rule. |
| `Base64()` | A lookahead that rejects the empty string. |
| `MarkdownLink()` | A negative lookbehind that skips images (`![...]`). |
| `MacAddress()` | A backreference that requires the same separator throughout. |
| `Email(PatternStrictness.Strict)` | Lookaheads for the length limits. `Loose` and `Standard` translate. |
| Every other member | Translates. |

Match extent, for extraction and replacement, is preserved by 22 of the 30 members on PostgreSQL, 29 on MySQL and MariaDB (`Base64` can match the empty string), 26 of the 26 that translate on SQLite, and only `HtmlTag` on SQL Server, because the others end with `$`.

```csharp
string address = CommonPatterns.IPv4(DigitMode.Ascii).ToSqlCondition("[Address]", RegexFlavor.SqlServer);
// REGEXP_LIKE([Address], N'\A(?:(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?))\.(?:(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?))\.(?:(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?))\.(?:(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?))\n?\z')
```

## From the command line

The `fluentregex` tool translates a regex without writing code. `--to` picks the flavor (`postgresql`, `mysql`, `mariadb`, `sqlserver`, `sqlite`; also `postgres`, `pg` and `mssql`). Without `--column` it prints the literal; with `--column` it prints the `WHERE` condition.

```bash
fluentregex export "^[0-9]+$" --to postgresql
# '\A[0-9]+(?=\n?\Z)'

fluentregex export "^[0-9]+$" --to mysql --column email
# REGEXP_LIKE(email, '(?-i)\\A[0-9]+(?=\\n?\\z)')

fluentregex export "^[0-9]+$" --to sqlserver --column "[Code]"
# REGEXP_LIKE([Code], N'\A[0-9]+\n?\z')
```

Two more options change the pattern before it is translated:

```bash
fluentregex export "^hello$" --to mysql --options IgnoreCase
# '(?-i)\\A[Hh][Ee][Ll][Ll][Oo](?=\\n?\\z)'

fluentregex export "^\d{4}$" --to postgresql --ascii-digits
# '\A[0-9]{4}(?=\n?\Z)'
```

- `--options` takes a comma-separated list of `RegexOptions` names, such as `IgnoreCase,CultureInvariant`.
- `--ascii-digits` applies `WithAsciiDigits()`, so `\d` becomes `[0-9]`.
- An unsupported pattern prints the same list as the exception, prefixed with `error:`, and exits with code 2.
- The tool does not print `MatchExtentWarnings`. Call `ToFlavor` from code when you need them.

See [Command-Line Tool](./command-line-tool.md).

## Best practices

- **Translate once.** `ToFlavor` and `ToSqlCondition` parse and translate the pattern on every call. Store the result, for example in a `static readonly` field, when the same query runs often.
- **Prefer parameters.** Pass `FlavorConversion.Pattern` as a parameter. Use `ToSqlCondition` for SQL text you write at build time or for quick scripts.
- **Never pass user input as `columnSql`.** It is inserted verbatim.
- **Use ASCII digits when the data allows it.** `WithAsciiDigits()` and `DigitMode.Ascii` keep PostgreSQL patterns small and SQL Server patterns under 4,000 characters.
- **Check `PreservesMatchExtent` before extracting or replacing** in SQL. Plain filtering never needs it.
- **Fix the culture** with `RegexOptions.CultureInvariant` when case-insensitive patterns are translated on servers with different cultures.
- **Test against your real engine** for anything critical. The translation targets PostgreSQL 17, MySQL 8.4, MariaDB 11.4 (PCRE2 10.42) and SQL Server 2025 behavior.

## How the translations were verified

During development, a harness compared .NET's `IsMatch` with live database engines in containers (PostgreSQL 17, MySQL 8.4, MariaDB 11.4 and SQL Server 2025). It used matching and non-matching samples for every `CommonPatterns` variant and hundreds of hand-picked constructs: anchors, trailing newlines, case folding with the Kelvin sign and U+0130, Unicode digits, `\b`, lookarounds, backreferences, large repeats and escapes. MySQL and MariaDB were checked six ways each: against a bound parameter and against columns in five collations.

- PostgreSQL: 3,270 checks, 0 disagreements.
- SQL Server: 2,736 checks, 0 disagreements.
- MySQL: 194,994 checks, 0 disagreements on BMP input.
- MariaDB: 192,222 checks, 0 disagreements on BMP input.

A second, differential harness compared acceptance, the matched substring, replace-all results and every group on about 2,600 random patterns: 344,355 comparisons in all. No pair that `IsExact` promised differed in acceptance, and no pair that `PreservesMatchExtent` promised differed in substring, replacement or captures.

:::note

These numbers come from the project's development notes. The library's unit tests do not need a database, and this page's samples were run against the library only (plus SQLite for [Raw SQL and Dapper](#raw-sql-and-dapper)).

:::

## See also

- [Entity Framework Core](./entity-framework-core.md): use these translations from LINQ with `Regex.IsMatch` or a `Pattern`.
- [SQL LIKE](./sql-like.md): compile a pattern to `LIKE`, with a proof of exactness.
- [Exporting Patterns](./export.md): JavaScript, TypeScript and JSON Schema.
- [Unicode and Scripts](./unicode.md): digits, categories and diacritics.
- [Common Patterns](./common-patterns.md): the ready-made patterns used on this page.
