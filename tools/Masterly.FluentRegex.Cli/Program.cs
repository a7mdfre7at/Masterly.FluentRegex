using System.Text.RegularExpressions;
using Masterly.FluentRegex;
using Masterly.FluentRegex.Cli;

return Cli.Run(args);

namespace Masterly.FluentRegex.Cli
{
    internal static class Cli
    {
        private const int Ok = 0;
        private const int Findings = 1;
        private const int UsageError = 2;

        private const string Usage = """
            fluentregex - companion tool for Masterly.FluentRegex

            Usage:
              fluentregex convert  <regex> [--options <list>]           Print fluent C# that builds the regex
              fluentregex convert  --file <path>                        Convert every regex literal in C# files
              fluentregex analyze  <regex> [--options <list>] [--confirm]
                                                                        Report catastrophic backtracking (ReDoS) risks
              fluentregex scan     <path> [--fail-on high|medium|none] [--confirm]
                                                                        Scan C# files for risky regex literals (CI friendly)
              fluentregex explain  <regex> [--options <list>]           Explain a regex in plain English
              fluentregex generate <regex> [--count N] [--seed N] [--non-matching] [--options <list>]
                                                                        Generate matching (or near-miss) test strings
              fluentregex trace    <regex> <input> [--options <list>]   Show which part matched what, or where it failed
              fluentregex diagram  <regex> [--format svg|mermaid] [--output <file>] [--options <list>]
                                                                        Draw a railroad diagram or Mermaid flowchart
              fluentregex compare  <regexA> <regexB> [--options <list>] Prove equivalence or show the shortest counterexample
              fluentregex optimize <regex> [--options <list>]           Simplify a regex with verified, semantics-preserving rewrites
              fluentregex learn    <example>... [--not <example>...]   Infer a regex from examples (or --from-file <path>, one per line)
              fluentregex profile  <regex> <input> [--options <list>]   Count backtracking steps and show the hotspots
              fluentregex highlight <regex> <file|-> [--format ansi|html|brackets] [--options <list>]
                                                                        Show matches and groups inside a text file
              fluentregex coverage <regex> <file|-> [--options <list>]  Report which branches and limits the lines of a file exercise
              fluentregex export   <regex> [--to js|json-schema|typescript|postgresql|mysql|mariadb|sqlserver|sqlite]
                                           [--column <sql>] [--name <identifier>] [--options <list>]
                                                                        Translate to JavaScript, JSON Schema, a TypeScript constant
                                                                        or a database's regex dialect (with --column, a full WHERE condition)
              fluentregex export   <regex> --to like [--dialect standard|sqlserver|postgresql|mysql|sqlite]
                                           [--scope substring|full] [--case-insensitive] [--column <sql>] [--options <list>]
                                                                        Compile to SQL LIKE and prove whether it is exact or a superset
              fluentregex facts    <regex> [--scope full|substring] [--options <list>]
                                                                        Prove length bounds and the required prefix and suffix of every match

            Options:
              --options <list>   Comma-separated RegexOptions, e.g. IgnoreCase,Multiline
              --ascii-digits     Make \d match only 0-9 (by default .NET's \d matches every Unicode digit)
              --help, -h         Show this help
              --version          Show the version

            Exit codes:
              0  success
              1  a finding: analyze (High risk), scan (findings at or above --fail-on), compare (not
                 equivalent), profile (step budget exhausted), coverage (something not covered)
              2  usage or input error
            """;

        public static int Run(string[] args)
        {
            // Counterexamples, heat bars and highlighted text are often non-ASCII.
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            try
            {
                if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
                {
                    Console.WriteLine(Usage);
                    return args.Length == 0 ? UsageError : Ok;
                }

                if (args[0] == "--version")
                {
                    Console.WriteLine(typeof(Pattern).Assembly.GetName().Version?.ToString(3));
                    return Ok;
                }

                var arguments = new Arguments(args.Skip(1).ToArray());
                return args[0] switch
                {
                    "convert" => Convert(arguments),
                    "analyze" => Analyze(arguments),
                    "scan" => Scan(arguments),
                    "explain" => Explain(arguments),
                    "generate" => Generate(arguments),
                    "trace" => Trace(arguments),
                    "diagram" => Diagram(arguments),
                    "compare" => Compare(arguments),
                    "optimize" => Optimize(arguments),
                    "learn" => Learn(args.Skip(1).ToArray()),
                    "profile" => Profile(arguments),
                    "highlight" => Highlight(arguments),
                    "coverage" => Coverage(arguments),
                    "export" => Export(arguments),
                    "facts" => Facts(arguments),
                    _ => Fail("Unknown command '" + args[0] + "'. Run 'fluentregex --help'.")
                };
            }
            catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or InvalidOperationException or FormatException or NotSupportedException)
            {
                return Fail(ex.Message);
            }
        }

