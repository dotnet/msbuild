using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;

namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    public class CreateMSDeployScript : Task
    {
        [Required]
        public string ProjectName { get; set; }

        [Required]
        public string ScriptFullPath { get; set; }

        [Required]
        public string ReadMeFullPath { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(ScriptFullPath))
            {
                File.Create(ScriptFullPath);
            }

            File.WriteAllLines(ScriptFullPath, GetReplacedFileContents(Resources.MsDeployBatchFile));

            if (!File.Exists(ReadMeFullPath))
            {
                File.Create(ReadMeFullPath);
            }

            File.WriteAllLines(ReadMeFullPath, GetReplacedFileContents(Resources.MsDeployReadMe));

            return true;
        }

        private string[] GetReplacedFileContents(string fileContents)
        {
            var lines = fileContents.Split(new string[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i]?.Replace("$$ProjectName$$", ProjectName);
            }

            return lines;
        }
    }
}
