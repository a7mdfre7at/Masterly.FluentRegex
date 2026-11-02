#nullable enable

namespace Masterly.FluentRegex;

/// <summary>Controls how <see cref="Pattern.ToFlavor"/> trades exactness for idiomatic output.</summary>
public sealed class FlavorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether <c>\d</c>, <c>\w</c>, <c>\s</c> and <c>\b</c> keep
    /// their Unicode .NET meaning (<c>\p{Nd}</c>, <c>[\p{L}\p{Mn}\p{Nd}\p{Pc}]</c>, ...). When false
    /// they are written as their ASCII JavaScript namesakes and a warning is reported. Default: true.
    /// </summary>
    public bool PreserveUnicodeSemantics { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether <c>$</c>, <c>.</c> and multiline anchors keep their
    /// .NET meaning, for example <c>$</c> as <c>(?=\n?$)</c> because .NET's <c>$</c> also matches
    /// before a final newline. When false the plain JavaScript constructs (and the <c>m</c> flag)
    /// are used and a warning is reported. Default: true.
    /// </summary>
    public bool PreserveLineSemantics { get; set; } = true;
}
