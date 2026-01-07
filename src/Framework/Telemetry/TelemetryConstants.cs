// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
namespace Microsoft.Build.Framework.Telemetry;

/// <summary>
/// Constants for VS Telemetry for basic configuration and appropriate naming for VS exporting/collection.
/// </summary>
internal static class TelemetryConstants
{
    /// <summary>
    /// Prefix required by VS exporting/collection.
    /// </summary>
    public const string EventPrefix = "VS/MSBuild/";

    /// <summary>
    /// Prefix required by VS exporting/collection.
    /// </summary>
    public const string PropertyPrefix = "VS.MSBuild.";

    /// <summary>
    /// "Microsoft.Build.Telemetry.*" namespace is required by VS exporting/collection.
    /// </summary>
    public const string ActivitySourceNamespacePrefix = "Microsoft.Build.Telemetry";

    /// <summary>
    /// Namespace of the default ActivitySource handling e.g. End of build telemetry.
    /// </summary>
    public const string DefaultActivitySourceNamespace = $"{ActivitySourceNamespacePrefix}Default";

    /// <summary>
    /// Sample rate for the default namespace.
    /// 1:25000 gives us sample size of sufficient confidence with the assumption we collect the order of 1e7 - 1e8 events per day.
    /// </summary>
    public const double DefaultSampleRate = 4e-5;

    /// <summary>
    /// Name of the property for build duration.
    /// </summary>
    public const string BuildDurationPropertyName = "BuildDurationInMilliseconds";

    /// <summary>
    /// Name of the property for inner build duration.
    /// </summary>
    public const string InnerBuildDurationPropertyName = "InnerBuildDurationInMilliseconds";

    /// <summary>
    /// Name of the property for build activity.
    /// </summary>
    public const string Build = "Build";
}
