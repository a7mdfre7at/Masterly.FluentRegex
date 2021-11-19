using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Masterly.FluentRegex.UnitTests
{
    public class SimplePatternTests
    {
		[Fact]
		public void StartOfLine()
		{
			Pattern pattern = Pattern.With.StartOfLine;

			pattern.ToString().Should().Be("^");
		}

		[Fact]
		public void EndOfLine()
		{
			Pattern pattern = Pattern.With.EndOfLine;
			pattern.ToString().Should().Be("$");
		}

		[Fact]
		public void Anything()
		{
			Pattern pattern = Pattern.With.Anything;
			pattern.ToString().Should().Be(".");
		}

		[Fact]
		public void Literal()
		{
			Pattern pattern = Pattern.With.Literal("a");
			pattern.ToString().Should().Be("a");
		}

		[Fact]
		public void Digit()
		{
			Pattern pattern = Pattern.With.Digit;
			pattern.ToString().Should().Be("\\d");
		}

		[Fact]
		public void NonDigit()
		{
			Pattern pattern = Pattern.With.NonDigit;
			pattern.ToString().Should().Be("\\D");
		}

		[Fact]
		public void Word()
		{
			Pattern pattern = Pattern.With.Word;
			pattern.ToString().Should().Be("\\w");
		}

		[Fact]
		public void NonWord()
		{
			Pattern pattern = Pattern.With.NonWord;
			pattern.ToString().Should().Be("\\W");
		}

		[Fact]
		public void WordBoundary()
		{
			Pattern pattern = Pattern.With.WordBoundary;
			pattern.ToString().Should().Be("\\b");
		}

		[Fact]
		public void Letter()
		{
			Pattern pattern = Pattern.With.Letter;
			pattern.ToString().Should().Be("a-zA-Z");
		}

		[Fact]
		public void LowercaseLetter()
		{
			Pattern pattern = Pattern.With.LowercaseLetter;
			pattern.ToString().Should().Be("a-z");
		}

		[Fact]
		public void UppercaseLetter()
		{
			Pattern pattern = Pattern.With.UppercaseLetter;
			pattern.ToString().Should().Be("A-Z");
		}

		[Fact]
		public void Whitespace()
		{
			Pattern pattern = Pattern.With.Whitespace;
			pattern.ToString().Should().Be("\\s");
		}

		[Fact]
		public void NonWhitespace()
		{
			Pattern pattern = Pattern.With.NonWhitespace;
			pattern.ToString().Should().Be("\\S");
		}

		[Fact]
		public void Tab()
		{
			Pattern pattern = Pattern.With.Tab;
			pattern.ToString().Should().Be("\\t");
		}

		[Fact]
		public void CarriageReturn()
		{
			Pattern pattern = Pattern.With.CarriageReturn;
			pattern.ToString().Should().Be("\\r");
		}

		[Fact]
		public void Newline()
		{
			Pattern pattern = Pattern.With.Newline;
			pattern.ToString().Should().Be("\\n");
		}
	}
}
