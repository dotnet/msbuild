using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.LocalizationTasks
{
    public class EmitLocalizedResources : Task
    {
        private static readonly string NeutralResxMetadata = "NeutralResx";
        private static readonly string ParentXlfMetadata = "ParentXlf";
        private static readonly string ChildResxMetadata = "ChildResx";
        private static readonly string ComputedCultureCodeMetadata = "ComputedCulture";
        private static readonly string LogicalNameMetadata = "LogicalName";

        [Required]
        public ITaskItem[] NeutralResources { get; set; }

        /// <summary>
        /// Name of the main assembly
        /// </summary>
        [Required]
        public string AssemblyName { get; set; }

        /// <summary>
        /// Directory path where localized resx should be generated in 
        /// </summary>
        [Required]
        public string LocalizedResxRoot { get; set; }

        /// <summary>
        /// Path to the root directory that contains the xlf files for a neutral resource
        /// Assumes all neutral resource have the same relative path to their xlf
        /// </summary>
        [Required]
        public string RelativePathToXlfRoot { get; set; }

        [Output]
        public ITaskItem[] ResolvedXlfResources { get; set; }

        [Output]
        public ITaskItem[] ResolvedLocalizedResxResources { get; set; }

        public override bool Execute()
        {
            if (!NeutralResources.Any())
            {
                Log.LogMessage("No neutral resources found");
                return true;
            }

            ResolvedXlfResources = NeutralResources.SelectMany(ComputeXlfResourceItems).ToArray();

            ResolvedLocalizedResxResources = ResolvedXlfResources.Select(ComputeLocalizedResource).ToArray();

            return !Log.HasLoggedErrors;
        }

        private ITaskItem ComputeLocalizedResource(ITaskItem xlf)
        {
            var resxName = xlf.GetMetadata("FileName");
            var resxFileName = resxName + ".resx";
            var resxPath = Path.Combine(LocalizedResxRoot, resxFileName);

            var resxItem = new TaskItem(resxPath);
            resxItem.SetMetadata(NeutralResxMetadata, xlf.GetMetadata(NeutralResxMetadata));
            resxItem.SetMetadata(ParentXlfMetadata, xlf.ItemSpec);
            resxItem.SetMetadata(ComputedCultureCodeMetadata, xlf.GetMetadata(ComputedCultureCodeMetadata));
            resxItem.SetMetadata(LogicalNameMetadata, $"{AssemblyName}.{resxName}.resources");

            xlf.SetMetadata(ChildResxMetadata, resxPath);

            return resxItem;
        }

        private IEnumerable<ITaskItem> ComputeXlfResourceItems(ITaskItem neutralResource)
        {
            var neutralResxRootDirectory = Path.GetDirectoryName(neutralResource.GetMetadata("FullPath"));
            var xlfRootPath = Path.Combine(neutralResxRootDirectory, RelativePathToXlfRoot);

            if (!Directory.Exists(xlfRootPath))
            {
                throw new InvalidOperationException(
                    $"Could not find expected xlf root {xlfRootPath} next to its neutral resource {neutralResource.ItemSpec}");
            }

            var resx = neutralResource.ItemSpec;

            return
                (Directory.EnumerateFiles(xlfRootPath)
                    .Where(f => IsValidXlf(f, resx))
                    .Select((f => CreateXLFTaskItemForNeutralResx(f, resx))));
        }

        private bool IsValidXlf(string xlfPath, string resx)
        {
            var resxFileName = Path.GetFileNameWithoutExtension(resx);
            var xlfFileName = Path.GetFileName(xlfPath);

            return Regex.IsMatch(xlfFileName, $"^{resxFileName}\\.[a-zA-Z\\-]+\\.xlf$");
        }

        private ITaskItem CreateXLFTaskItemForNeutralResx(string xlfPath, string neutralResx)
        {
            var xlfItem = new TaskItem(xlfPath);
            xlfItem.SetMetadata(NeutralResxMetadata, neutralResx);
            xlfItem.SetMetadata(ComputedCultureCodeMetadata, GetCultureCodeFromPath(xlfPath));

            return xlfItem;
        }

        private static string GetCultureCodeFromPath(string localizedResourcePath)
        {
            return Path.GetExtension(Path.GetFileNameWithoutExtension(localizedResourcePath)).Substring(1); // remove the . at the beginning
        }
    }
}