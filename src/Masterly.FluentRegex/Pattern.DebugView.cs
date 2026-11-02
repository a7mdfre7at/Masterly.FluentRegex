#nullable enable
using System.Diagnostics;

namespace Masterly.FluentRegex;

/// <summary>
/// Debugger integration: the debugger shows a pattern as its regex text, and expanding it shows the options,
/// timeout, group names, a plain-language explanation and a ReDoS verdict.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
[DebuggerTypeProxy(typeof(PatternDebugView))]
public partial class Pattern
{
}
