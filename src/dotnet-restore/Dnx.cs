using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel.Graph;

namespace Microsoft.DotNet.Tools.Restore
{
    public static class Dnx
    {
        public static int RunRestore(IEnumerable<string> args)
        {
            var result = RunDnx(new List<string> {"restore"}.Concat(args))
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();

            return result.ExitCode;
        }

        public static int RunPackageInstall(LibraryRange dependency, string projectPath, IEnumerable<string> args)
        {
            var result = RunDnx(new List<string> { "install", dependency.Name, dependency.VersionRange.OriginalString, projectPath }.Concat(args))
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();

            return result.ExitCode;
        }

        private static Command RunDnx(IEnumerable<string> dnxArgs)
        {
            return Command.Create("dotnet-dnx", dnxArgs);
        }
    }
}