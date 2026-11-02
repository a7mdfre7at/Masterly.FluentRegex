namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>Annotation names used to carry <c>HasPattern</c> configuration until the model is finalized.</summary>
internal static class FluentRegexAnnotationNames
{
    private const string Prefix = "FluentRegex:";

    /// <summary>The <see cref="Masterly.FluentRegex.Pattern"/> a string property must match.</summary>
    public const string Pattern = Prefix + "Pattern";

    /// <summary>The explicit CHECK constraint name, when one was given.</summary>
    public const string ConstraintName = Prefix + "ConstraintName";
}
