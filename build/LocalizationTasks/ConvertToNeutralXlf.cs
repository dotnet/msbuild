using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.LocalizationTasks
{
    public class ConvertToNeutralXlf : Task
    {
        /// <summary>
        /// Path to the neutral resx files to generate a corresponding neutral xlf from.
        /// </summary>
        [Required]
        public ITaskItem[] NeutralResources { get; set; }

        public override bool Execute()
        {
            if (NeutralResources.Length == 0)
            {
                Log.LogError($"Task was called with empty {nameof(NeutralResources)}");
            }

            foreach (var neutralResource in NeutralResources)
            {
                var localizedXlf = LocalizationUtils.LocalizedXlfFiles(neutralResource).FirstOrDefault();

                if (localizedXlf == null)
                {
                    Log.LogError($"{neutralResource} has no corresponding xlf files");
                }

                var outputFilename = ComputeNeutralXlfName(neutralResource);

                MakeNeutral(localizedXlf, outputFilename);
            }

            return !Log.HasLoggedErrors;
        }

        private string ComputeNeutralXlfName(ITaskItem neutralResouce)
        {
            var filename = neutralResouce.GetMetadata("Filename");
            var xlfRootPath = LocalizationUtils.ComputeXlfRootPath(neutralResouce);

            return Path.Combine(xlfRootPath, filename + ".xlf");
        }

        private static void MakeNeutral(string inputfilename, string outputfilename)
        {
            //need to load xml file
            var doc = XDocument.Load(inputfilename);

            //step 1: remove  target-language attribute
            //< file datatype = "xml" source - language = "en" target - language = "cs" original = "../Strings.shared.resx" >
            var fileNodes = from node in doc.Descendants()
                where node.Name.LocalName != null && node.Name.LocalName == "file"
                select node;
            fileNodes.ToList().ForEach(x =>
            {
                if (x.HasAttributes)
                {
                    foreach (var attrib in x.Attributes())
                    {
                        if (attrib.Name == "target-language")
                            attrib.Remove();
                    }
                }
            });

            //step 2: remove all tags with "target"
            // < target state = "new" > MSBuild is expecting a valid "{0}" object.</ target >
            var targetNodes = from node in doc.Descendants()
                where node.Name.LocalName != null && node.Name.LocalName == "target"
                select node;
            targetNodes.ToList().ForEach(x => x.Remove());

            //save
            var fi = new FileInfo(outputfilename);

            if (fi.Exists)
            {
                fi.Delete();
            }
            if (fi.Directory.Exists == false)
            {
                fi.Directory.Create();
            }

            doc.Save(fi.FullName);
        }
    }
}