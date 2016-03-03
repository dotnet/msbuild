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
    public class DefaultCommandResolver : CompositeCommandResolver
    {
        public static DefaultCommandResolver Create()
        {
            var environment = new EnvironmentProvider();
            var packagedCommandSpecFactory = new PackagedCommandSpecFactory();

            var platformCommandSpecFactory = default(IPlatformCommandSpecFactory);
            if (PlatformServices.Default.Runtime.OperatingSystemPlatform == Platform.Windows)
            {
                platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();
            }
            else
            {
                platformCommandSpecFactory = new GenericPlatformCommandSpecFactory();
            }

            return new DefaultCommandResolver(environment, packagedCommandSpecFactory, platformCommandSpecFactory);
        }

        public DefaultCommandResolver(
            IEnvironmentProvider environment,
            IPackagedCommandSpecFactory packagedCommandSpecFactory,
            IPlatformCommandSpecFactory platformCommandSpecFactory) : base()
        {
            AddCommandResolver(new RootedCommandResolver());
            AddCommandResolver(new ProjectToolsCommandResolver(packagedCommandSpecFactory));
            AddCommandResolver(new AppBaseCommandResolver(environment, platformCommandSpecFactory));
            AddCommandResolver(new PathCommandResolver(environment, platformCommandSpecFactory));
        }
    }
}