        private static int Convert(Arguments args)
        {
            string? file = args.Option("file");
            if (file == null)
            {
                Console.WriteLine(Load(args).ToFluentCode());
                return Ok;
            }

            int converted = 0;
            int skipped = 0;
            foreach (string path in RegexLiteralFinder.CSharpFiles(file))
            {
                foreach (RegexLiteral literal in RegexLiteralFinder.Find(path))
                {
                    Console.WriteLine($"// {Relative(literal.File)}({literal.Line},{literal.Column}) {literal.Kind}: {literal.Pattern}");
                    try
                    {
                        Console.WriteLine(Pattern.FromRegex(literal.Pattern, literal.Options).ToFluentCode() + ";");
                        converted++;
                    }
                    catch (ArgumentException ex)
                    {
                        Console.WriteLine("// skipped: " + ex.Message);
                        skipped++;
                    }
                    Console.WriteLine();
                }
            }
            string skippedText = skipped > 0 ? $" {skipped} skipped as invalid." : string.Empty;
            Console.Error.WriteLine($"{converted} regex literal(s) converted.{skippedText}");
            return Ok;
        }

        private static int Analyze(Arguments args)
        {
            SafetyReport report = Load(args).AnalyzeSafety(confirm: args.Flag("confirm"));
            WriteColored(report.ToString(), RiskColor(report.Risk));
            return report.Risk >= RiskLevel.High ? Findings : Ok;
        }

        private static int Scan(Arguments args)
        {
            string path = args.Positional(0, "path");
            RiskLevel failOn = (args.Option("fail-on") ?? "high").ToLowerInvariant() switch
            {
                "high" => RiskLevel.High,
                "medium" => RiskLevel.Medium,
                "none" => (RiskLevel)int.MaxValue,
                var other => throw new ArgumentException("Unknown --fail-on value '" + other + "'. Use high, medium or none.")
            };
            bool confirm = args.Flag("confirm");

            int scanned = 0;
            var counts = new Dictionary<RiskLevel, int>();
            foreach (string file in RegexLiteralFinder.CSharpFiles(path))
            {
                foreach (RegexLiteral literal in RegexLiteralFinder.Find(file))
                {
                    scanned++;
                    SafetyReport report;
                    try
                    {
                        report = Pattern.FromRegex(literal.Pattern, literal.Options).AnalyzeSafety(confirm);
                    }
                    catch (ArgumentException ex)
                    {
                        WriteColored($"{Relative(literal.File)}({literal.Line},{literal.Column}): error: invalid regex: {ex.Message}", ConsoleColor.Red);
                        counts[RiskLevel.High] = counts.GetValueOrDefault(RiskLevel.High) + 1;
                        continue;
                    }

                    foreach (SafetyIssue issue in report.Issues.Where(i => i.Risk >= RiskLevel.Medium))
                    {
                        counts[issue.Risk] = counts.GetValueOrDefault(issue.Risk) + 1;
                        string severity = issue.Risk == RiskLevel.High ? "warning" : "info";
                        string confirmed = issue.Confirmed == true ? " [CONFIRMED by timeout]" : string.Empty;
                        WriteColored($"{Relative(literal.File)}({literal.Line},{literal.Column}): {severity} FRX-{issue.Kind}: {literal.Kind} \"{literal.Pattern}\"{confirmed}", RiskColor(issue.Risk));
                        Console.WriteLine("    " + issue.Description);
                        Console.WriteLine("    Fix: " + issue.Suggestion);
                    }
                }
            }

            int high = counts.GetValueOrDefault(RiskLevel.High);
            int medium = counts.GetValueOrDefault(RiskLevel.Medium);
            Console.WriteLine();
            Console.WriteLine($"Scanned {scanned} regex literal(s): {high} high, {medium} medium risk finding(s).");
            bool failed = (failOn <= RiskLevel.High && high > 0) || (failOn <= RiskLevel.Medium && medium > 0);
            return failed ? Findings : Ok;
        }

