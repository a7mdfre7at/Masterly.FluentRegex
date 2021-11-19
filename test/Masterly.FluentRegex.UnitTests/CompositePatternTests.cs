using FluentAssertions;
using Xunit;

namespace Masterly.FluentRegex.UnitTests
{
    public class CompositePatternTests
    {
        [Fact]
        public void Group()
        {
            Pattern pattern1 = Pattern.With
                                            .Digit
                                            .Word;

            Pattern pattern2 = Pattern.With.Group(pattern1);

            pattern2.ToString().Should().Be(@"(\d\w)");
        }

        [Fact]
        public void Set()
        {
            Pattern pattern = Pattern.With.Set(Pattern.With
                                                        .Digit
                                                        .Word);

            pattern.ToString().Should().Be(@"[\d\w]");
        }

        [Fact]
        public void NegatedSet()
        {
            Pattern pattern = Pattern.With.NegatedSet(Pattern.With
                                                               .Digit
                                                               .Word);

            pattern.ToString().Should().Be(@"[^\d\w]");
        }

        [Fact]
        public void Choice_With2Options()
        {
            Pattern pattern = Pattern.With.Choice(Pattern.With.Digit.Repeat.Times(3),
                                                    Pattern.With.Whitespace);

            pattern.ToString().Should().Be("(\\d{3}|\\s)");
        }

        [Fact]
        public void Choice_With3Options()
        {
            Pattern pattern = Pattern.With.Choice(Pattern.With.Digit.Repeat.Times(3),
                                            Pattern.With.Whitespace,
                                            Pattern.With.Literal("a"));

            pattern.ToString().Should().Be("(\\d{3}|\\s|a)");
        }

        [Fact]
        public void Email()
        {
            string regex = @"^[a-zA-Z\d\.-_]+@[a-zA-Z\d\.-]+\.[a-zA-Z]{2,4}$";

            Pattern pattern = Pattern.With
                               .StartOfLine
                               .Set(Pattern.With.Letter.Digit.Literal(".-_")).Repeat.OneOrMore
                               .Literal("@")
                               .Set(Pattern.With.Letter.Digit.Literal(".-")).Repeat.OneOrMore
                               .Literal(".")
                               .Set(Pattern.With.Letter).Repeat.Times(2, 4)
                               .EndOfLine;

            pattern.ToString().Should().Be(regex);
        }

        [Fact]
        public void SSN()
        {
            string regex = @"^\d{3}-?\d{2}-?\d{4}$";

            Pattern pattern = Pattern.With
                               .StartOfLine
                               .Digit.Repeat.Times(3)
                               .Literal("-").Repeat.Optional
                               .Digit.Repeat.Times(2)
                               .Literal("-").Repeat.Optional
                               .Digit.Repeat.Times(4)
                               .EndOfLine;
            
            pattern.ToString().Should().Be(regex);
        }
    }
}
