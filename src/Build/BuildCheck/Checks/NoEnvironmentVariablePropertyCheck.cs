// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Experimental.BuildCheck.Checks;

internal sealed class NoEnvironmentVariablePropertyCheck : Check
{
    public static CheckRule SupportedRule = new CheckRule(
                "BC0103",
                "NoEnvironmentVariablePropertyCheck",
                "No implicit property derived from an environment variable should be used during the build",
                "Property is derived from environment variable: {0}. Properties should be passed explicitly using the /p option.",
                new CheckConfiguration() { Severity = CheckResultSeverity.Suggestion });

    private const string RuleId = "BC0103";

    private const string VerboseEnvVariableOutputKey = "allow_displaying_environment_variable_value";

    /// <summary>
    /// Contains the list of reported environment variables.
    /// </summary>
    private readonly HashSet<EnvironmentVariableIdentityKey> _environmentVariablesReported = new HashSet<EnvironmentVariableIdentityKey>();

    private bool _isVerboseEnvVarOutput;
    private EvaluationCheckScope _scope;

    public override string FriendlyName => "MSBuild.NoEnvironmentVariablePropertyCheck";

    public override IReadOnlyList<CheckRule> SupportedRules { get; } = [SupportedRule];

    public override void Initialize(ConfigurationContext configurationContext)
    {
        _scope = configurationContext.CheckConfig[0].EvaluationCheckScope;
        foreach (CustomConfigurationData customConfigurationData in configurationContext.CustomConfigurationData)
        {
            bool? isVerboseEnvVarOutput = GetVerboseEnvVarOutputConfig(customConfigurationData, RuleId);
            _isVerboseEnvVarOutput = isVerboseEnvVarOutput.HasValue && isVerboseEnvVarOutput.Value;           
        }
    }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext) => registrationContext.RegisterEnvironmentVariableReadAction(ProcessEnvironmentVariableReadAction);

    private void ProcessEnvironmentVariableReadAction(BuildCheckDataContext<EnvironmentVariableCheckData> context)
    {
        if (context.Data.EvaluatedEnvironmentVariables.Count != 0)
        {
            foreach (var envVariableData in context.Data.EvaluatedEnvironmentVariables)
            {
                if (!CheckScopeClassifier.IsActionInObservedScope(_scope, envVariableData.Value.File, context.Data.ProjectFilePath))
                {
                    continue;
                }
                EnvironmentVariableIdentityKey identityKey = new(envVariableData.Key, envVariableData.Value.File, envVariableData.Value.Line, envVariableData.Value.Column);
                if (!_environmentVariablesReported.Contains(identityKey))
                {
                    if (_isVerboseEnvVarOutput)
                    {
                        context.ReportResult(BuildCheckResult.Create(
                            SupportedRule,
                            ElementLocation.Create(envVariableData.Value.File, envVariableData.Value.Line, envVariableData.Value.Column),
                            $"'{envVariableData.Key}' with value: '{envVariableData.Value.EnvVarValue}'"));
                    }
                    else
                    {
                        context.ReportResult(BuildCheckResult.Create(
                            SupportedRule,
                            ElementLocation.Create(envVariableData.Value.File, envVariableData.Value.Line, envVariableData.Value.Column),
                            $"'{envVariableData.Key}'"));
                    }

                    _environmentVariablesReported.Add(identityKey);
                }
            }
        }
    }

    private static bool? GetVerboseEnvVarOutputConfig(CustomConfigurationData customConfigurationData, string ruleId) => customConfigurationData.RuleId.Equals(ruleId, StringComparison.InvariantCultureIgnoreCase)
            && (customConfigurationData.ConfigurationData?.TryGetValue(VerboseEnvVariableOutputKey, out string? configVal) ?? false)
            ? bool.Parse(configVal)
            : null;

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
