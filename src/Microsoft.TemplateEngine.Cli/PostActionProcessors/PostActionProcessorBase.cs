using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal abstract class PostActionProcessor2Base
    {
        protected internal New3Callbacks Callbacks { get; set; }

        protected IReadOnlyList<string> GetTargetForSource(ICreationEffects2 creationEffects, string sourcePathGlob)
        {
            Glob g = Glob.Parse(sourcePathGlob);
            List<string> results = new List<string>();

            if (creationEffects.FileChanges != null)
            {
                foreach (IFileChange2 change in creationEffects.FileChanges)
                {
                    if (g.IsMatch(change.SourceRelativePath))
                    {
                        results.Add(change.TargetRelativePath);
                    }
                }
            }

            return results;
        }
    }
}
