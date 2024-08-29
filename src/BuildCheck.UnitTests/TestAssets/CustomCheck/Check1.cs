// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.Build.Experimental.BuildCheck;

namespace CustomCheck
{
    public sealed class Check1 : Check
    {
        public static CheckRule SupportedRule = new CheckRule(
            "X01234",
            "Title",
            "Description",
            "Message format: {0}",
            new CheckConfiguration());

        public override string FriendlyName => "CustomRule1";

        public override IReadOnlyList<CheckRule> SupportedRules { get; } = new List<CheckRule>() { SupportedRule };

        private string message = "Argument for the message format";

        public override void Initialize(ConfigurationContext configurationContext)
        {
            var infraData = configurationContext.CheckConfig[0];
            var customData = configurationContext.CustomConfigurationData[0].ConfigurationData;
            // configurationContext to be used only if check needs external configuration data.
            if (customData is not null &&
                configurationContext.CustomConfigurationData[0].RuleId == "X01234" &&
                customData.TryGetValue("setmessage", out string? setMessage))
            {
                message = infraData.Severity + setMessage;
            }
        }

        public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
        {
            registrationContext.RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction);
        }

        private void EvaluatedPropertiesAction(BuildCheckDataContext<EvaluatedPropertiesCheckData> context)
        {
            context.ReportResult(BuildCheckResult.Create(
                SupportedRule,
                ElementLocation.EmptyLocation,
                message));
        }
    }
}
