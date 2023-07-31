// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class ApplyCssScopes : Task
    {
        [Required]
        public ITaskItem[] RazorComponents { get; set; }

        [Required]
        public ITaskItem[] RazorGenerate { get; set; }

        [Required]
        public ITaskItem[] ScopedCss { get; set; }

        [Output]
        public ITaskItem[] RazorComponentsWithScopes { get; set; }

        [Output]
        public ITaskItem[] RazorGenerateWithScopes { get; set; }

        public override bool Execute()
        {
            var razorComponentsWithScopes = new List<ITaskItem>();
            var razorGenerateWithScopes = new List<ITaskItem>();
            var unmatchedScopedCss = new List<ITaskItem>(ScopedCss);
            var scopedCssByRazorItem = new Dictionary<string, IList<ITaskItem>>();

            for (var i = 0; i < RazorComponents.Length; i++)
            {
                var componentCandidate = RazorComponents[i];
                MatchScopedCssFiles(
                    razorComponentsWithScopes,
                    componentCandidate,
                    unmatchedScopedCss,
                    scopedCssByRazorItem,
                    "RazorComponent",
                    "(.*)\\.razor\\.css$",
                    "$1.razor");
            }

            for (var i = 0; i < RazorGenerate.Length; i++)
            {
                var razorViewCandidate = RazorGenerate[i];
                MatchScopedCssFiles(
                    razorGenerateWithScopes,
                    razorViewCandidate,
                    unmatchedScopedCss,
                    scopedCssByRazorItem,
                    "View",
                    "(.*)\\.cshtml\\.css$",
                    "$1.cshtml");
            }

            foreach (var kvp in scopedCssByRazorItem)
            {
                if (RazorComponents.Any(rc => string.Equals(rc.ItemSpec, kvp.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    var component = kvp.Key;
                    var scopeFiles = kvp.Value;

                    if (scopeFiles.Count > 1)
                    {
                        Log.LogError(null, "BLAZOR101", "", component, 0, 0, 0, 0, $"More than one scoped css files were found for the razor component '{component}'. " +
                            $"Each razor component must have at most a single associated scoped css file." +
                            Environment.NewLine +
                            string.Join(Environment.NewLine, scopeFiles.Select(f => f.ItemSpec)));
                    }
                }
                else
                {
                    var view = kvp.Key;
                    var scopeFiles = kvp.Value;

                    if (scopeFiles.Count > 1)
                    {
                        Log.LogError(null, "RZ1007", "", view, 0, 0, 0, 0, $"More than one scoped css files were found for the razor view '{view}'. " +
                            $"Each razor view must have at most a single associated scoped css file." +
                            Environment.NewLine +
                            string.Join(Environment.NewLine, scopeFiles.Select(f => f.ItemSpec)));
                    }
                }
            }

            // We don't want to allow scoped css files without a matching component. Our convention is very specific in its requirements
            // so failing to have a matching component very likely means an error.
            // When the matching component was specified explicitly, failing to find a matching component is an error.
            // This simplifies a few things like being able to assume that the presence of a .razor.css file or a ScopedCssInput item will result in a bundle being produced,
            // that the contents of the bundle are independent of the existence of a component and that users will be able to catch errors at compile
            // time instead of wondering why their component doesn't have a scope applied to it.
            // In the rare case that a .razor file exists on the user project, has an associated .razor.css file and the user decides to exclude it as a RazorComponent they
            // can update the Content item for the .razor.css file with Scoped=false and we will not consider it.
            foreach (var unmatched in unmatchedScopedCss)
            {
                Log.LogError(null, "BLAZOR102", "", unmatched.ItemSpec, 0, 0, 0, 0, $"The scoped css file '{unmatched.ItemSpec}' was defined but no associated razor component or view was found for it.");
            }

            RazorComponentsWithScopes = razorComponentsWithScopes.ToArray();
            RazorGenerateWithScopes = razorGenerateWithScopes.ToArray();

            return !Log.HasLoggedErrors;
        }

        private static void MatchScopedCssFiles(
            List<ITaskItem> itemsWithScopes,
            ITaskItem itemCandidate,
            List<ITaskItem> unmatchedScopedCss,
            Dictionary<string, IList<ITaskItem>> scopedCssByItem,
            string explicitMetadataName,
            string candidateMatchPattern,
            string replacementExpression)
        {
            var j = 0;
            while (j < unmatchedScopedCss.Count)
            {
                var scopedCssCandidate = unmatchedScopedCss[j];
                var explicitRazorItem = scopedCssCandidate.GetMetadata(explicitMetadataName);
                var razorItem = !string.IsNullOrWhiteSpace(explicitRazorItem) ?
                    explicitRazorItem :
                    Regex.Replace(scopedCssCandidate.ItemSpec, candidateMatchPattern, replacementExpression, RegexOptions.IgnoreCase);

                if (string.Equals(itemCandidate.ItemSpec, razorItem, StringComparison.OrdinalIgnoreCase))
                {
                    unmatchedScopedCss.RemoveAt(j);
                    if (!scopedCssByItem.TryGetValue(itemCandidate.ItemSpec, out var existing))
                    {
                        scopedCssByItem[itemCandidate.ItemSpec] = new List<ITaskItem>() { scopedCssCandidate };
                        var item = new TaskItem(itemCandidate);
                        item.SetMetadata("CssScope", scopedCssCandidate.GetMetadata("CssScope"));
                        itemsWithScopes.Add(item);
                    }
                    else
                    {
                        existing.Add(scopedCssCandidate);
                    }
                }
                else
                {
                    j++;
                }
            }
        }
    }
}
