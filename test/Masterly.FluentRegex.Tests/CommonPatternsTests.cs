namespace Masterly.FluentRegex.Tests;

public class CommonPatternsTests
{
    #region Email Tests

    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("user.name@domain.org", true)]
    [InlineData("user_name@domain.co.uk", true)]
    [InlineData("user-name@domain.io", true)]
    [InlineData("test123@test123.com", true)]
    [InlineData("invalid", false)]
    [InlineData("missing@", false)]
    [InlineData("@domain.com", false)]
    [InlineData("spaces in@email.com", false)]
    public void Email_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.Email();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    #endregion

    #region URL Tests

    [Theory]
    [InlineData("http://example.com", true)]
    [InlineData("https://example.com", true)]
    [InlineData("https://www.example.com", true)]
    [InlineData("https://example.com/path", true)]
    [InlineData("https://example.com/path/to/page", true)]
    [InlineData("ftp://example.com", false)]
    [InlineData("example.com", false)]
    public void Url_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.Url();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    #endregion

    #region GUID Tests

    [Theory]
    [InlineData("550e8400-e29b-41d4-a716-446655440000", true)]
    [InlineData("00000000-0000-0000-0000-000000000000", true)]
    [InlineData("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF", true)]
    [InlineData("not-a-guid", false)]
    [InlineData("550e8400e29b41d4a716446655440000", false)] // Missing dashes
    [InlineData("550e8400-e29b-41d4-a716-44665544000", false)] // Too short
    public void Guid_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.Guid();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    #endregion

    #region HexColor Tests

    [Theory]
    [InlineData("#FFF", true)]
    [InlineData("#fff", true)]
    [InlineData("#FFFFFF", true)]
    [InlineData("#ffffff", true)]
    [InlineData("#FFFF", true)]      // RGBA short
    [InlineData("#FFFFFFFF", true)]  // RRGGBBAA
    [InlineData("#GGG", false)]
    [InlineData("FFF", false)]       // Missing #
    [InlineData("#FF", false)]       // Too short
    public void HexColor_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.HexColor();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    #endregion

    #region Date Tests

    [Theory]
    [InlineData("2024-01-15", true)]
    [InlineData("2024/01/15", true)]
    [InlineData("01/15/2024", true)]
    [InlineData("1/5/24", true)]
    [InlineData("15-01-2024", true)]
    [InlineData("not-a-date", false)]
    public void Date_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.Date();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("2024-01-15", true)]
    [InlineData("2024-12-31", true)]
    [InlineData("1999-06-30", true)]
    [InlineData("2024/01/15", false)] // Wrong separator for ISO
    public void DateIso_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.DateIso();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    #endregion

    #region Time Tests

    [Theory]
    [InlineData("12:30", true)]
    [InlineData("23:59", true)]
    [InlineData("00:00", true)]
    [InlineData("12:30:45", true)]
    [InlineData("23:59:59", true)]
    [InlineData("25:00", false)]
    [InlineData("12:60", false)]
    public void Time24_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.Time24();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("12:30 AM", true)]
    [InlineData("12:30 PM", true)]
    [InlineData("12:30AM", true)]
    [InlineData("1:30 am", true)]
    [InlineData("12:30:45 PM", true)]
    [InlineData("12:30", false)] // Missing AM/PM
    public void Time12_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.Time12();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    #endregion

    #region SSN Tests

    [Theory]
    [InlineData("123-45-6789", true)]
    [InlineData("000-00-0000", true)]
    [InlineData("999-99-9999", true)]
    [InlineData("123456789", false)]  // Missing dashes
    [InlineData("123-45-678", false)] // Too short
    [InlineData("12-345-6789", false)] // Wrong format
    public void SocialSecurityNumber_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.SocialSecurityNumber();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    #endregion

    #region ZIP Code Tests

    [Theory]
    [InlineData("12345", true)]
    [InlineData("12345-6789", true)]
    [InlineData("00000", true)]
    [InlineData("1234", false)]       // Too short
    [InlineData("123456", false)]     // Wrong format
    [InlineData("12345-678", false)]  // Extended part too short
    public void ZipCode_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.ZipCode();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    #endregion

    #region Username Tests

    [Theory]
    [InlineData("user", true)]
    [InlineData("user123", true)]
    [InlineData("user_name", true)]
    [InlineData("User_Name_123", true)]
    [InlineData("ab", false)]          // Too short
    [InlineData("user name", false)]   // Contains space
    [InlineData("user@name", false)]   // Contains @
    public void Username_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.Username();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    #endregion

    #region Integer Tests

    [Theory]
    [InlineData("123", true)]
    [InlineData("-123", true)]
    [InlineData("0", true)]
    [InlineData("-0", true)]
    [InlineData("12.34", false)]
    [InlineData("abc", false)]
    public void Integer_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.Integer();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    #endregion

    #region Decimal Tests

    [Theory]
    [InlineData("123", true)]
    [InlineData("123.45", true)]
    [InlineData("-123.45", true)]
    [InlineData("0.5", true)]
    [InlineData("-0.5", true)]
    [InlineData(".5", false)]         // Missing leading digit
    [InlineData("abc", false)]
    public void Decimal_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.Decimal();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    #endregion

    #region Scientific Notation Tests

    [Theory]
    [InlineData("1e10", true)]
    [InlineData("1E10", true)]
    [InlineData("1.5e10", true)]
    [InlineData("-1.5e-10", true)]
    [InlineData("1.5e+10", true)]
    [InlineData("123", false)]        // No exponent
    public void ScientificNotation_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.ScientificNotation();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    #endregion

    #region CreditCard Tests

    [Theory]
    [InlineData("1234567890123456", true)]
    [InlineData("1234 5678 9012 3456", true)]
    [InlineData("1234-5678-9012-3456", true)]
    [InlineData("123456789012", false)]  // Too short
    public void CreditCard_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.CreditCard();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    #endregion

    #region Strong Password Tests

    [Theory]
    [InlineData("Password1!", true)]
    [InlineData("StrongP@ss1", true)]
    [InlineData("Abc12345!", true)]
    [InlineData("weakpass", false)]     // No uppercase, digit, or special
    [InlineData("WEAKPASS1!", false)]   // No lowercase
    [InlineData("Weak1!", false)]       // Too short
    public void StrongPassword_MatchesCorrectly(string input, bool expected)
    {
        var pattern = CommonPatterns.StrongPassword();
        pattern.IsMatch(input).ShouldBe(expected);
    }

    #endregion

    #region Pattern Reuse Tests

    [Fact]
    public void CommonPatterns_CanBeReused()
    {
        var emailPattern = CommonPatterns.Email();

        // Should be able to use the same pattern multiple times
        emailPattern.IsMatch("test@example.com").ShouldBeTrue();
        emailPattern.IsMatch("another@test.org").ShouldBeTrue();
        emailPattern.IsMatch("invalid").ShouldBeFalse();
    }

    [Fact]
    public void CommonPatterns_ReturnNewInstances()
    {
        var pattern1 = CommonPatterns.Email();
        var pattern2 = CommonPatterns.Email();

        // Each call should return a new instance
        pattern1.ShouldNotBeSameAs(pattern2);
    }

    #endregion
}
