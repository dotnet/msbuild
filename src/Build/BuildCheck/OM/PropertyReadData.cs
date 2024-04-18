using Microsoft.Build.Evaluation;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BuildCheck.Analyzers;

/// <summary>
/// Information about property being accessed - whether during evaluation or build.
/// </summary>
internal class PropertyReadData(
    string projectFilePath,
    string propertyName,
    IMsBuildElementLocation elementLocation,
    bool isUninitialized,
    PropertyReadContext propertyReadContext)
    : AnalysisData(projectFilePath)
{
    /// <summary>
    /// Name of the property that was accessed.
    /// </summary>
    public string PropertyName { get; } = propertyName;

    /// <summary>
    /// Location of the property access.
    /// </summary>
    public IMsBuildElementLocation ElementLocation { get; } = elementLocation;

    /// <summary>
    /// Indicates whether the property was accessed before being initialized.
    /// </summary>
    public bool IsUninitialized { get; } = isUninitialized;

    /// <summary>
    /// Gets the context type in which the property was accessed.
    /// </summary>
    public PropertyReadContext PropertyReadContext { get; } = propertyReadContext;
}