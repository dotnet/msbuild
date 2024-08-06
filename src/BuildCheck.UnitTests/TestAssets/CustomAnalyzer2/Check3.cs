using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.Build.Experimental.BuildCheck;

namespace CustomCheck2
{
    public sealed class Check3 : BuildExecutionCheck
    {
        public static BuildExecutionCheckRule SupportedRule = new BuildExecutionCheckRule(
            "X01235",
            "Title",
            "Description",
            "Message format: {0}",
            new BuildExecutionCheckConfiguration());

        public override string FriendlyName => "CustomRule3";

        public override IReadOnlyList<BuildExecutionCheckRule> SupportedRules { get; } = new List<BuildExecutionCheckRule>() { SupportedRule };

        public override void Initialize(ConfigurationContext configurationContext)
        {
            // configurationContext to be used only if check needs external configuration data.
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
                "Argument for the message format"));
        }
    }
}
