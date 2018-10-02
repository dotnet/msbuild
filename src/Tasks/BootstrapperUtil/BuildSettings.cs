// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    /// <summary>
    /// This class defines the settings for the bootstrapper build operation.
    /// </summary>
    [ComVisible(true), Guid("5D13802C-C830-4b41-8E7A-F69D9DD6A095"), ClassInterface(ClassInterfaceType.None)]
    public class BuildSettings : IBuildSettings
    {
        public BuildSettings()
        {
            ProductBuilders = new ProductBuilderCollection();
        }

        /// <summary>
        /// The name of the application to be installed after the bootstrapper has installed all required components.  If no application is to be installed, this parameter may be null
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// The file to be installed after the bootstrapper has installed the required components.  It is assumed that this file path is relative to the bootstrapper source path.  If no application is to be installed, this parameter may be null
        /// </summary>
        public string ApplicationFile { get; set; }

        /// <summary>
        /// A value of true indicates that the application should require elevation to install on Vista.
        /// </summary>
        public bool ApplicationRequiresElevation { get; set; }

        /// <summary>
        /// The expected source location if the bootstrapper is published to a website.  It is expected that the ApplicationFile, if specified, will be published to the location consistent to this value. If ComponentsLocation is Relative, required component files will also be published in a manner consistent with this value.  This value may be null if setup.exe is not to be published to the web
        /// </summary>
        public string ApplicationUrl { get; set; }

        /// <summary>
        /// Specifies the install time location for bootstrapper components
        /// </summary>
        public ComponentsLocation ComponentsLocation { get; set; } = ComponentsLocation.HomeSite;

        /// <summary>
        /// The location the bootstrapper install time will use for components if ComponentsLocation is "Absolute"
        /// </summary>
        public string ComponentsUrl { get; set; }

        /// <summary>
        /// If true, the bootstrapper components will be copied to the build output directory.  If false, the files will not be copied
        /// </summary>
        public bool CopyComponents { get; set; }

        /// <summary>
        /// The culture identifier for the bootstrapper to be built
        /// </summary>
        public int LCID { get; set; } = Util.DefaultCultureInfo.LCID;

        /// <summary>
        /// The culture identifier to use if the LCID identifier is not available
        /// </summary>
        public int FallbackLCID { get; set; } = Util.DefaultCultureInfo.LCID;

        /// <summary>
        /// The file location to copy output files to
        /// </summary>
        public string OutputPath { get; set; } = null;

        /// <summary>
        /// The product builders to use for generating the bootstrapper
        /// </summary>
        public ProductBuilderCollection ProductBuilders { get; }

        /// <summary>
        /// Specifies a URL for the Web site containing support information for the bootstrapper
        /// </summary>
        public string SupportUrl { get; set; }

        /// <summary>
        /// True if the bootstrapper will perform XML validation on the component manifests
        /// </summary>
        public bool Validate { get; set; }

        internal string Culture { get; set; }

        internal string FallbackCulture { get; set; }
    }
}
