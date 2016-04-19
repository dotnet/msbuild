using System;
using Microsoft.DotNet.Cli.Utils;
using System.IO;

namespace DesktopAppWhichCallsDotnet
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var projectPath = args[0];
            
            return Command.CreateDotNet("build", new string[] {})
                .WorkingDirectory(Path.GetDirectoryName(projectPath))
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute()
                .ExitCode;
        }
    }
}
