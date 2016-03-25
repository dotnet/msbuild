using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.Extensions.PlatformAbstractions;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli.Build
{
    public class DotNetCli
    {
        public static readonly DotNetCli Stage0 = new DotNetCli(GetStage0Path());
        public static readonly DotNetCli Stage1 = new DotNetCli(Dirs.Stage1);
        public static readonly DotNetCli Stage2 = new DotNetCli(Dirs.Stage2);

        public string BinPath { get; }

        public DotNetCli(string binPath)
        {
            BinPath = binPath;
        }

        public Command Exec(string command, params string[] args)
        {
            var newArgs = args.ToList();
            newArgs.Insert(0, command);

            if (EnvVars.Verbose)
            {
                newArgs.Insert(0, "-v");
            }

            return Command.Create(Path.Combine(BinPath, $"dotnet{Constants.ExeSuffix}"), newArgs);
        }

        public Command Restore(params string[] args) => Exec("restore", args);
        public Command Build(params string[] args) => Exec("build", args);
        public Command Pack(params string[] args) => Exec("pack", args);
        public Command Test(params string[] args) => Exec("test", args);
        public Command Publish(params string[] args) => Exec("publish", args);

        private static string GetStage0Path()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(Directory.GetCurrentDirectory(), ".dotnet_stage0",
                    PlatformServices.Default.Runtime.OperatingSystemPlatform.ToString(),
                    PlatformServices.Default.Runtime.RuntimeArchitecture);
            }
            else
            {
                return Path.Combine(Directory.GetCurrentDirectory(), ".dotnet_stage0", PlatformServices.Default.Runtime.OperatingSystemPlatform.ToString());
            }

        }
    }
}
