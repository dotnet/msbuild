// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;

namespace Microsoft.DotNet.Tools.Ref
{
    public class RefCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            DirectoryInfo currDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            FileInfo projectFile = currDir.GetFiles("*.csproj").FirstOrDefault();

            if (!projectFile.Exists)
            {
                throw new Exception("Unable to find any projects in the current folder");
            }

            CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: false)
            {
                Name = "dotnet ref",
                FullName = ".NET Ref Command",
                Description = "Command to modify project and package references",
                AllowArgumentSeparator = true,
                ArgumentSeparatorHelpText = HelpMessageStrings.MSBuildAdditionalArgsHelpText
            };
            app.HelpOption("-h|--help");

            app.Command("add", c =>
            {
                c.Description = "Add package and project references";
                c.HelpOption("-h|--help");

                CommandArgument packageNameArg = c.Argument("<PACKAGE_NAME>", "The package name to install");
                CommandOption packageVersionOption = c.Option("-v|--version <PACKAGE_VERSION>", "The package version to install, defaults to * if omited", CommandOptionType.SingleValue);

                c.OnExecute(() =>
                {

                    string version = "*";
                    if (packageVersionOption.HasValue())
                    {
                        version = packageVersionOption.Value();
                    }

                    var rootElement = ProjectRootElement.Open(projectFile.FullName);
                    AddOrUpdatePackageRef(packageNameArg.Value, version, rootElement);         
                    rootElement.Save();

                    // List<string> msbuildArgs = new List<string>();
                    // msbuildArgs.Add("/t:RefAdd");
                    // msbuildArgs.Add($"/p:PackageName={packageNameArg.Value}");
                    // msbuildArgs.Add($"/p:PackageVersion={version}");
                    // msbuildArgs.AddRange(app.RemainingArguments);
                    // return new MSBuildForwardingApp(msbuildArgs).Execute();
                    return 1;
                });
                
            });            

            app.Command("del", c =>
            {
                c.Description = "Remove package and project references";
                c.HelpOption("-h|--help");

                CommandArgument packageNameArg = c.Argument("<PACKAGE_NAME>", "The package name to remove");

                c.OnExecute(() =>
                {
                    var rootElement = ProjectRootElement.Open(projectFile.FullName);
                    RemovePackageRef(packageNameArg.Value, rootElement);
                    rootElement.Save();

                    // List<string> msbuildArgs = new List<string>();
                    // msbuildArgs.Add("/t:RefDel");                    
                    // msbuildArgs.Add($"/p:PackageName={packageNameArg.Value}");
                    // msbuildArgs.AddRange(app.RemainingArguments);
                    // return new MSBuildForwardingApp(msbuildArgs).Execute();
                    return 1;
                });
                
            });            

            return app.Execute(args);
        }

        private static void RemovePackageRef(string packageName, ProjectRootElement proj)
        {            
            //find existing ones
            var packageRefs = proj.Items.Where(i => i.ItemType == "PackageReference")
                .Where(x => x.Include == packageName)
                .ToList();
            //remove any existing ones
            foreach (var packageRef in packageRefs)
            {
                var parent = packageRef.Parent;
                packageRef.Parent.RemoveChild(packageRef);
                parent.RemoveIfEmpty();
            }
        }

        private static void AddOrUpdatePackageRef(string packageName, string packageVersion, ProjectRootElement proj)
        {            
            RemovePackageRef(packageName, proj);

            //add this one
            proj.AddItem("PackageReference", packageName, new Dictionary<string, string>{{"Version", packageVersion}});
        }
    }
}
