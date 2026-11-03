#nullable enable

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill that lets <c>init</c> accessors compile on netstandard2.1.
/// </summary>
internal static class IsExternalInit
{
}
#endif
