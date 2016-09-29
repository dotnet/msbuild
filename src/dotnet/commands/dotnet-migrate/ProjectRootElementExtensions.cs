using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;

namespace Microsoft.DotNet.Tools.Migrate
{
    public static class ProjectRootElementExtensions
    {
        public static string GetSdkVersion(this ProjectRootElement projectRootElement)
        {
            //TODO: Temporarily pinning the SDK version for Migration. Once we have packageref migration we can remove this.
            return "1.0.0-alpha-20160929-1";

            // return projectRootElement
            //     .Items
            //     .Where(i => i.ItemType == "PackageReference")
            //     .First(i => i.Include == ConstantPackageNames.CSdkPackageName)
            //     .GetMetadataWithName("version").Value;
        }
    }
}