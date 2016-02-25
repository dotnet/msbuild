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
    public class ScriptCommandResolver : CompositeCommandResolver
    {
        public static ScriptCommandResolver Create()
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

            return new ScriptCommandResolver(environment, platformCommandSpecFactory);
        }

        public ScriptCommandResolver(
            IEnvironmentProvider environment,
            IPlatformCommandSpecFactory platformCommandSpecFactory)
        {
            AddCommandResolver(new RootedCommandResolver());
            AddCommandResolver(new ProjectPathCommandResolver(environment, platformCommandSpecFactory));
            AddCommandResolver(new AppBaseCommandResolver(environment, platformCommandSpecFactory));
            AddCommandResolver(new PathCommandResolver(environment, platformCommandSpecFactory));
        }
    }
}
