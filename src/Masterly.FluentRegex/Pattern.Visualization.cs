using Masterly.FluentRegex.Visualization;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// Diagrams for documentation, pull requests and code review.
    /// </summary>
    public partial class Pattern
    {
        /// <summary>
        /// Renders the pattern as a railroad diagram: a self-contained SVG document.
        /// </summary>
        /// <returns>SVG markup that can be saved to a .svg file or embedded in HTML and Markdown.</returns>
        /// <example>
        /// File.WriteAllText("email.svg", CommonPatterns.Email().ToRailroadSvg());
        /// </example>
        public string ToRailroadSvg() => RailroadRenderer.Render(ParseTree());

        /// <summary>
        /// Renders the pattern as a Mermaid flowchart, which GitHub, GitLab, Azure DevOps and many
        /// documentation tools display natively inside a <c>```mermaid</c> code block.
        /// </summary>
        /// <returns>Mermaid flowchart source.</returns>
        public string ToMermaid() => MermaidRenderer.Render(ParseTree());
    }
}
