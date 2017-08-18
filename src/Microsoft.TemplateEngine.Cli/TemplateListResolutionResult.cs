// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli
{
    public class TemplateListResolutionResult
    {
        public IReadOnlyList<IFilteredTemplateInfo> CoreMatchedTemplates { get; set; }

        public IReadOnlyList<IFilteredTemplateInfo> UnambiguousTemplateGroupToUse { get; set; }

        public IReadOnlyList<IFilteredTemplateInfo> MatchedTemplatesWithSecondaryMatchInfo { get; set; }

        public bool HasCoreMatchedTemplatesWithDisposition(Func<IFilteredTemplateInfo, bool> filter)
        {
            return CoreMatchedTemplates != null
                    && CoreMatchedTemplates.Any(filter);
        }

        public bool HasUnambiguousTemplateGroupToUse
        {
            get
            {
                return UnambiguousTemplateGroupToUse != null
                        && UnambiguousTemplateGroupToUse.Count > 0;
            }
        }

        public bool HasMatchedTemplatesWithSecondaryMatchInfo
        {
            get
            {
                return MatchedTemplatesWithSecondaryMatchInfo != null
                        && MatchedTemplatesWithSecondaryMatchInfo.Count > 0;
            }
        }
    }
}
