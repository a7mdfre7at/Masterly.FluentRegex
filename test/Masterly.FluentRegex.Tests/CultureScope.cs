using System.Globalization;

namespace Masterly.FluentRegex.Tests;

/// <summary>
/// Runs a block under a given current culture and restores the previous one. Case-insensitive patterns fold case
/// under the current culture (as .NET's own regex does), so tests that assert a folding set must pin the culture
/// rather than inherit the machine's: en-US folds i with U+0130, the invariant culture does not.
/// </summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

    public CultureScope(string name)
    {
        CultureInfo.CurrentCulture = name.Length == 0 ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(name);
    }

    public void Dispose() => CultureInfo.CurrentCulture = _previous;
}