        private static int Explain(Arguments args)
        {
            Console.WriteLine(Load(args).Explain());
            return Ok;
        }

        private static int Generate(Arguments args)
        {
            Pattern pattern = Load(args);
            int count = int.Parse(args.Option("count") ?? "10", System.Globalization.CultureInfo.InvariantCulture);
            int? seed = args.Option("seed") is { } s ? int.Parse(s, System.Globalization.CultureInfo.InvariantCulture) : null;
            string[] samples = args.Flag("non-matching") ? pattern.GenerateNonMatching(count, seed) : pattern.GenerateMatching(count, seed);
            foreach (string sample in samples) Console.WriteLine(sample);
            return Ok;
        }

        private static int Trace(Arguments args)
        {
            Pattern pattern = Load(args);
            MatchTrace trace = pattern.Trace(args.Positional(1, "input"));
            WriteColored(trace.ToString(), trace.IsMatch ? ConsoleColor.Green : ConsoleColor.Yellow);
            return Ok;
        }

        private static int Diagram(Arguments args)
        {
            Pattern pattern = Load(args);
            string format = (args.Option("format") ?? "svg").ToLowerInvariant();
            string output = format switch
            {
                "svg" => pattern.ToRailroadSvg(),
                "mermaid" => pattern.ToMermaid(),
                _ => throw new ArgumentException("Unknown --format '" + format + "'. Use svg or mermaid.")
            };

            string? file = args.Option("output");
            if (file == null)
            {
                Console.WriteLine(output);
            }
            else
            {
                File.WriteAllText(file, output);
                Console.Error.WriteLine("Wrote " + file);
            }
            return Ok;
        }

        private static int Compare(Arguments args)
        {
            Pattern left = Load(args, 0, "regexA");
            Pattern right = Load(args, 1, "regexB");
            PatternComparison comparison = Pattern.Compare(left, right);
            bool equivalent = comparison.Relation == PatternRelation.Equivalent;
            WriteColored(comparison.ToString(), equivalent ? ConsoleColor.Green : ConsoleColor.Yellow);
            return equivalent ? Ok : Findings;
        }

        private static int Optimize(Arguments args)
        {
            OptimizationResult result = Load(args).Optimize();
            Console.WriteLine(result.ToString());
            return Ok;
        }

        private static int Learn(string[] rawArgs)
        {
            const string RejectMarker = "--not";
            const string FileOption = "--from-file";

            List<string> matches = new List<string>();
            List<string> rejects = new List<string>();
            List<string> target = matches;
            for (int i = 0; i < rawArgs.Length; i++)
            {
                if (rawArgs[i] == RejectMarker)
                {
                    target = rejects;
                }
                else if (rawArgs[i] == FileOption && i + 1 < rawArgs.Length)
                {
                    target.AddRange(File.ReadAllLines(rawArgs[++i]).Where(line => line.Length > 0));
                }
                else
                {
                    target.Add(rawArgs[i]);
                }
            }

            if (matches.Count == 0)
            {
                throw new ArgumentException("Give at least one example to learn from. Run 'fluentregex --help'.");
            }

            Pattern learned = Pattern.Learn(matches, rejects);
            Console.WriteLine(learned.ToString());
            Console.WriteLine();
            Console.WriteLine(learned.ToFluentCode());
            return Ok;
        }

        private static int Profile(Arguments args)
        {
            ProfileReport report = Load(args).Profile(args.Positional(1, "input"));
            WriteColored(report.ToString(), report.BudgetExhausted ? ConsoleColor.Red : ConsoleColor.Green);
            return report.BudgetExhausted ? Findings : Ok;
        }

