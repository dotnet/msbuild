using System.Collections.Generic;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli
{
    public class TemplateListResolutionResult
    {
        public IReadOnlyList<IFilteredTemplateInfo> CoreMatchedTemplates { get; set; }

        public IReadOnlyList<IFilteredTemplateInfo> UnambiguousTemplateGroupToUse { get; set; }

        public IReadOnlyList<IFilteredTemplateInfo> MatchedTemplatesWithSecondaryMatchInfo { get; set; }
    }
}
