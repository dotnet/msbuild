using System.Reflection;

namespace Microsoft.MSBuild.ReleaseSnappingMCP;

/// <summary>
/// Generates MSBuild release checklists by reading the template from documentation/release-checklist.md
/// (embedded as a resource) and replacing version placeholders.
/// 
/// The primary source of truth is the release-checklist.md file in the documentation folder.
/// Any updates to the checklist process should be made there, and this generator will automatically
/// pick up those changes when rebuilt.
/// </summary>
public sealed class ReleaseChecklistGenerator
{
    private readonly string _checklistTemplate;

    public ReleaseChecklistGenerator()
    {
        _checklistTemplate = LoadChecklistTemplate();
    }

    /// <summary>
    /// Loads the checklist template from the embedded resource.
    /// Falls back to a minimal template if the resource is not found.
    /// </summary>
    private static string LoadChecklistTemplate()
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream("release-checklist.md");
        if (stream is null)
        {
            // Fallback message if template is not embedded
            return """
                # MSBuild Release Checklist {{THIS_RELEASE_VERSION}}
                
                **Error:** The release checklist template was not found as an embedded resource.
                
                Please ensure the project is built correctly and that `documentation/release-checklist.md` 
                is properly embedded in the assembly.
                
                As a workaround, you can copy the checklist manually from:
                https://github.com/dotnet/msbuild/blob/main/documentation/release-checklist.md
                """;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Gets the raw checklist template without any replacements.
    /// Useful for debugging or viewing the template structure.
    /// </summary>
    public string GetRawTemplate() => _checklistTemplate;

    /// <summary>
    /// Generates a release checklist for the specified version.
    /// Reads the template from documentation/release-checklist.md and replaces all placeholders.
    /// </summary>
    public string GenerateChecklist(ReleaseVersion version)
    {
        return _checklistTemplate
            .Replace("{{THIS_RELEASE_VERSION}}", version.Current)
            .Replace("{{PREVIOUS_RELEASE_VERSION}}", version.Previous)
            .Replace("{{NEXT_VERSION}}", version.Next)
            .Replace("{{THIS_RELEASE_EXACT_VERSION}}", $"{version.Current}.0")
            .Replace("{{URL_OF_CHANNEL_PROMOTION_PR}}", "<!-- TODO: Add URL -->")
            .Replace("{{URL_OF_NEXT_VERSION_BRANDING_PR}}", "<!-- TODO: Add URL -->")
            .Replace("{{URL_OF_LOCALIZATION_TICKET}}", "<!-- TODO: Add URL -->")
            .Replace("{{URL_OF_FINAL_BRANDING_PR}}", "<!-- TODO: Add URL -->")
            .Replace("{{URL_OF_PERFSTAR_PR}}", "<!-- TODO: Add URL -->")
            .Replace("{{URL_OF_VS_INSERTION}}", "<!-- TODO: Add URL -->")
            .Replace("{{URL_OF_PR}}", "<!-- TODO: Add URL -->");
    }

    /// <summary>
    /// Gets the issue title for a release checklist.
    /// </summary>
    public static string GetIssueTitle(ReleaseVersion version) => $"Release {version.Current}";
}
