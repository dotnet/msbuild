using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.Publish.Tasks.Tests.EndToEnd
{
    public class Initialize21Templates: IDisposable
    {
        public const string DotNet21SdkVersion = "2.1.502";
        public Initialize21Templates()
        {
            string dotNetInstallDir = Environment.GetEnvironmentVariable("DOTNET_INSTALL_DIR");
            if (string.IsNullOrEmpty(dotNetInstallDir))
            {
                // Handle scenarios where environment is not initialized.
                dotNetInstallDir = Path.Combine(Environment.GetEnvironmentVariable("localappdata"), "Microsoft", "dotnet");
            }

            string TemplateDir21 = Path.Combine(dotNetInstallDir, "sdk", DotNet21SdkVersion, "Templates");
            //Install the 2.1 templates
            int? exitCode = new ProcessWrapper().RunProcess(FolderPublish31.DotNetExeName, $"new -i \"{TemplateDir21}/*.nupkg\" ", AppContext.BaseDirectory, out int? processId1, createDirectoryIfNotExists: true, waitForExit: true, testOutputHelper: null);
    }

        public void Dispose()
        {
            // ... clean up test data
        }
    }
}
