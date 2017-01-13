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

        [Required]
        public string PropertyNameToDisableDefaultItems { get; set; }

        public string PropertyValueToDisableDefaultItems { get; set; } = "false";

        [Required]
        public string MoreInformationLink { get; set; }

        protected override void ExecuteCore()
        {
            if (DefaultItemsEnabled && DefaultItemsOfThisTypeEnabled)
            {
                var duplicateItems = Items.GroupBy(i => i.ItemSpec).Where(g => g.Count() > 1).ToList();
                if (duplicateItems.Any())
                {
                    string duplicateItemsFormatted = string.Join("; ", duplicateItems.Select(d => $"'{d.Key}'"));

                    string message = string.Format(CultureInfo.CurrentCulture, Strings.DuplicateItemsError,
                        ItemName,
                        PropertyNameToDisableDefaultItems,
                        PropertyValueToDisableDefaultItems,
                        duplicateItemsFormatted);

                    Log.LogError(message);
                }
            }
        }
    }
}
