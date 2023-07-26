// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class ApplyJsModules : Task
    {
        [Required]
        public ITaskItem[] RazorComponents { get; set; }

        [Required]
        public ITaskItem[] RazorGenerate { get; set; }

        [Required]
        public ITaskItem[] JSFileModuleCandidates { get; set; }

        [Output]
        public ITaskItem[] JsFileModules { get; set; }

        public override bool Execute()
        {
            var razorComponentsWithJsModules = new List<ITaskItem>();
            var razorGenerateWithJsModules = new List<ITaskItem>();
            var unmatchedJsModules = new List<ITaskItem>(JSFileModuleCandidates);
            var jsModulesByRazorItem = new Dictionary<string, IList<ITaskItem>>();

            for (var i = 0; i < RazorComponents.Length; i++)
            {
                var componentCandidate = RazorComponents[i];
                MatchJsModuleFiles(
                    razorComponentsWithJsModules,
                    componentCandidate,
                    unmatchedJsModules,
                    jsModulesByRazorItem,
                    "RazorComponent",
                    "(.*)\\.razor\\.js$",
                    "$1.razor");
            }

            for (var i = 0; i < RazorGenerate.Length; i++)
            {
                var razorViewCandidate = RazorGenerate[i];
                MatchJsModuleFiles(
                    razorGenerateWithJsModules,
                    razorViewCandidate,
                    unmatchedJsModules,
                    jsModulesByRazorItem,
                    "View",
                    "(.*)\\.cshtml\\.js$",
                    "$1.cshtml");
            }

            foreach (var kvp in jsModulesByRazorItem)
            {
                if (RazorComponents.Any(rc => string.Equals(rc.ItemSpec, kvp.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    var component = kvp.Key;
                    var jsModuleFiles = kvp.Value;

                    if (jsModuleFiles.Count > 1)
                    {
                        Log.LogError(null, "BLAZOR105", "", component, 0, 0, 0, 0, $"More than one JS module files were found for the razor component '{component}'. " +
                            $"Each razor component must have at most a single associated JS module file." +
                            Environment.NewLine +
                            string.Join(Environment.NewLine, jsModuleFiles.Select(f => f.ItemSpec)));
                    }
                }
                else
                {
                    var view = kvp.Key;
                    var jsModuleFiles = kvp.Value;

                    if (jsModuleFiles.Count > 1)
                    {
                        Log.LogError(null, "RZ1007", "", view, 0, 0, 0, 0, $"More than one JS module files were found for the razor view '{view}'. " +
                            $"Each razor view must have at most a single associated JS module file." +
                            Environment.NewLine +
                            string.Join(Environment.NewLine, jsModuleFiles.Select(f => f.ItemSpec)));
                    }
                }
            }

            foreach (var unmatched in unmatchedJsModules)
            {
                Log.LogError(null, "BLAZOR106", "", unmatched.ItemSpec, 0, 0, 0, 0, $"The JS module file '{unmatched.ItemSpec}' was defined but no associated razor component or view was found for it.");
            }

            JsFileModules = jsModulesByRazorItem.Values.SelectMany(e => e).ToArray();

            return !Log.HasLoggedErrors;
        }

        private static void MatchJsModuleFiles(
            List<ITaskItem> itemsWithScopes,
            ITaskItem itemCandidate,
            List<ITaskItem> unmatchedJsModules,
            Dictionary<string, IList<ITaskItem>> jsModuleByItem,
            string explicitMetadataName,
            string candidateMatchPattern,
            string replacementExpression)
        {
            var i = 0;
            while (i < unmatchedJsModules.Count)
            {
                var jsModuleCandidate = unmatchedJsModules[i];
                var explicitRazorItem = jsModuleCandidate.GetMetadata(explicitMetadataName);
                var jsModuleCandidatePath = jsModuleCandidate.GetMetadata("RelativePath");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    jsModuleCandidatePath = jsModuleCandidatePath.Replace('/', '\\');
                }

                var razorItem = !string.IsNullOrWhiteSpace(explicitRazorItem) ?
                    explicitRazorItem :
                    Regex.Replace(jsModuleCandidatePath, candidateMatchPattern, replacementExpression, RegexOptions.IgnoreCase);

                if (string.Equals(itemCandidate.ItemSpec, razorItem, StringComparison.OrdinalIgnoreCase))
                {
                    unmatchedJsModules.RemoveAt(i);
                    if (!jsModuleByItem.TryGetValue(itemCandidate.ItemSpec, out var existing))
                    {
                        jsModuleByItem[itemCandidate.ItemSpec] = new List<ITaskItem>() { jsModuleCandidate };
                        var item = new TaskItem(itemCandidate);
                        item.SetMetadata("JSModule", jsModuleCandidate.GetMetadata("JSModule"));
                        itemsWithScopes.Add(item);
                    }
                    else
                    {
                        existing.Add(jsModuleCandidate);
                    }
                }
                else
                {
                    i++;
                }
            }
        }
    }
}