        private static int Highlight(Arguments args)
        {
            Pattern pattern = Load(args);
            string text = ReadInput(args.Positional(1, "file"));
            string format = (args.Option("format") ?? (Console.IsOutputRedirected ? "brackets" : "ansi")).ToLowerInvariant();
            HighlightFormat highlightFormat = format switch
            {
                "ansi" => HighlightFormat.Ansi,
                "html" => HighlightFormat.Html,
                "brackets" => HighlightFormat.Brackets,
                _ => throw new ArgumentException($"Unknown --format '{format}'. Use ansi, html or brackets.")
            };
            Console.WriteLine(pattern.Highlight(text, highlightFormat));
            return Ok;
        }

        private static int Coverage(Arguments args)
        {
            Pattern pattern = Load(args);
            string text = ReadInput(args.Positional(1, "file")).TrimEnd('\r', '\n');
            string[] inputs = text.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
            CoverageReport report = pattern.MeasureCoverage(inputs);
            WriteColored(report.ToString(), report.Uncovered.Count == 0 ? ConsoleColor.Green : ConsoleColor.Yellow);
            return report.Uncovered.Count == 0 ? Ok : Findings;
        }

        private static int Export(Arguments args)
        {
            Pattern pattern = Load(args);
            string target = (args.Option("to") ?? "js").ToLowerInvariant();
            if (target == "typescript")
            {
                string name = args.Option("name") ?? "pattern";
                Dictionary<string, Pattern> patterns = new Dictionary<string, Pattern> { [name] = pattern };
                Console.WriteLine(PatternExport.ToTypeScript(patterns));
                return Ok;
            }

            if (target == "like")
            {
                return ExportLike(pattern, args);
            }

            RegexFlavor flavor = ParseFlavor(target);
            FlavorConversion conversion = pattern.ToFlavor(flavor);
            Console.WriteLine(ExportOutput(pattern, conversion, args.Option("column")));
            WriteWarnings(conversion.Warnings);
            return Ok;
        }

        private static RegexFlavor ParseFlavor(string target) => target switch
        {
            "js" or "javascript" => RegexFlavor.JavaScript,
            "json-schema" or "jsonschema" => RegexFlavor.JsonSchema,
            "postgresql" or "postgres" or "pg" => RegexFlavor.PostgreSql,
            "mysql" => RegexFlavor.MySql,
            "mariadb" => RegexFlavor.MariaDb,
            "sqlserver" or "mssql" => RegexFlavor.SqlServer,
            "sqlite" => RegexFlavor.Sqlite,
            _ => throw new ArgumentException($"Unknown --to '{target}'. Use js, json-schema, typescript, postgresql, mysql, mariadb, sqlserver, sqlite or like.")
        };

        /// <summary>The literal for JavaScript and SQL, the bare pattern for JSON Schema, or a WHERE condition when a column is given.</summary>
        private static string ExportOutput(Pattern pattern, FlavorConversion conversion, string? column)
        {
            if (conversion.IsSql)
            {
                return column == null ? conversion.ToLiteral() : pattern.ToSqlCondition(column, conversion.Flavor);
            }

            return conversion.Flavor == RegexFlavor.JavaScript ? conversion.ToLiteral() : conversion.Pattern;
        }

        private static int ExportLike(Pattern pattern, Arguments args)
        {
            LikeDialect dialect = ParseLikeDialect((args.Option("dialect") ?? "standard").ToLowerInvariant());
            MatchScope scope = ParseScope(args.Option("scope"), MatchScope.Substring);
            LikeCollation collation = args.Flag("case-insensitive") ? LikeCollation.CaseInsensitive : LikeCollation.Binary;
            LikeConversion like = pattern.ToLike(dialect, scope, collation);
            string? column = args.Option("column");
            Console.WriteLine(column == null ? like.Expression : like.ToSql(column));
            if (like.IsExact)
            {
                WriteColored("exact: LIKE accepts exactly the strings the pattern accepts (proven)", ConsoleColor.Green);
            }
            else
            {
                WriteColored("superset: LIKE accepts every string the pattern accepts and possibly more; keep the regex check", ConsoleColor.Yellow);
            }
            WriteWarnings(like.Warnings);
            return Ok;
        }

