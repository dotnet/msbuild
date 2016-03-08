using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace Microsoft.DotNet.Cli.Utils
{
    internal static class CommandResolver
    {
        public static CommandSpec TryResolveCommandSpec(
            string commandName, 
            IEnumerable<string> args, 
            NuGetFramework framework = null, 
            string configuration=Constants.DefaultConfiguration, 
            string outputPath=null)
        {
            var commandResolverArgs = new CommandResolverArguments
            {
                CommandName = commandName,
                CommandArguments = args,
                Framework = framework,
                ProjectDirectory = Directory.GetCurrentDirectory(),
                Configuration = configuration,
                OutputPath = outputPath
            };
            
            var defaultCommandResolver = DefaultCommandResolverPolicy.Create();
            
            return defaultCommandResolver.Resolve(commandResolverArgs);
        }
        
        public static CommandSpec TryResolveScriptCommandSpec(
            string commandName, 
            IEnumerable<string> args, 
            Project project, 
            string[] inferredExtensionList)
        {
            var commandResolverArgs = new CommandResolverArguments
            {
                CommandName = commandName,
                CommandArguments = args,
                ProjectDirectory = project.ProjectDirectory,
                InferredExtensions = inferredExtensionList
            };

            var scriptCommandResolver = ScriptCommandResolverPolicy.Create();
            
            return scriptCommandResolver.Resolve(commandResolverArgs);
        }
    }
}

