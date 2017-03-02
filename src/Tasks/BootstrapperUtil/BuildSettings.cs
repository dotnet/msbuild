// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    /// <summary>
    /// This class defines the settings for the bootstrapper build operation.
    /// </summary>
    [ComVisible(true), GuidAttribute("5D13802C-C830-4b41-8E7A-F69D9DD6A095"), ClassInterface(ClassInterfaceType.None)]
    public class BuildSettings : IBuildSettings
    {
        private string _applicationName = null;
        private string _applicationFile = null;
        private bool _applicationRequiresElevation = false;
        private string _applicationUrl = null;
        private ComponentsLocation _componentsLocation = ComponentsLocation.HomeSite;
        private string _componentsUrl = null;
        private bool _fCopyComponents = false;
        private int _lcid = Util.DefaultCultureInfo.LCID;
        private int _fallbackLCID = Util.DefaultCultureInfo.LCID;
        private string _outputPath = null;
        private string _supportUrl = null;
        private ProductBuilderCollection _productBuilders = null;
        private bool _fValidate = false;
        private string _culture = null;
        private string _fallbackCulture = null;

        public BuildSettings()
        {
            _productBuilders = new ProductBuilderCollection();
        }

        /// <summary>
        /// The name of the application to be installed after the bootstrapper has installed all required components.  If no application is to be installed, this parameter may be null
        /// </summary>
        public string ApplicationName
        {
            get { return _applicationName; }
            set { _applicationName = value; }
        }

        /// <summary>
        /// The file to be installed after the bootstrapper has installed the required components.  It is assumed that this file path is relative to the bootstrapper source path.  If no application is to be installed, this parameter may be null
        /// </summary>
        public string ApplicationFile
        {
            get { return _applicationFile; }
            set { _applicationFile = value; }
        }

        /// <summary>
        /// A value of true indicates that the application should require elevation to install on Vista.
        /// </summary>
        public bool ApplicationRequiresElevation
        {
            get { return _applicationRequiresElevation; }
            set { _applicationRequiresElevation = value; }
        }

        /// <summary>
        /// The expected source location if the bootstrapper is published to a website.  It is expected that the ApplicationFile, if specified, will be published to the location consistent to this value. If ComponentsLocation is Relative, required component files will also be published in a manner consistent with this value.  This value may be null if setup.exe is not to be published to the web
        /// </summary>
        public string ApplicationUrl
        {
            get { return _applicationUrl; }
            set { _applicationUrl = value; }
        }

        /// <summary>
        /// Specifies the install time location for bootstrapper components
        /// </summary>
        public ComponentsLocation ComponentsLocation
        {
            get { return _componentsLocation; }
            set { _componentsLocation = value; }
        }

        /// <summary>
        /// The location the bootstrapper install time will use for components if ComponentsLocation is "Absolute"
        /// </summary>
        public string ComponentsUrl
        {
            get { return _componentsUrl; }
            set { _componentsUrl = value; }
        }

        /// <summary>
        /// If true, the bootstrapper components will be copied to the build output directory.  If false, the files will not be copied
        /// </summary>
        public bool CopyComponents
        {
            get { return _fCopyComponents; }
            set { _fCopyComponents = value; }
        }

        /// <summary>
        /// The culture identifier for the bootstrapper to be built
        /// </summary>
        public int LCID
        {
            get { return _lcid; }
            set { _lcid = value; }
        }

        /// <summary>
        /// The culture identifier to use if the LCID identifier is not available
        /// </summary>
        public int FallbackLCID
        {
            get { return _fallbackLCID; }
            set { _fallbackLCID = value; }
        }

        /// <summary>
        /// The file location to copy output files to
        /// </summary>
        public string OutputPath
        {
            get { return _outputPath; }
            set { _outputPath = value; }
        }

        /// <summary>
        /// The product builders to use for generating the bootstrapper
        /// </summary>
        public ProductBuilderCollection ProductBuilders
        {
            get { return _productBuilders; }
        }

        /// <summary>
        /// Specifies a URL for the Web site containing support information for the bootstrapper
        /// </summary>
        public string SupportUrl
        {
            get { return _supportUrl; }
            set { _supportUrl = value; }
        }

        /// <summary>
        /// True if the bootstrapper will perform XML validation on the component manifests
        /// </summary>
        public bool Validate
        {
            get { return _fValidate; }
            set { _fValidate = value; }
        }

        internal string Culture
        {
            get { return _culture; }
            set { _culture = value; }
        }

        internal string FallbackCulture
        {
            get { return _fallbackCulture; }
            set { _fallbackCulture = value; }
        }
    }
}
