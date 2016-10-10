using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using XliffParser;

namespace Microsoft.Build.LocalizationTasks
{
    public class UpdateXlfFromResx : Task
    {
        [Required]
        public string ResxPath { get; set; }

        [Required]
        public string XlfPath { get; set; }

        public override bool Execute()
        {
            var xlfDocument = new XlfDocument(XlfPath);
            var result = xlfDocument.Update(ResxPath, "updated", "new");

            Log.LogMessage(MessageImportance.Low,
                "Update results:" +
                $"\n\t{result.UpdatedItems} resources updated" +
                $"\n\t{result.AddedItems} resources added" +
                $"\n\t{result.RemovedItems} resources deleted");

            xlfDocument.Save();

            return !Log.HasLoggedErrors;
        }
    }
}