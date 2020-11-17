
namespace Microsoft.TemplateEngine.Cli.TableOutput
{
    /// <summary>
    /// Represents a table row for template group display
    /// </summary>
    internal struct TemplateGroupTableRow
    {
        internal string Author { get; set; }
        internal string Classifications { get; set; }
        internal string Languages { get; set; }
        internal string Name { get; set; }
        internal string ShortName { get; set; }
        internal string Type { get; set; }
    }
}
