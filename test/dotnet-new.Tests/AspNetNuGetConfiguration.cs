using System.IO;

namespace Microsoft.DotNet.New.Tests
{
    public class AspNetNuGetConfiguration
    {
        public static void WriteNuGetConfigWithAspNetPrivateFeeds(string path)
        {
            string resourceName = "dotnet-new.Tests.NuGet.tempaspnetpatch.config";
            using (Stream input = typeof(GivenThatIWantANewAppWithSpecifiedType).Assembly.GetManifestResourceStream(resourceName))
            using (Stream output = File.OpenWrite(path))
            {
                input.CopyTo(output);
            }
        }
    }
}