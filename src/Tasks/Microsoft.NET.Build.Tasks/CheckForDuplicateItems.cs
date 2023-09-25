// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class CheckForDuplicateItems : TaskBase
    {
        [Required]
        public ITaskItem[] Items { get; set; }

        [Required]
        public string ItemName { get; set; }

        public bool DefaultItemsEnabled { get; set; }

        public bool DefaultItemsOfThisTypeEnabled { get; set; }

        [Required]
        public string PropertyNameToDisableDefaultItems { get; set; }

        public string PropertyValueToDisableDefaultItems { get; set; } = "false";

        [Required]
        public string MoreInformationLink { get; set; }

        [Output]
        public ITaskItem[] DeduplicatedItems { get; set; }

        protected override void ExecuteCore()
        {
            DeduplicatedItems = Array.Empty<ITaskItem>();

            if (DefaultItemsEnabled && DefaultItemsOfThisTypeEnabled)
            {
                var itemGroups = Items.GroupBy(i => i.ItemSpec, StringComparer.OrdinalIgnoreCase);

                var duplicateItems = itemGroups.Where(g => g.Count() > 1).ToList();
                if (duplicateItems.Any())
                {
                    string duplicateItemsFormatted = string.Join("; ", duplicateItems.Select(d => $"'{d.Key}'"));

                    string message = string.Format(CultureInfo.CurrentCulture, Strings.DuplicateItemsError,
                        ItemName,
                        PropertyNameToDisableDefaultItems,
                        PropertyValueToDisableDefaultItems,
                        duplicateItemsFormatted,
                        MoreInformationLink);

                    Log.LogError(message);

                    DeduplicatedItems = itemGroups.Select(g => g.First()).ToArray();
                }
            }
        }
    }
}
