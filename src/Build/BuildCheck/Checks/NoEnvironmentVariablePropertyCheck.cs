// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Checks;

internal sealed class NoEnvironmentVariablePropertyCheck : Check
{
    public static CheckRule SupportedRule = new CheckRule(
        "BC0103",
        "NoEnvironmentVariablePropertyCheck",
        ResourceUtilities.GetResourceString("BuildCheck_BC0103_Title")!,
        ResourceUtilities.GetResourceString("BuildCheck_BC0103_MessageFmt")!,
        new CheckConfiguration() { Severity = CheckResultSeverity.Suggestion });

    private const string RuleId = "BC0103";

    private const string VerboseEnvVariableOutputKey = "allow_displaying_environment_variable_value";

    private readonly Queue<(string projectPath, BuildCheckDataContext<EnvironmentVariableCheckData>)> _buildCheckResults = new Queue<(string, BuildCheckDataContext<EnvironmentVariableCheckData>)>();

    /// <summary>
    /// Contains the list of viewed environment variables.
    /// </summary>
    private readonly HashSet<EnvironmentVariableIdentityKey> _environmentVariablesCache = new HashSet<EnvironmentVariableIdentityKey>();

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

        CheckScopeClassifier.NotifyOnScopingReadiness += HandleScopeReadiness;
    }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext) => registrationContext.RegisterEnvironmentVariableReadAction(ProcessEnvironmentVariableReadAction);

    private void ProcessEnvironmentVariableReadAction(BuildCheckDataContext<EnvironmentVariableCheckData> context)
    {
        EnvironmentVariableIdentityKey identityKey = new(context.Data.EnvironmentVariableName, context.Data.EnvironmentVariableLocation);
        if (!_environmentVariablesCache.Contains(identityKey))
        {
            // Scope information is available after evaluation of the project file. If it is not ready, we will report the check later.
            if (!CheckScopeClassifier.IsScopingReady(_scope))
            {
                _buildCheckResults.Enqueue((context.Data.ProjectFilePath, context));
            }
            else if (CheckScopeClassifier.IsActionInObservedScope(_scope, context.Data.EnvironmentVariableLocation.File, context.Data.ProjectFilePath))
            {
                context.ReportResult(BuildCheckResult.Create(
                    SupportedRule,
                    context.Data.EnvironmentVariableLocation,
                    GetFormattedMessage(context.Data.EnvironmentVariableName, context.Data.EnvironmentVariableValue)));
            }

            _environmentVariablesCache.Add(identityKey);
        }
    }

    private static bool? GetVerboseEnvVarOutputConfig(CustomConfigurationData customConfigurationData, string ruleId) => customConfigurationData.RuleId.Equals(ruleId, StringComparison.InvariantCultureIgnoreCase)
            && (customConfigurationData.ConfigurationData?.TryGetValue(VerboseEnvVariableOutputKey, out string? configVal) ?? false)
            ? bool.Parse(configVal)
            : null;

    private void HandleScopeReadiness()
    {
        while (_buildCheckResults.Count > 0)
        {
            (string projectPath, BuildCheckDataContext<EnvironmentVariableCheckData> context) = _buildCheckResults.Dequeue();
            if (!CheckScopeClassifier.IsActionInObservedScope(_scope, context.Data.EnvironmentVariableLocation.File, projectPath))
            {
                continue;
            }

            context.ReportResult(BuildCheckResult.Create(
                SupportedRule,
                context.Data.EnvironmentVariableLocation,
                GetFormattedMessage(context.Data.EnvironmentVariableName, context.Data.EnvironmentVariableValue)));
        }

        CheckScopeClassifier.NotifyOnScopingReadiness -= HandleScopeReadiness;
    }

    private string GetFormattedMessage(string envVariableName, string envVariableValue) => _isVerboseEnvVarOutput ? string.Format(ResourceUtilities.GetResourceString("BuildCheck_BC0103_MessageAddendum")!, envVariableName, envVariableValue) : $"'{envVariableName}'";

    internal class EnvironmentVariableIdentityKey(string environmentVariableName, IMSBuildElementLocation location) : IEquatable<EnvironmentVariableIdentityKey>
    {
        public string EnvironmentVariableName { get; } = environmentVariableName;

        public IMSBuildElementLocation Location { get; } = location;

        public override bool Equals(object? obj) => Equals(obj as EnvironmentVariableIdentityKey);

        public bool Equals(EnvironmentVariableIdentityKey? other) =>
            other != null &&
            EnvironmentVariableName == other.EnvironmentVariableName &&
            Location.File == other.Location.File &&
            Location.Line == other.Location.Line &&
            Location.Column == other.Location.Column;

        public override int GetHashCode()
        {
            int hashCode = 17;
            hashCode = hashCode * 31 + (Location.File != null ? Location.File.GetHashCode() : 0);
            hashCode = hashCode * 31 + Location.Line.GetHashCode();
            hashCode = hashCode * 31 + Location.Column.GetHashCode();

            return hashCode;
        }
    }
}
