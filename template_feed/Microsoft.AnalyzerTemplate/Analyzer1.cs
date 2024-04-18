using System;

namespace Company.AnalyzerTemplate
{
    public sealed class Analyzer1 : BuildAnalyzer
    {
        public static BuildAnalyzerRule SupportedRule = new BuildAnalyzerRule("X01234", "Title",
            "Description", "Category",
            "Message format: {0}",
            new BuildAnalyzerConfiguration() { Severity = BuildAnalyzerResultSeverity.Warning, IsEnabled = true });

        public override string FriendlyName => "Company.Analyzer1";

        public override IReadOnlyList<BuildAnalyzerRule> SupportedRules { get; } =[SupportedRule];

        public override void Initialize(ConfigurationContext configurationContext)
        {
            // configurationContext to be used only if analyzer needs external configuration data.
        }

        public override void RegisterActions(IBuildCopRegistrationContext registrationContext)
        {
            registrationContext.RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction);
        }
        
        private void EvaluatedPropertiesAction(BuildCopDataContext<EvaluatedPropertiesAnalysisData> context)
        {
            context.ReportResult(BuildCopResult.Create(
                SupportedRule,
                ElementLocation.EmptyLocation,
                "Argument for the message format");
        }
    }
}
