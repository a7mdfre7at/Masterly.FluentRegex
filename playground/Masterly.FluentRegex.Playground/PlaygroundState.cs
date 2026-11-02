using System.Text;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Playground;

/// <summary>
/// The shareable state of the playground, encoded in the URL fragment so a link reproduces
/// exactly the pattern, options and test text the author was looking at.
/// </summary>
public sealed class PlaygroundState
{
    private const string PatternKey = "p";
    private const string OptionsKey = "o";
    private const string TextKey = "t";
    private const string TabKey = "tab";
    private const string AsciiDigitsKey = "ad";
    private const string EnabledFlag = "1";

    /// <summary>Gets or sets the regex source.</summary>
    public string Regex { get; set; } = @"^(?<user>[\w.+-]+)@(?<domain>[\w-]+(?:\.[\w-]+)+)$";

    /// <summary>Gets or sets the regex options.</summary>
    public RegexOptions Options { get; set; } = RegexOptions.Multiline;

    /// <summary>Gets or sets the text the pattern is tested against.</summary>
    public string TestText { get; set; } = "ahmad@example.com\nnot an email\nsupport@masterly.dev";

    /// <summary>Gets or sets the selected panel.</summary>
    public string Tab { get; set; } = "matches";

    /// <summary>Gets or sets a value indicating whether <c>\d</c> matches only the ASCII digits 0-9.</summary>
    public bool AsciiDigits { get; set; }

    /// <summary>Encodes the state as a URL fragment, including the leading '#'.</summary>
    /// <returns>The fragment.</returns>
    public string ToFragment()
    {
        string digits = AsciiDigits ? $"&{AsciiDigitsKey}={EnabledFlag}" : string.Empty;
        return $"#{PatternKey}={Encode(Regex)}&{OptionsKey}={(int)Options}&{TextKey}={Encode(TestText)}&{TabKey}={Uri.EscapeDataString(Tab)}{digits}";
    }

    /// <summary>Reads state from a URL fragment, keeping defaults for anything missing or malformed.</summary>
    /// <param name="uri">The absolute page URI.</param>
    /// <returns>The decoded state.</returns>
    public static PlaygroundState FromUri(string uri)
    {
        PlaygroundState state = new PlaygroundState();
        int hash = uri.IndexOf('#');
        if (hash < 0)
        {
            return state;
        }

        foreach (string part in uri.Substring(hash + 1).Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = part.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            string key = part.Substring(0, equals);
            string value = part.Substring(equals + 1);
            state.Apply(key, value);
        }

        return state;
    }

    private void Apply(string key, string value)
    {
        switch (key)
        {
            case PatternKey:
                Regex = Decode(value) ?? Regex;
                break;
            case OptionsKey:
                if (int.TryParse(value, out int options))
                {
                    Options = (RegexOptions)options;
                }
                break;
            case TextKey:
                TestText = Decode(value) ?? TestText;
                break;
            case TabKey:
                Tab = Uri.UnescapeDataString(value);
                break;
            case AsciiDigitsKey:
                AsciiDigits = value == EnabledFlag;
                break;
        }
    }

    private static string Encode(string text)
    {
        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string? Decode(string value)
    {
        string base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
