// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Resources;
using System.Reflection;
using System.Diagnostics;
using System.Collections;
using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.Deployment.Bootstrapper;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Generates a bootstrapper for ClickOnce deployment projects.
    /// </summary>
    public sealed class GenerateBootstrapper : TaskExtension
    {
        private string _applicationFile = null;
        private string _applicationName = null;
        private bool _applicationRequiresElevation = false;
        private string _applicationUrl = null;
        private ITaskItem[] _bootstrapperItems = null;
        private string _componentsLocation = null;
        private string _componentsUrl = null;
        private bool _copyComponents = true;
        private string _culture = Util.DefaultCultureInfo.Name;
        private string _fallbackCulture = Util.DefaultCultureInfo.Name;
        private string _outputPath = Directory.GetCurrentDirectory();
        private string _path = null;
        private string _supportUrl = null;
        private bool _validate = true;
        private string[] _bootstrapperComponentFiles = null;
        private string _bootstrapperKeyFile = null;
        private string _visualStudioVersion = null;

        public GenerateBootstrapper()
        {
        }

        public string ApplicationName
        {
            get { return _applicationName; }
            set { _applicationName = value; }
        }

        public string ApplicationFile
        {
            get { return _applicationFile; }
            set { _applicationFile = value; }
        }

        public bool ApplicationRequiresElevation
        {
            get { return _applicationRequiresElevation; }
            set { _applicationRequiresElevation = value; }
        }

        public string ApplicationUrl
        {
            get { return _applicationUrl; }
            set { _applicationUrl = value; }
        }

        public ITaskItem[] BootstrapperItems
        {
            get { return _bootstrapperItems; }
            set { _bootstrapperItems = value; }
        }

        public string ComponentsLocation
        {
            get { return _componentsLocation; }
            set { _componentsLocation = value; }
        }

        public string ComponentsUrl
        {
            get { return _componentsUrl; }
            set { _componentsUrl = value; }
        }

        public bool CopyComponents
        {
            get { return _copyComponents; }
            set
            {
                _copyComponents = value;
            }
        }

        public string Culture
        {
            get { return _culture; }
            set { _culture = value; }
        }

        public string FallbackCulture
        {
            get { return _fallbackCulture; }
            set { _fallbackCulture = value; }
        }

        public string OutputPath
        {
            get { return _outputPath; }
            set { _outputPath = value; }
        }

        public string Path
        {
            get { return _path; }
            set { _path = value; }
        }

        public string SupportUrl
        {
            get { return _supportUrl; }
            set { _supportUrl = value; }
        }

        public string VisualStudioVersion
        {
            get { return _visualStudioVersion; }
            set { _visualStudioVersion = value; }
        }

        public bool Validate
        {
            get { return _validate; }
            set { _validate = value; }
        }

        [Output]
        public string BootstrapperKeyFile
        {
            get { return _bootstrapperKeyFile; }
            set { _bootstrapperKeyFile = value; }
        }

        [Output]
        public string[] BootstrapperComponentFiles
        {
            get { return _bootstrapperComponentFiles; }
            set { _bootstrapperComponentFiles = value; }
        }

        /// <summary>
        /// Generate the bootstrapper.
        /// </summary>
        /// <returns> Return true on success, false on failure.</returns>
        public override bool Execute()
        {
            if (_path == null)
            {
                _path = Util.GetDefaultPath(_visualStudioVersion);
            }

            BootstrapperBuilder bootstrapperBuilder = new BootstrapperBuilder();

            bootstrapperBuilder.Validate = this.Validate;
            bootstrapperBuilder.Path = this.Path;

            ProductCollection products = bootstrapperBuilder.Products;

            BuildSettings settings = new BuildSettings();

            settings.ApplicationFile = ApplicationFile;
            settings.ApplicationName = ApplicationName;
            settings.ApplicationRequiresElevation = ApplicationRequiresElevation;
            settings.ApplicationUrl = ApplicationUrl;
            settings.ComponentsLocation = ConvertStringToComponentsLocation(this.ComponentsLocation);
            settings.ComponentsUrl = ComponentsUrl;
            settings.CopyComponents = CopyComponents;
            settings.Culture = _culture;
            settings.FallbackCulture = _fallbackCulture;
            settings.OutputPath = this.OutputPath;
            settings.SupportUrl = this.SupportUrl;

            if (String.IsNullOrEmpty(settings.Culture) || settings.Culture == "*")
            {
                settings.Culture = settings.FallbackCulture;
            }

            if (this.BootstrapperItems != null)
            {
                // The bootstrapper items may not be in the correct order, because XMake saves 
                // items in alphabetical order.  So we will attempt to put items into the correct 
                // order, according to the Products order in the search.  To do this, we add all 
                // the items we are told to build into a hashtable, then go through our products 
                // in order, looking to see if the item is built.  If it is, remove the item from 
                // the hashtable.  All remaining items in the table can not be built, so errors 
                // will be issued.
                Hashtable items = new Hashtable(StringComparer.OrdinalIgnoreCase);

                foreach (ITaskItem bootstrapperItem in this.BootstrapperItems)
                {
                    string installAttribute = bootstrapperItem.GetMetadata("Install");
                    if (String.IsNullOrEmpty(installAttribute) || Shared.ConversionUtilities.ConvertStringToBool(installAttribute))
                    {
                        if (!items.Contains(bootstrapperItem.ItemSpec))
                        {
                            items.Add(bootstrapperItem.ItemSpec, bootstrapperItem);
                        }
                        else
                        {
                            Log.LogWarningWithCodeFromResources("GenerateBootstrapper.DuplicateItems", bootstrapperItem.ItemSpec);
                        }
                    }
                }

                foreach (Product product in products)
                {
                    if (items.Contains(product.ProductCode))
                    {
                        settings.ProductBuilders.Add(product.ProductBuilder);
                        items.Remove(product.ProductCode);
                    }
                }

                foreach (ITaskItem bootstrapperItem in items.Values)
                {
                    Log.LogWarningWithCodeFromResources("GenerateBootstrapper.ProductNotFound", bootstrapperItem.ItemSpec, bootstrapperBuilder.Path);
                }
            }

            BuildResults results = bootstrapperBuilder.Build(settings);
            BuildMessage[] messages = results.Messages;

            if (messages != null)
            {
                foreach (BuildMessage message in messages)
                {
                    if (message.Severity == BuildMessageSeverity.Error)
                        Log.LogError(null, message.HelpCode, message.HelpKeyword, null, 0, 0, 0, 0, message.Message);
                    else if (message.Severity == BuildMessageSeverity.Warning)
                        Log.LogWarning(null, message.HelpCode, message.HelpKeyword, null, 0, 0, 0, 0, message.Message);
                }
            }

            this.BootstrapperKeyFile = results.KeyFile;
            this.BootstrapperComponentFiles = results.ComponentFiles;

            return results.Succeeded;
        }

        private ComponentsLocation ConvertStringToComponentsLocation(string parameterValue)
        {
            if (parameterValue == null || parameterValue.Length == 0)
                return Microsoft.Build.Tasks.Deployment.Bootstrapper.ComponentsLocation.HomeSite;
            try
            {
                return (Microsoft.Build.Tasks.Deployment.Bootstrapper.ComponentsLocation)Enum.Parse(typeof(Microsoft.Build.Tasks.Deployment.Bootstrapper.ComponentsLocation), parameterValue, false);
            }
            catch (FormatException)
            {
                Log.LogWarningWithCodeFromResources("GenerateBootstrapper.InvalidComponentsLocation", parameterValue);
                return Microsoft.Build.Tasks.Deployment.Bootstrapper.ComponentsLocation.HomeSite;
            }
        }
    }
}
