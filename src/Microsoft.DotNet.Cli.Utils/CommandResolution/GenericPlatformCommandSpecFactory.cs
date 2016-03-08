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
    public class GenericPlatformCommandSpecFactory : IPlatformCommandSpecFactory
    {
        public CommandSpec CreateCommandSpec(
           string commandName,
           IEnumerable<string> args,
           string commandPath,
           CommandResolutionStrategy resolutionStrategy,
           IEnvironmentProvider environment)
        {
            var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args);
            return new CommandSpec(commandPath, escapedArgs, resolutionStrategy);
        }
    }
}
