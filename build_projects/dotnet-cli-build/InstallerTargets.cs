using Microsoft.DotNet.Cli.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    public class InstallerTargets
    {
        public static BuildTargetResult GenerateInstaller(BuildTargetContext c)
        {
            MsiTargets.GenerateMsisAndBundles(c);
            PkgTargets.GeneratePkgs(c);
            DebTargets.GenerateDebs(c);

            return c.Success();
        }

        public static BuildTargetResult TestInstaller(BuildTargetContext c)
        {
            DebTargets.TestDebInstaller(c);

            return c.Success();
        }
    }
}
