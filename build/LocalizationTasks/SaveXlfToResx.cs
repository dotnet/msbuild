using System;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using XliffParser;

namespace Microsoft.Build.LocalizationTasks
{
    public class SaveXlfToResx : Task
    {
        [Required]
        public string ResxPath { get; set; }

        [Required]
        public string XlfPath { get; set; }

        /// <summary>
        /// When set to "true" it makes the task replace the resource values with another value.
        /// Useful for testing localized builds.
        /// 
        /// If <see cref="TestString"/> is non empty, resource values get replaced with it.
        /// If it is empty, resource values get replaced with !resource_id!english_resource!localized_resource!
        /// </summary>
        public string ReplaceWithTestString { get; set; }

        public string TestString { get; set; }

        public override bool Execute()
        {
            var xlfDocument = new XlfDocument(XlfPath);

            if (!string.IsNullOrEmpty(ReplaceWithTestString) && ReplaceWithTestString.Equals("true"))
            {
                ReplaceResourceValuesWithTestString(xlfDocument);
            }

            xlfDocument.SaveAsResX(ResxPath);

            return !Log.HasLoggedErrors;
        }

        private void ReplaceResourceValuesWithTestString(XlfDocument xlfDocument)
        {
            foreach (var transUnit in xlfDocument.Files.SelectMany(f => f.TransUnits))
            {
                transUnit.Target = string.IsNullOrEmpty(TestString) ? DebugString(transUnit) : TestString;
            }
        }

        private static string DebugString(XlfTransUnit transUnit)
        {
            return "!" + transUnit.Id + "!" + transUnit.Source + "!" + transUnit.Target + "!";
        }
    }
}