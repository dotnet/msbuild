using System.IO;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Publishing.Tasks.Core.Collect
{
    public class WebConfigTransform : Build.Utilities.Task
    {
        public const string WebConfigFileName = "web.config";
        public const string DotNetExecutableName = "dotnet";

        [Required]
        public string ProjectDirectory
        {
            get;
            set;
        }

        [Required]
        public string PublishDir
        {
            get;
            set;
        }

        [Required]
        public string AssemblyName
        {
            get;
            set;
        }

        [Required]
        public bool IsPortableApp
        {
            get;
            set;
        }

        public override bool Execute()
        {
            string sourceWebConfigPath = Path.Combine(ProjectDirectory, WebConfigFileName);
            if (File.Exists(sourceWebConfigPath))
            {
                string destinationWebConfigPath = Path.Combine(PublishDir, WebConfigFileName);
                File.Copy(sourceWebConfigPath, destinationWebConfigPath, true);

                string webConfigContents = File.ReadAllText(destinationWebConfigPath);
                //< aspNetCore processPath = "%LAUNCHER_PATH%" arguments = "%LAUNCHER_ARGS%" stdoutLogEnabled = "false" stdoutLogFile = ".\logs\stdout" forwardWindowsAuthToken = "false" />

                if (IsPortableApp)
                {
                    webConfigContents = webConfigContents.Replace("%LAUNCHER_PATH%", DotNetExecutableName);
                    webConfigContents = webConfigContents.Replace("%LAUNCHER_ARGS%", AssemblyName + ".dll");
                }
                else
                {
                    webConfigContents = webConfigContents.Replace("%LAUNCHER_PATH%", Path.GetFileNameWithoutExtension(AssemblyName));
                }

                File.WriteAllText(destinationWebConfigPath, webConfigContents);
            }

            return true; 
        }
    }
}
