using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.Build.Experimental.BuildCheck;

namespace InvalidCustomAnalyzer
{
    public sealed class InvalidAnalyzer
    {
        public static BuildAnalyzerRule SupportedRule = new BuildAnalyzerRule(
            "X01235",
            "Title",
            "Description",
            "Message format: {0}",
            new BuildAnalyzerConfiguration());

        public string FriendlyName => "InvalidAnalyzer";
    }
}
