namespace Masterly.FluentRegex
{
    public class Repeat
	{
		private readonly Pattern _pattern;

        public Repeat(Pattern parentPattern) => _pattern = parentPattern;

        private Pattern AppendRegex(string rgx)
		{
			_pattern.RegEx(rgx);
			return _pattern;
		}

        public Pattern OneOrMore => AppendRegex("+");

        public Pattern ZeroOrMore => AppendRegex("*");

		public Pattern Optional => AppendRegex("?");

		public Pattern Times(int exactly) => AppendRegex(string.Format("{{{0}}}", exactly));

		public Pattern Times(int min, int max) => AppendRegex(string.Format("{{{0},{1}}}", min, max));

		public Pattern AtLeast(int x) => AppendRegex(string.Format("{{{0},}}", x));

		public Pattern AtMost(int x) => AppendRegex(string.Format("{{,{0}}}", x));
	}
}