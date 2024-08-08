using Microsoft.Build.Construction;
using Microsoft.Build.Experimental.BuildCheck;
using System.Collections.Generic;

namespace Company.CheckTemplate
{
    public sealed class Check1 : Check
    {
        public static BuildCheckRule SupportedRule = new CheckRule(
            "X01234",
            "Title",
            "Description",
            "Message format: {0}",
            new BuildCheckConfiguration());

        public override string FriendlyName => "Company.Check1";

        public override IReadOnlyList<CheckRule> SupportedRules { get; } = new List<CheckRule>() { SupportedRule };

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
