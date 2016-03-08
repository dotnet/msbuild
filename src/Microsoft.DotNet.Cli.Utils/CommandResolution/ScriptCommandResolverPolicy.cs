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
    public class ScriptCommandResolverPolicy
    {
        public static CompositeCommandResolver Create()
        {
            var environment = new EnvironmentProvider();

            var platformCommandSpecFactory = default(IPlatformCommandSpecFactory);
            if (PlatformServices.Default.Runtime.OperatingSystemPlatform == Platform.Windows)
            {
                platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();
            }
            else
            {
                platformCommandSpecFactory = new GenericPlatformCommandSpecFactory();
            }

            return CreateScriptCommandResolver(environment, platformCommandSpecFactory);
        }

        public static CompositeCommandResolver CreateScriptCommandResolver(
            IEnvironmentProvider environment,
            IPlatformCommandSpecFactory platformCommandSpecFactory)
        {
            var compositeCommandResolver = new CompositeCommandResolver();

            compositeCommandResolver.AddCommandResolver(new RootedCommandResolver());
            compositeCommandResolver.AddCommandResolver(new ProjectPathCommandResolver(environment, platformCommandSpecFactory));
            compositeCommandResolver.AddCommandResolver(new AppBaseCommandResolver(environment, platformCommandSpecFactory));
            compositeCommandResolver.AddCommandResolver(new PathCommandResolver(environment, platformCommandSpecFactory));
        
            return compositeCommandResolver;
        }
    }
}
