using System;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var environment = new EnvironmentProvider();
            var packagedCommandSpecFactory = new PackagedCommandSpecFactory();

            var projectDependenciesCommandFactory = new ProjectDependenciesCommandFactory(
                FrameworkConstants.CommonFrameworks.NetCoreApp10,
                "Debug",
                null,
                null,
                Directory.GetCurrentDirectory());

            var command = projectDependenciesCommandFactory.Create("dotnet-portable", null);

            command.Execute();
        }
    }
}
