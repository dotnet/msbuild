using Microsoft.DotNet.Cli.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    public class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            return BuildSetup.Create(".NET Core CLI")
                .UseStandardGoals()
                .UseAllTargetsFromAssembly<Program>()
                .Run(args);
        } 
    }
}
