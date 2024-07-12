// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Experimental.BuildCheck.Analyzers;

internal sealed class NoEnvironmentVariablePropertyAnalyzer : BuildAnalyzer
{
    /// <summary>
    /// Contains the list of reported environment variables.
    /// </summary>
    private readonly HashSet<EnvironmentVariableIdentityKey> _environmentVariablesReported = new HashSet<EnvironmentVariableIdentityKey>();

    public static BuildAnalyzerRule SupportedRule = new BuildAnalyzerRule(
        "BC0103",
        "NoEnvironmentVariablePropertyAnalyzer",
        "No implicit property derived from an environment variable should be used during the build",
        "Property is derived from environment variable: '{0}' with value: '{1}'. Properties should be passed explicitly using the /p option.",
        new BuildAnalyzerConfiguration() { Severity = BuildAnalyzerResultSeverity.Warning });

    public override string FriendlyName => "MSBuild.NoEnvironmentVariablePropertyAnalyzer";

    public override IReadOnlyList<BuildAnalyzerRule> SupportedRules { get; } = [SupportedRule];

    public override void Initialize(ConfigurationContext configurationContext)
    {
        // No custom configuration
    }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext) => registrationContext.RegisterEvaluatedPropertiesAction(ProcessEnvironmentVariableReadAction);

    private void ProcessEnvironmentVariableReadAction(BuildCheckDataContext<EvaluatedPropertiesAnalysisData> context)
    {
        if (context.Data.EvaluatedEnvironmentVariables.Count != 0)
        {
            foreach (var envVariableData in context.Data.EvaluatedEnvironmentVariables)
            {
                EnvironmentVariableIdentityKey identityKey = new(envVariableData.Key, envVariableData.Value.File, envVariableData.Value.Line, envVariableData.Value.Column);
                if (!_environmentVariablesReported.Contains(identityKey))
                {
                    context.ReportResult(BuildCheckResult.Create(
                        SupportedRule,
                        ElementLocation.Create(envVariableData.Value.File, envVariableData.Value.Line, envVariableData.Value.Column),
                        envVariableData.Key,
                        envVariableData.Value.EnvVarValue));

                    _environmentVariablesReported.Add(identityKey);
                }
            }
        }
    }

    internal class EnvironmentVariableIdentityKey(string environmentVariableName, string file, int line, int column) : IEquatable<EnvironmentVariableIdentityKey>
    {
        public string EnvironmentVariableName { get; } = environmentVariableName;

        public string File { get; } = file;

        public int Line { get; } = line;

        public int Column { get; } = column;

        public override bool Equals(object? obj) => Equals(obj as EnvironmentVariableIdentityKey);

        public bool Equals(EnvironmentVariableIdentityKey? other) =>
            other != null &&
            EnvironmentVariableName == other.EnvironmentVariableName &&
            File == other.File &&
            Line == other.Line &&
            Column == other.Column;

        public override int GetHashCode()
        {
            int hashCode = 17;
            hashCode = hashCode * 31 + (File != null ? File.GetHashCode() : 0);
            hashCode = hashCode * 31 + Line.GetHashCode();
            hashCode = hashCode * 31 + Column.GetHashCode();
            return hashCode;
        }
    }
}
