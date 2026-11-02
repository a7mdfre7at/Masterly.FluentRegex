using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Syntax
{
    /// <summary>
    /// The exact set of UTF-16 code units a single-character construct can match.
    /// Membership is computed by the real .NET regex engine, so every class, Unicode category,
    /// block, subtraction and case-insensitivity rule is honored without reimplementing them.
    /// </summary>
    internal sealed class CharSet
    {
        private const int Size = 0x10000;
        private const RegexOptions RelevantOptions = RegexOptions.IgnoreCase | RegexOptions.Singleline |
                                                     RegexOptions.CultureInvariant | RegexOptions.ECMAScript;

        private static readonly ConcurrentDictionary<string, CharSet> Cache = new ConcurrentDictionary<string, CharSet>(StringComparer.Ordinal);
        private static readonly Lazy<string> AllChars = new Lazy<string>(BuildAllChars);

        private readonly ulong[] _bits = new ulong[Size / 64];
        private int[] _members;
        private int[] _preferred;

        private CharSet() { }

        public static CharSet Empty { get; } = new CharSet();

        public int Count { get; private set; }

        public bool IsEmpty => Count == 0;

        public bool Contains(char c) => (_bits[c >> 6] & (1UL << (c & 63))) != 0;

        private void Add(int c)
        {
            ulong mask = 1UL << (c & 63);
            if ((_bits[c >> 6] & mask) == 0)
            {
                _bits[c >> 6] |= mask;
                Count++;
            }
        }

        public bool Overlaps(CharSet other)
        {
            if (other == null) return false;
            for (int i = 0; i < _bits.Length; i++)
            {
                if ((_bits[i] & other._bits[i]) != 0) return true;
            }
            return false;
        }

        public CharSet Union(CharSet other)
        {
            var result = new CharSet();
            for (int i = 0; i < _bits.Length; i++) result._bits[i] = _bits[i] | (other?._bits[i] ?? 0);
            result.RecountBits();
            return result;
        }

        public CharSet Intersect(CharSet other)
        {
            var result = new CharSet();
            for (int i = 0; i < _bits.Length; i++) result._bits[i] = _bits[i] & (other?._bits[i] ?? 0);
            result.RecountBits();
            return result;
        }

        /// <summary>Every UTF-16 code unit.</summary>
        public static CharSet All { get; } = BuildAll();

        /// <summary>Returns the set that contains only <paramref name="c"/>.</summary>
        public static CharSet Single(char c)
        {
            var result = new CharSet();
            result.Add(c);
            return result;
        }

        /// <summary>Returns the set of code units that are not in this set.</summary>
        public CharSet Complement()
        {
            var result = new CharSet();
            for (int i = 0; i < _bits.Length; i++) result._bits[i] = ~_bits[i];
            result.RecountBits();
            return result;
        }

        /// <summary>Returns the members of this set that are not in <paramref name="other"/>.</summary>
        public CharSet Except(CharSet other)
        {
            var result = new CharSet();
            for (int i = 0; i < _bits.Length; i++) result._bits[i] = _bits[i] & ~(other?._bits[i] ?? 0);
            result.RecountBits();
            return result;
        }

        /// <summary>Returns true when both sets have exactly the same members.</summary>
        public bool SetEquals(CharSet other)
        {
            if (other == null || other.Count != Count) return false;
            for (int i = 0; i < _bits.Length; i++)
            {
                if (_bits[i] != other._bits[i]) return false;
            }
            return true;
        }

        /// <summary>Returns true when every member of this set is also in <paramref name="other"/>.</summary>
        public bool IsSubsetOf(CharSet other)
        {
            if (other == null) return IsEmpty;
            for (int i = 0; i < _bits.Length; i++)
            {
                if ((_bits[i] & ~other._bits[i]) != 0) return false;
            }
            return true;
        }

        /// <summary>A hash of the members, consistent with <see cref="SetEquals"/>.</summary>
        public int ContentHash()
        {
            unchecked
            {
                int hash = Count;
                for (int i = 0; i < _bits.Length; i++)
                {
                    ulong word = _bits[i];
                    if (word != 0) hash = (hash * 31) ^ (int)word ^ (int)(word >> 32) ^ i;
                }
                return hash;
            }
        }

        /// <summary>Enumerates the members as maximal contiguous ranges, lowest first.</summary>
        public IEnumerable<KeyValuePair<char, char>> Ranges()
        {
            int c = 0;
            while (c < Size)
            {
                if (!Contains((char)c))
                {
                    c++;
                    continue;
                }
                int low = c;
                while (c + 1 < Size && Contains((char)(c + 1))) c++;
                yield return new KeyValuePair<char, char>((char)low, (char)c);
                c++;
            }
        }

        private static CharSet BuildAll()
        {
            var result = new CharSet();
            for (int i = 0; i < result._bits.Length; i++) result._bits[i] = ulong.MaxValue;
            result.Count = Size;
            return result;
        }

        private void RecountBits()
        {
            int count = 0;
            foreach (ulong word in _bits)
            {
                ulong w = word;
                while (w != 0)
                {
                    w &= w - 1;
                    count++;
                }
            }
            Count = count;
        }

        /// <summary>
        /// Returns a representative member, preferring printable ASCII so generated samples stay readable.
        /// </summary>
        public char Pick(Random random)
        {
            EnsureMembers();
            if (_members.Length == 0) throw new InvalidOperationException("The character set is empty.");
            if (_preferred.Length > 0 && random.NextDouble() < 0.92) return (char)_preferred[random.Next(_preferred.Length)];
            return (char)_members[random.Next(_members.Length)];
        }

        /// <summary>Returns the first member, preferring printable ASCII.</summary>
        public char First()
        {
            EnsureMembers();
            if (_preferred.Length > 0) return (char)_preferred[0];
            if (_members.Length > 0) return (char)_members[0];
            throw new InvalidOperationException("The character set is empty.");
        }

        public IEnumerable<char> PreferredMembers()
        {
            EnsureMembers();
            foreach (int c in _preferred) yield return (char)c;
        }

        private void EnsureMembers()
        {
            if (_members != null) return;
            var members = new List<int>();
            var preferred = new List<int>();
            for (int c = 0; c < Size; c++)
            {
                if (!Contains((char)c)) continue;
                if (c >= 0xD800 && c <= 0xDFFF) continue; // lone surrogates make poor samples
                members.Add(c);
                if (c >= 0x20 && c <= 0x7E) preferred.Add(c);
            }
            _preferred = preferred.ToArray();
            _members = members.ToArray();
        }

        /// <summary>
        /// Computes the set for a literal or character-class node. Returns null for nodes that
        /// are not single-character constructs.
        /// </summary>
        public static CharSet ForNode(RegexNode node)
        {
            switch (node)
            {
                case LiteralNode literal:
                    return ForSource(LiteralSource(literal.Value), literal.Options);
                case CharClassNode charClass:
                    return ForSource(charClass.Source, charClass.Options);
                default:
                    return null;
            }
        }

        public static CharSet ForSource(string classSource, RegexOptions options)
        {
            options &= RelevantOptions;
            string key = ((int)options).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + classSource;
            return Cache.GetOrAdd(key, _ => Compute(classSource, options));
        }

        private static string LiteralSource(char c)
        {
            // \uXXXX is valid regex for any code unit and is never affected by x-mode.
            return @"\u" + ((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static CharSet Compute(string classSource, RegexOptions options)
        {
            var set = new CharSet();
            Regex regex;
            try
            {
                regex = new Regex("(?:" + classSource + ")", options);
            }
            catch (ArgumentException)
            {
                return set;
            }

            for (Match m = regex.Match(AllChars.Value); m.Success; m = m.NextMatch())
            {
                if (m.Length == 1) set.Add(m.Index);
            }
            return set;
        }

        private static string BuildAllChars()
        {
            var sb = new StringBuilder(Size);
            for (int c = 0; c < Size; c++) sb.Append((char)c);
            return sb.ToString();
        }
    }
}
