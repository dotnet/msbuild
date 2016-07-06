using Microsoft.DotNet.Cli.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    public class InstallerTargets
    {
        public static BuildTargetResult GenerateInstaller(BuildTargetContext c)
        {
            var debTargets = new DebTargets
            {
                CLISDKRoot = c.BuildContext.Get<string>("CLISDKRoot")
            };

            MsiTargets.GenerateMsisAndBundles(c);
            PkgTargets.GeneratePkgs(c);
            debTargets.GenerateDebs(c);

            return c.Success();
        }

        public static BuildTargetResult TestInstaller(BuildTargetContext c)
        {
            DebTargets.TestDebInstaller(c);

            return c.Success();
        }
    }
}
