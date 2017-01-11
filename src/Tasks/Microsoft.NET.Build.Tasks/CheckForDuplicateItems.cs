using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Microsoft.NET.Build.Tasks
{
    public class CheckForDuplicateItems : TaskBase
    {
        [Required]
        public ITaskItem [] Items { get; set; }

        [Required]
        public string ItemName { get; set; }

        public bool DefaultItemsEnabled { get; set; }

        public bool DefaultItemsOfThisTypeEnabled { get; set; }

        public bool DefaultItemsWereIncluded { get; set; }

        public string PropertyNameToDisableDefaultItems { get; set; }

        public string PropertyValueToDisableDefaultItems { get; set; } = "false";

        public string MoreInformationLink { get; set; }

        protected override void ExecuteCore()
        {
            var duplicateItems = Items.GroupBy(i => i.ItemSpec).Where(g => g.Count() > 1).ToList();
            if (duplicateItems.Any())
            {
                string defaultExplanation = "";
                if (DefaultItemsEnabled && DefaultItemsOfThisTypeEnabled)
                {
                    string moreInformation = "";
                    if (!string.IsNullOrEmpty(MoreInformationLink))
                    {
                        moreInformation = string.Format(CultureInfo.CurrentCulture, Strings.ForMoreInformation, MoreInformationLink);
                    }

                    defaultExplanation = string.Format(CultureInfo.CurrentCulture, Strings.DuplicateItemsDefaultExplanation, ItemName);

                    defaultExplanation += string.Format(CultureInfo.CurrentCulture, Strings.DuplicateItemsHowToFix,
                        PropertyNameToDisableDefaultItems,
                        PropertyValueToDisableDefaultItems,
                        moreInformation);
                }

                //  TODO: Does quoting and the separator between items in the list need to be localized?
                string duplicateItemsFormatted = string.Join(", ", duplicateItems.Select(d => $"'{d.Key}'"));

                string message = string.Format(CultureInfo.CurrentCulture, Strings.DuplicateItemsError,
                    ItemName,
                    defaultExplanation,
                    duplicateItemsFormatted);

                Log.LogError(message);
            }
        }
    }
}