        private static LikeDialect ParseLikeDialect(string dialect) => dialect switch
        {
            "standard" or "ansi" => LikeDialect.Standard,
            "sqlserver" or "mssql" => LikeDialect.SqlServer,
            "postgresql" or "postgres" or "pg" => LikeDialect.PostgreSql,
            "mysql" or "mariadb" => LikeDialect.MySql,
            "sqlite" => LikeDialect.Sqlite,
            _ => throw new ArgumentException($"Unknown --dialect '{dialect}'. Use standard, sqlserver, postgresql, mysql or sqlite.")
        };

        private static int Facts(Arguments args)
        {
            Pattern pattern = Load(args);
            MatchScope scope = ParseScope(args.Option("scope"), MatchScope.FullString);
            Console.WriteLine(pattern.GetLanguageFacts(scope).ToString());
            return Ok;
        }

        private static MatchScope ParseScope(string? scope, MatchScope defaultScope) => scope?.ToLowerInvariant() switch
        {
            null => defaultScope,
            "full" or "fullstring" or "full-string" => MatchScope.FullString,
            "substring" or "search" => MatchScope.Substring,
            _ => throw new ArgumentException($"Unknown --scope '{scope}'. Use full or substring.")
        };

        private static void WriteWarnings(IReadOnlyList<string> warnings)
        {
            foreach (string warning in warnings)
            {
                WriteColored($"warning: {warning}", ConsoleColor.Yellow);
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static string ReadInput(string path) => path == "-" ? Console.In.ReadToEnd() : File.ReadAllText(path);

        private static Pattern Load(Arguments args) => Load(args, 0, "regex");

        private static Pattern Load(Arguments args, int index, string argumentName)
        {
            string regex = args.Positional(index, argumentName);
            RegexOptions options = RegexOptions.None;
            if (args.Option("options") is { } list)
            {
                foreach (string name in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!Enum.TryParse(name, ignoreCase: true, out RegexOptions flag))
                        throw new ArgumentException("Unknown regex option '" + name + "'.");
                    options |= flag;
                }
            }
            Pattern pattern = Pattern.FromRegex(regex, options);
            return args.Flag("ascii-digits") ? pattern.WithAsciiDigits() : pattern;
        }

        private static string Relative(string path)
        {
            string relative = Path.GetRelativePath(Environment.CurrentDirectory, path);
            return relative.StartsWith("..", StringComparison.Ordinal) ? path : relative;
        }

        private static ConsoleColor RiskColor(RiskLevel risk) => risk switch
        {
            RiskLevel.High => ConsoleColor.Red,
            RiskLevel.Medium => ConsoleColor.Yellow,
            _ => ConsoleColor.Green
        };

        private static void WriteColored(string text, ConsoleColor color)
        {
            if (Console.IsOutputRedirected)
            {
                Console.WriteLine(text);
                return;
            }
            ConsoleColor previous = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = previous;
        }

        private static int Fail(string message)
        {
            Console.Error.WriteLine("error: " + message);
            return UsageError;
        }
    }

    /// <summary>Minimal argument parser: positional values, --name value options and --flag switches.</summary>
    internal sealed class Arguments
    {
        private static readonly HashSet<string> Flags = new(StringComparer.Ordinal) { "confirm", "non-matching", "ascii-digits", "case-insensitive" };

        private readonly List<string> _positional = new();
        private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);

        public Arguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("--", StringComparison.Ordinal) && arg.Length > 2)
                {
                    string name = arg[2..];
                    if (Flags.Contains(name)) _options[name] = null;
                    else if (i + 1 < args.Length) _options[name] = args[++i];
                    else throw new ArgumentException("Option '" + arg + "' needs a value.");
                }
                else
                {
                    _positional.Add(arg);
                }
            }
        }

        public string Positional(int index, string name) =>
            index < _positional.Count ? _positional[index] : throw new ArgumentException("Missing <" + name + "> argument. Run 'fluentregex --help'.");

        public string? Option(string name) => _options.TryGetValue(name, out string? value) ? value : null;

        public bool Flag(string name) => _options.ContainsKey(name);
    }
}
