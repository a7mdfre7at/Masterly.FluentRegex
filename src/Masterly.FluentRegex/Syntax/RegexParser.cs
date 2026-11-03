using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Syntax
{
    /// <summary>Thrown when a pattern cannot be parsed.</summary>
    internal sealed class RegexSyntaxException : Exception
    {
        public RegexSyntaxException(string message, int position)
            : base(message + " (at position " + position.ToString(CultureInfo.InvariantCulture) + ")") => Position = position;

        public int Position { get; }
    }

    /// <summary>
    /// A recursive-descent parser for .NET regular expression syntax that produces a
    /// <see cref="RegexTree"/> with exact source spans.
    /// </summary>
    internal sealed class RegexParser
    {
        private readonly string _pattern;
        private readonly List<GroupNode> _groups = new List<GroupNode>();
        private int _pos;
        private RegexOptions _options;
        private readonly RegexOptions _initialOptions;
        private HashSet<int> _captureSlots;

        private RegexParser(string pattern, RegexOptions options)
        {
            _pattern = pattern;
            _options = options;
            _initialOptions = options;
        }

        public static RegexTree Parse(string pattern, RegexOptions options = RegexOptions.None)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            var parser = new RegexParser(pattern, options);
            RegexNode root = parser.ParseAlternation();
            if (parser._pos < pattern.Length)
            {
                if (pattern[parser._pos] == ')') throw new RegexSyntaxException("Too many ')'", parser._pos);
                throw new RegexSyntaxException("Unexpected character '" + pattern[parser._pos] + "'", parser._pos);
            }
            parser.AssignGroupNumbers(options);
            return new RegexTree(pattern, options, root, parser._groups);
        }

        public static bool TryParse(string pattern, RegexOptions options, out RegexTree tree)
        {
            try
            {
                tree = Parse(pattern, options);
                return true;
            }
            catch (RegexSyntaxException)
            {
                tree = null;
                return false;
            }
        }

        /// <summary>
        /// Splits raw regex text into everything before its final atom and the final atom itself,
        /// so a quantifier mode can be applied to the atom alone.
        /// </summary>
        public static bool TrySplitLastAtom(string text, out string prefix, out string lastAtom)
        {
            prefix = string.Empty;
            lastAtom = text;
            if (!TryParse(text, RegexOptions.None, out RegexTree tree)) return false;

            RegexNode last = tree.Root;
            while (true)
            {
                if (last is SequenceNode sequence && sequence.Items.Count > 0) last = sequence.Items[sequence.Items.Count - 1];
                else if (last is AlternationNode alternation) last = alternation.Branches[alternation.Branches.Count - 1];
                else break;
            }

            if (last is QuantifierNode || last is EmptyNode) return false;
            prefix = text.Substring(0, last.Start);
            lastAtom = text.Substring(last.Start, last.End - last.Start);
            return lastAtom.Length > 0;
        }

        // ------------------------------------------------------------------
        // Structure
        // ------------------------------------------------------------------

        private RegexNode ParseAlternation()
        {
            int start = _pos;
            RegexOptions options = _options;
            var branches = new List<RegexNode> { ParseSequence() };
            while (_pos < _pattern.Length && _pattern[_pos] == '|')
            {
                _pos++;
                branches.Add(ParseSequence());
            }
            if (branches.Count == 1) return branches[0];
            return new AlternationNode(_pattern, start, _pos, options, branches);
        }

        private RegexNode ParseSequence()
        {
            int start = _pos;
            RegexOptions options = _options;
            var items = new List<RegexNode>();

            while (true)
            {
                SkipIgnoredWhitespace();
                if (_pos >= _pattern.Length) break;
                char c = _pattern[_pos];
                if (c == '|' || c == ')') break;

                RegexNode atom = ParseAtom();
                if (atom == null) continue;

                SkipIgnoredWhitespace();
                atom = ParseQuantifier(atom);
                items.Add(atom);
            }

            if (items.Count == 0) return new EmptyNode(_pattern, start, options);
            if (items.Count == 1) return items[0];
            return new SequenceNode(_pattern, items[0].Start, items[items.Count - 1].End, options, items);
        }

        private void SkipIgnoredWhitespace()
        {
            if ((_options & RegexOptions.IgnorePatternWhitespace) == 0) return;
            while (_pos < _pattern.Length)
            {
                char c = _pattern[_pos];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f' || c == '\v')
                {
                    _pos++;
                }
                else if (c == '#')
                {
                    while (_pos < _pattern.Length && _pattern[_pos] != '\n') _pos++;
                }
                else
                {
                    break;
                }
            }
        }

        private RegexNode ParseQuantifier(RegexNode atom)
        {
            if (_pos >= _pattern.Length) return atom;
            int qStart = _pos;
            int min, max;
            char c = _pattern[_pos];

            if (c == '*') { min = 0; max = QuantifierNode.Unbounded; _pos++; }
            else if (c == '+') { min = 1; max = QuantifierNode.Unbounded; _pos++; }
            else if (c == '?') { min = 0; max = 1; _pos++; }
            else if (c == '{' && TryReadBraces(_pos, out min, out max, out int braceEnd)) { _pos = braceEnd; }
            else return atom;

            if (atom is AnchorNode || atom is InlineOptionsNode || atom is CommentNode)
            {
                // .NET accepts quantifiers on some zero-width items; keep them, they are harmless.
            }

            bool lazy = false;
            if (_pos < _pattern.Length && _pattern[_pos] == '?')
            {
                lazy = true;
                _pos++;
            }

            if (max != QuantifierNode.Unbounded && max < min)
                throw new RegexSyntaxException("Illegal {x,y} with x > y", qStart);

            string quantifierText = _pattern.Substring(qStart, _pos - qStart);
            var node = new QuantifierNode(_pattern, atom.Start, _pos, atom.Options, atom, min, max, lazy, quantifierText);

            SkipIgnoredWhitespace();
            if (_pos < _pattern.Length)
            {
                char next = _pattern[_pos];
                if (next == '*' || next == '+' || next == '?' || (next == '{' && TryReadBraces(_pos, out _, out _, out _)))
                    throw new RegexSyntaxException("Nested quantifier '" + next + "'", _pos);
            }
            return node;
        }

        private bool TryReadBraces(int at, out int min, out int max, out int end)
        {
            min = max = 0;
            end = at;
            int i = at + 1;
            int digitsStart = i;
            while (i < _pattern.Length && char.IsDigit(_pattern[i]) && _pattern[i] < 128) i++;
            if (i == digitsStart) return false;
            if (!int.TryParse(_pattern.Substring(digitsStart, i - digitsStart), NumberStyles.None, CultureInfo.InvariantCulture, out min)) return false;

            if (i < _pattern.Length && _pattern[i] == '}')
            {
                max = min;
                end = i + 1;
                return true;
            }

            if (i >= _pattern.Length || _pattern[i] != ',') return false;
            i++;
            int maxStart = i;
            while (i < _pattern.Length && char.IsDigit(_pattern[i]) && _pattern[i] < 128) i++;
            if (i >= _pattern.Length || _pattern[i] != '}') return false;

            if (i == maxStart) max = QuantifierNode.Unbounded;
            else if (!int.TryParse(_pattern.Substring(maxStart, i - maxStart), NumberStyles.None, CultureInfo.InvariantCulture, out max)) return false;

            end = i + 1;
            return true;
        }

        // ------------------------------------------------------------------
        // Atoms
        // ------------------------------------------------------------------

        private RegexNode ParseAtom()
        {
            int start = _pos;
            char c = _pattern[_pos];
            switch (c)
            {
                case '(':
                    return ParseGroup();
                case '[':
                    return ParseBracketClass();
                case '.':
                    _pos++;
                    return new CharClassNode(_pattern, start, _pos, _options, ClassKind.Any);
                case '^':
                    _pos++;
                    return new AnchorNode(_pattern, start, _pos, _options, AnchorKind.StartOfLine);
                case '$':
                    _pos++;
                    return new AnchorNode(_pattern, start, _pos, _options, AnchorKind.EndOfLine);
                case '\\':
                    return ParseEscape();
                case '*':
                case '+':
                case '?':
                    throw new RegexSyntaxException("Quantifier '" + c + "' following nothing", _pos);
                case '{':
                    if (TryReadBraces(_pos, out _, out _, out _))
                        throw new RegexSyntaxException("Quantifier '{' following nothing", _pos);
                    _pos++;
                    return new LiteralNode(_pattern, start, _pos, _options, c);
                default:
                    _pos++;
                    return new LiteralNode(_pattern, start, _pos, _options, c);
            }
        }

        private RegexNode ParseEscape()
        {
            int start = _pos;
            _pos++; // backslash
            if (_pos >= _pattern.Length) throw new RegexSyntaxException("Illegal \\ at end of pattern", start);
            char c = _pattern[_pos];

            switch (c)
            {
                case 'd': _pos++; return new CharClassNode(_pattern, start, _pos, _options, ClassKind.Digit);
                case 'D': _pos++; return new CharClassNode(_pattern, start, _pos, _options, ClassKind.NotDigit);
                case 'w': _pos++; return new CharClassNode(_pattern, start, _pos, _options, ClassKind.Word);
                case 'W': _pos++; return new CharClassNode(_pattern, start, _pos, _options, ClassKind.NotWord);
                case 's': _pos++; return new CharClassNode(_pattern, start, _pos, _options, ClassKind.Space);
                case 'S': _pos++; return new CharClassNode(_pattern, start, _pos, _options, ClassKind.NotSpace);
                case 'p':
                case 'P':
                {
                    string category = ReadCategoryName();
                    return new CharClassNode(_pattern, start, _pos, _options, c == 'p' ? ClassKind.Category : ClassKind.NotCategory, category);
                }
                case 'b': _pos++; return new AnchorNode(_pattern, start, _pos, _options, AnchorKind.WordBoundary);
                case 'B': _pos++; return new AnchorNode(_pattern, start, _pos, _options, AnchorKind.NonWordBoundary);
                case 'A': _pos++; return new AnchorNode(_pattern, start, _pos, _options, AnchorKind.StartOfInput);
                case 'z': _pos++; return new AnchorNode(_pattern, start, _pos, _options, AnchorKind.EndOfInput);
                case 'Z': _pos++; return new AnchorNode(_pattern, start, _pos, _options, AnchorKind.EndOfInputOrNewline);
                case 'G': _pos++; return new AnchorNode(_pattern, start, _pos, _options, AnchorKind.ContiguousMatch);
                case 'k':
                {
                    _pos++;
                    if (_pos >= _pattern.Length || (_pattern[_pos] != '<' && _pattern[_pos] != '\''))
                        throw new RegexSyntaxException("Malformed \\k<...> named back reference", start);
                    char close = _pattern[_pos] == '<' ? '>' : '\'';
                    int nameStart = ++_pos;
                    while (_pos < _pattern.Length && _pattern[_pos] != close) _pos++;
                    if (_pos >= _pattern.Length) throw new RegexSyntaxException("Malformed \\k<...> named back reference", start);
                    string name = _pattern.Substring(nameStart, _pos - nameStart);
                    _pos++;
                    if (int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
                        return new BackreferenceNode(_pattern, start, _pos, _options, null, number);
                    return new BackreferenceNode(_pattern, start, _pos, _options, name, 0);
                }
            }

            if (c >= '1' && c <= '9')
            {
                int numStart = _pos;
                while (_pos < _pattern.Length && _pattern[_pos] >= '0' && _pattern[_pos] <= '9') _pos++;
                int number = int.Parse(_pattern.Substring(numStart, _pos - numStart), CultureInfo.InvariantCulture);

                // Like .NET (outside ECMAScript mode): several digits that name no group are an octal escape.
                if (_pos - numStart > 1 && c <= '7' && (_initialOptions & RegexOptions.ECMAScript) == 0 && !IsCaptureSlot(number))
                {
                    _pos = numStart;
                    return new LiteralNode(_pattern, start, ReadOctal(), _options, _octalValue);
                }

                return new BackreferenceNode(_pattern, start, _pos, _options, null, number);
            }

            _pos = start;
            char value = ReadEscapedChar(inClass: false);
            return new LiteralNode(_pattern, start, _pos, _options, value);
        }

        private char _octalValue;

        /// <summary>
        /// Reads up to three octal digits at the current position into <see cref="_octalValue"/>, keeping the low
        /// 8 bits as .NET does, and returns the position after them.
        /// </summary>
        private int ReadOctal()
        {
            int value = 0;
            int digits = 0;
            while (digits < 3 && _pos < _pattern.Length && _pattern[_pos] >= '0' && _pattern[_pos] <= '7')
            {
                value = (value * 8) + (_pattern[_pos++] - '0');
                digits++;
            }

            _octalValue = (char)(value & 0xFF);
            return _pos;
        }

        /// <summary>True when the pattern has a capture group with this number, as the .NET engine numbers them.</summary>
        private bool IsCaptureSlot(int number)
        {
            if (_captureSlots == null)
            {
                const RegexOptions parseOptions = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.ExplicitCapture
                    | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace | RegexOptions.RightToLeft
                    | RegexOptions.CultureInvariant;
                _captureSlots = new HashSet<int>();
                try
                {
                    foreach (int slot in new Regex(_pattern, _initialOptions & parseOptions).GetGroupNumbers())
                    {
                        _captureSlots.Add(slot);
                    }
                }
                catch (ArgumentException)
                {
                    // An invalid pattern is reported by the parser itself; treat every number as a backreference.
                    return true;
                }
            }

            return _captureSlots.Contains(number);
        }

        private string ReadCategoryName()
        {
            int start = _pos - 1;
            _pos++; // p or P
            if (_pos >= _pattern.Length || _pattern[_pos] != '{') throw new RegexSyntaxException("Malformed \\p{X} character escape", start);
            int nameStart = ++_pos;
            while (_pos < _pattern.Length && _pattern[_pos] != '}') _pos++;
            if (_pos >= _pattern.Length) throw new RegexSyntaxException("Incomplete \\p{X} character escape", start);
            string name = _pattern.Substring(nameStart, _pos - nameStart);
            _pos++;
            return name;
        }

        /// <summary>Reads a single-character escape starting at the backslash.</summary>
        private char ReadEscapedChar(bool inClass)
        {
            int start = _pos;
            _pos++; // backslash
            if (_pos >= _pattern.Length) throw new RegexSyntaxException("Illegal \\ at end of pattern", start);
            char c = _pattern[_pos++];
            switch (c)
            {
                case 't': return '\t';
                case 'n': return '\n';
                case 'r': return '\r';
                case 'f': return '\f';
                case 'v': return '\v';
                case 'e': return '\u001B';
                case 'a': return '\u0007';
                case 'b':
                    if (inClass) return '\b';
                    throw new RegexSyntaxException("\\b is not a character here", start);
                case 'x': return (char)ReadHex(2, start);
                case 'u': return (char)ReadHex(4, start);
                case 'c':
                {
                    if (_pos >= _pattern.Length) throw new RegexSyntaxException("Missing control character", start);
                    char control = char.ToUpperInvariant(_pattern[_pos++]);
                    if (control < '@' || control > '_') throw new RegexSyntaxException("Unrecognized control character", start);
                    return (char)(control - '@');
                }
                case '0':
                {
                    int value = 0;
                    int digits = 0;
                    while (digits < 2 && _pos < _pattern.Length && _pattern[_pos] >= '0' && _pattern[_pos] <= '7')
                    {
                        value = value * 8 + (_pattern[_pos++] - '0');
                        digits++;
                    }
                    return (char)value;
                }
            }

            if (inClass && c >= '1' && c <= '7')
            {
                // Octal escape inside a class.
                int value = c - '0';
                int digits = 1;
                while (digits < 3 && _pos < _pattern.Length && _pattern[_pos] >= '0' && _pattern[_pos] <= '7')
                {
                    value = value * 8 + (_pattern[_pos++] - '0');
                    digits++;
                }
                return (char)value;
            }

            if (char.IsLetterOrDigit(c) || c == '_')
                throw new RegexSyntaxException("Unrecognized escape sequence \\" + c, start);
            return c;
        }

        private int ReadHex(int digits, int start)
        {
            if (_pos + digits > _pattern.Length) throw new RegexSyntaxException("Insufficient hexadecimal digits", start);
            string hex = _pattern.Substring(_pos, digits);
            if (!int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int value))
                throw new RegexSyntaxException("Insufficient hexadecimal digits", start);
            _pos += digits;
            return value;
        }

        // ------------------------------------------------------------------
        // Character classes
        // ------------------------------------------------------------------

        private RegexNode ParseBracketClass()
        {
            int start = _pos;
            BracketClass bracket = ReadBracketBody();
            return new CharClassNode(_pattern, start, _pos, _options, ClassKind.Bracket, null, bracket);
        }

        private BracketClass ReadBracketBody()
        {
            int start = _pos;
            _pos++; // [
            var bracket = new BracketClass();
            if (_pos < _pattern.Length && _pattern[_pos] == '^')
            {
                bracket.Negated = true;
                _pos++;
            }

            bool first = true;
            while (true)
            {
                if (_pos >= _pattern.Length) throw new RegexSyntaxException("Unterminated [] set", start);
                char c = _pattern[_pos];

                if (c == ']' && !first)
                {
                    _pos++;
                    return bracket;
                }

                if (c == '-' && !first && _pos + 1 < _pattern.Length && _pattern[_pos + 1] == '[')
                {
                    _pos++;
                    bracket.Subtraction = ReadBracketBody();
                    if (_pos >= _pattern.Length || _pattern[_pos] != ']')
                        throw new RegexSyntaxException("A subtraction must be the last element in a character class", _pos);
                    _pos++;
                    return bracket;
                }

                first = false;
                int itemStart = _pos;
                ClassItem low = ReadClassAtom();

                // Quirk of .NET: an escaped hyphen never starts a range, so [\--/] is three literals.
                bool isRange = low.ShorthandKind == null && low.Source != @"\-" &&
                               _pos + 1 < _pattern.Length && _pattern[_pos] == '-' &&
                               _pattern[_pos + 1] != ']' && _pattern[_pos + 1] != '[';
                if (isRange)
                {
                    _pos++; // -
                    ClassItem high = ReadClassAtom();
                    if (high.ShorthandKind != null)
                        throw new RegexSyntaxException("Cannot include class " + high.Source + " in character range", itemStart);
                    if (high.Low < low.Low)
                        throw new RegexSyntaxException("[x-y] range in reverse order", itemStart);
                    bracket.Items.Add(ClassItem.Range(low.Low, high.Low, _pattern.Substring(itemStart, _pos - itemStart)));
                }
                else
                {
                    bracket.Items.Add(low);
                }
            }
        }

        private ClassItem ReadClassAtom()
        {
            int start = _pos;
            char c = _pattern[_pos];
            if (c != '\\')
            {
                _pos++;
                return ClassItem.Char(c, c.ToString());
            }

            if (_pos + 1 >= _pattern.Length) throw new RegexSyntaxException("Illegal \\ at end of pattern", start);
            char e = _pattern[_pos + 1];
            ClassKind? shorthand = null;
            switch (e)
            {
                case 'd': shorthand = ClassKind.Digit; break;
                case 'D': shorthand = ClassKind.NotDigit; break;
                case 'w': shorthand = ClassKind.Word; break;
                case 'W': shorthand = ClassKind.NotWord; break;
                case 's': shorthand = ClassKind.Space; break;
                case 'S': shorthand = ClassKind.NotSpace; break;
            }

            if (shorthand != null)
            {
                _pos += 2;
                return ClassItem.Shorthand(shorthand.Value, null, _pattern.Substring(start, 2));
            }

            if (e == 'p' || e == 'P')
            {
                _pos++;
                string category = ReadCategoryName();
                return ClassItem.Shorthand(e == 'p' ? ClassKind.Category : ClassKind.NotCategory, category, _pattern.Substring(start, _pos - start));
            }

            char value = ReadEscapedChar(inClass: true);
            return ClassItem.Char(value, _pattern.Substring(start, _pos - start));
        }

        // ------------------------------------------------------------------
        // Groups
        // ------------------------------------------------------------------

        private RegexNode ParseGroup()
        {
            int start = _pos;
            RegexOptions outerOptions = _options;
            _pos++; // (

            if (_pos < _pattern.Length && _pattern[_pos] == '?')
            {
                _pos++;
                if (_pos >= _pattern.Length) throw new RegexSyntaxException("Unrecognized grouping construct", start);
                char c = _pattern[_pos];

                switch (c)
                {
                    case ':': _pos++; return FinishGroup(start, GroupKind.NonCapturing, outerOptions, outerOptions);
                    case '=': _pos++; return FinishGroup(start, GroupKind.Lookahead, outerOptions, outerOptions);
                    case '!': _pos++; return FinishGroup(start, GroupKind.NegativeLookahead, outerOptions, outerOptions);
                    case '>': _pos++; return FinishGroup(start, GroupKind.Atomic, outerOptions, outerOptions);
                    case '#':
                    {
                        while (_pos < _pattern.Length && _pattern[_pos] != ')') _pos++;
                        if (_pos >= _pattern.Length) throw new RegexSyntaxException("Unterminated (?#...) comment", start);
                        _pos++;
                        return new CommentNode(_pattern, start, _pos, outerOptions);
                    }
                    case '(':
                        return ParseConditional(start, outerOptions);
                    case '<':
                        if (_pos + 1 < _pattern.Length && _pattern[_pos + 1] == '=')
                        {
                            _pos += 2;
                            return FinishGroup(start, GroupKind.Lookbehind, outerOptions, outerOptions);
                        }
                        if (_pos + 1 < _pattern.Length && _pattern[_pos + 1] == '!')
                        {
                            _pos += 2;
                            return FinishGroup(start, GroupKind.NegativeLookbehind, outerOptions, outerOptions);
                        }
                        return ParseNamedGroup(start, '>', outerOptions);
                    case '\'':
                        return ParseNamedGroup(start, '\'', outerOptions);
                }

                if (IsOptionChar(c) || c == '-')
                    return ParseOptions(start, outerOptions);

                throw new RegexSyntaxException("Unrecognized grouping construct", start);
            }

            bool explicitCapture = (_options & RegexOptions.ExplicitCapture) != 0;
            return FinishGroup(start, explicitCapture ? GroupKind.NonCapturing : GroupKind.Capturing, outerOptions, outerOptions, capturing: !explicitCapture);
        }

        private GroupNode FinishGroup(int start, GroupKind kind, RegexOptions outerOptions, RegexOptions innerOptions, bool capturing = false, string name = null, string balancing = null)
        {
            string openText = _pattern.Substring(start, _pos - start);
            _options = innerOptions;
            RegexNode body = ParseAlternation();
            _options = outerOptions;

            if (_pos >= _pattern.Length || _pattern[_pos] != ')')
                throw new RegexSyntaxException("Not enough )'s", start);
            _pos++;

            var group = new GroupNode(_pattern, start, _pos, outerOptions, kind, openText, body)
            {
                Name = name,
                BalancingName = balancing,
                Number = capturing ? -1 : 0
            };
            _groups.Add(group);
            return group;
        }

        private RegexNode ParseNamedGroup(int start, char close, RegexOptions outerOptions)
        {
            _pos++; // < or '
            int nameStart = _pos;
            while (_pos < _pattern.Length && _pattern[_pos] != close) _pos++;
            if (_pos >= _pattern.Length) throw new RegexSyntaxException("Invalid group name", start);
            string fullName = _pattern.Substring(nameStart, _pos - nameStart);
            _pos++; // > or '

            string name = fullName;
            string balancing = null;
            int dash = fullName.IndexOf('-');
            if (dash >= 0)
            {
                name = fullName.Substring(0, dash);
                balancing = fullName.Substring(dash + 1);
                if (balancing.Length == 0 || !IsValidName(balancing))
                    throw new RegexSyntaxException("Invalid group name", start);
            }

            if (name.Length > 0 && !IsValidName(name))
                throw new RegexSyntaxException("Invalid group name: Group names must begin with a word character", start);

            if (balancing != null)
                return FinishGroup(start, GroupKind.Balancing, outerOptions, outerOptions, capturing: name.Length > 0, name: name.Length > 0 ? name : null, balancing: balancing);

            if (name.Length == 0) throw new RegexSyntaxException("Invalid group name", start);
            return FinishGroup(start, GroupKind.Named, outerOptions, outerOptions, capturing: true, name: name);
        }

        private static bool IsValidName(string name)
        {
            foreach (char c in name)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_' || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.ConnectorPunctuation ||
                      CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark))
                    return false;
            }
            return name.Length > 0;
        }

        private static bool IsOptionChar(char c) => c == 'i' || c == 'm' || c == 'n' || c == 's' || c == 'x';

        private RegexNode ParseOptions(int start, RegexOptions outerOptions)
        {
            string on = string.Empty;
            string off = string.Empty;
            bool negate = false;
            while (_pos < _pattern.Length)
            {
                char c = _pattern[_pos];
                if (c == '-')
                {
                    negate = true;
                    _pos++;
                }
                else if (IsOptionChar(c))
                {
                    if (negate) off += c;
                    else on += c;
                    _pos++;
                }
                else
                {
                    break;
                }
            }

            if (_pos >= _pattern.Length) throw new RegexSyntaxException("Unrecognized grouping construct", start);
            RegexOptions newOptions = ApplyFlags(outerOptions, on, off);

            if (_pattern[_pos] == ')')
            {
                _pos++;
                _options = newOptions; // applies to the rest of the enclosing group
                return new InlineOptionsNode(_pattern, start, _pos, outerOptions, on, off);
            }

            if (_pattern[_pos] != ':') throw new RegexSyntaxException("Unrecognized grouping construct", start);
            _pos++;
            GroupNode group = FinishGroup(start, GroupKind.Options, outerOptions, newOptions);
            group.OptionsOn = on;
            group.OptionsOff = off;
            return group;
        }

        internal static RegexOptions ApplyFlags(RegexOptions options, string on, string off)
        {
            foreach (char c in on) options |= Flag(c);
            foreach (char c in off) options &= ~Flag(c);
            return options;
        }

        private static RegexOptions Flag(char c)
        {
            switch (c)
            {
                case 'i': return RegexOptions.IgnoreCase;
                case 'm': return RegexOptions.Multiline;
                case 'n': return RegexOptions.ExplicitCapture;
                case 's': return RegexOptions.Singleline;
                case 'x': return RegexOptions.IgnorePatternWhitespace;
                default: return RegexOptions.None;
            }
        }

        private RegexNode ParseConditional(int start, RegexOptions outerOptions)
        {
            // _pos is at the '(' that opens the condition.
            int conditionStart = _pos;
            string groupName = null;
            RegexNode condition = null;

            int close = _pattern.IndexOf(')', conditionStart + 1);
            string candidate = close > conditionStart ? _pattern.Substring(conditionStart + 1, close - conditionStart - 1) : null;
            bool isGroupTest = candidate != null && candidate.Length > 0 && !candidate.StartsWith("?", StringComparison.Ordinal) && IsValidName(candidate);

            if (isGroupTest)
            {
                groupName = candidate;
                _pos = close + 1;
            }
            else if (_pos + 1 < _pattern.Length && _pattern[_pos + 1] == '?')
            {
                condition = ParseGroup();
            }
            else
            {
                // (?(expression)yes|no) is an implicit positive lookahead.
                _pos++;
                RegexNode expression = ParseAlternation();
                if (_pos >= _pattern.Length || _pattern[_pos] != ')') throw new RegexSyntaxException("Not enough )'s", conditionStart);
                _pos++;
                condition = expression;
            }

            string openText = _pattern.Substring(start, _pos - start);
            int bodyStart = _pos;
            RegexNode body = ParseAlternation();
            if (_pos >= _pattern.Length || _pattern[_pos] != ')') throw new RegexSyntaxException("Not enough )'s", start);

            RegexNode yes;
            RegexNode no = null;
            if (body is AlternationNode alternation)
            {
                if (alternation.Branches.Count > 2) throw new RegexSyntaxException("Too many | in (?()|)", bodyStart);
                yes = alternation.Branches[0];
                no = alternation.Branches[1];
            }
            else
            {
                yes = body;
            }

            _pos++;
            return new ConditionalNode(_pattern, start, _pos, outerOptions, openText, groupName, condition, yes, no);
        }

        /// <summary>
        /// Numbers capturing groups the way .NET does: unnamed groups first, left to right,
        /// then named groups in order of first appearance. Numeric names keep their number.
        /// </summary>
        private void AssignGroupNumbers(RegexOptions options)
        {
            var capturing = new List<GroupNode>();
            foreach (GroupNode group in _groups)
            {
                if (group.Number == -1) capturing.Add(group);
            }
            capturing.Sort((a, b) => a.Start.CompareTo(b.Start));

            int next = 1;
            var used = new HashSet<int>();
            foreach (GroupNode group in capturing)
            {
                if (group.Name == null)
                {
                    group.Number = next;
                    used.Add(next);
                    next++;
                }
            }

            var namedNumbers = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (GroupNode group in capturing)
            {
                if (group.Name == null) continue;
                if (int.TryParse(group.Name, NumberStyles.None, CultureInfo.InvariantCulture, out int explicitNumber))
                {
                    group.Number = explicitNumber;
                    used.Add(explicitNumber);
                }
            }

            foreach (GroupNode group in capturing)
            {
                if (group.Name == null || group.Number != -1) continue;
                if (!namedNumbers.TryGetValue(group.Name, out int number))
                {
                    while (used.Contains(next)) next++;
                    number = next;
                    used.Add(number);
                    namedNumbers[group.Name] = number;
                }
                group.Number = number;
            }
        }
    }
}
