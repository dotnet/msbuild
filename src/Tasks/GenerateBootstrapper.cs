// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Tasks.Deployment.Bootstrapper;
#endif

using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Tasks
{
#if NETFRAMEWORK

    /// <summary>
    /// Generates a bootstrapper for ClickOnce deployment projects.
    /// </summary>
    public sealed class GenerateBootstrapper : TaskExtension, IGenerateBootstrapperTaskContract
    {
        public string ApplicationName { get; set; }

        public string ApplicationFile { get; set; }

        public bool ApplicationRequiresElevation { get; set; }

        public string ApplicationUrl { get; set; }

        public ITaskItem[] BootstrapperItems { get; set; }

        public string ComponentsLocation { get; set; }

        public string ComponentsUrl { get; set; }

        public bool CopyComponents { get; set; } = true;

        public string Culture { get; set; } = Util.DefaultCultureInfo.Name;

        public string FallbackCulture { get; set; } = Util.DefaultCultureInfo.Name;

        public string OutputPath { get; set; } = Directory.GetCurrentDirectory();

        public string Path { get; set; }

        public string SupportUrl { get; set; }

        public string VisualStudioVersion { get; set; }

        public bool Validate { get; set; } = true;

        [Output]
        public string BootstrapperKeyFile { get; set; }

        [Output]
        public string[] BootstrapperComponentFiles { get; set; }

        /// <summary>
        /// Generate the bootstrapper.
        /// </summary>
        /// <returns> Return true on success, false on failure.</returns>
        public override bool Execute()
        {
            if (Path == null)
            {
                Path = Util.GetDefaultPath(VisualStudioVersion);
            }

            var bootstrapperBuilder = new BootstrapperBuilder
            {
                Validate = Validate,
                Path = Path
            };

            ProductCollection products = bootstrapperBuilder.Products;

            var settings = new BuildSettings
            {
                ApplicationFile = ApplicationFile,
                ApplicationName = ApplicationName,
                ApplicationRequiresElevation = ApplicationRequiresElevation,
                ApplicationUrl = ApplicationUrl,
                ComponentsLocation = ConvertStringToComponentsLocation(ComponentsLocation),
                ComponentsUrl = ComponentsUrl,
                CopyComponents = CopyComponents,
                Culture = Culture,
                FallbackCulture = FallbackCulture,
                OutputPath = OutputPath,
                SupportUrl = SupportUrl
            };

            if (String.IsNullOrEmpty(settings.Culture) || settings.Culture == "*")
            {
                settings.Culture = settings.FallbackCulture;
            }

            if (BootstrapperItems != null)
            {
                // The bootstrapper items may not be in the correct order, because XMake saves
                // items in alphabetical order.  So we will attempt to put items into the correct
                // order, according to the Products order in the search.  To do this, we add all
                // the items we are told to build into a hashtable, then go through our products
                // in order, looking to see if the item is built.  If it is, remove the item from
                // the hashtable.  All remaining items in the table can not be built, so errors
                // will be issued.
                var items = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);

                foreach (ITaskItem bootstrapperItem in BootstrapperItems)
                {
                    string installAttribute = bootstrapperItem.GetMetadata("Install");
                    if (String.IsNullOrEmpty(installAttribute) || Shared.ConversionUtilities.ConvertStringToBool(installAttribute))
                    {
                        if (!items.ContainsKey(bootstrapperItem.ItemSpec))
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
                    if (items.Remove(product.ProductCode))
                    {
                        settings.ProductBuilders.Add(product.ProductBuilder);
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
                    {
                        Log.LogError(null, message.HelpCode, message.HelpKeyword, null, 0, 0, 0, 0, message.Message);
                    }
                    else if (message.Severity == BuildMessageSeverity.Warning)
                    {
                        Log.LogWarning(null, message.HelpCode, message.HelpKeyword, null, 0, 0, 0, 0, message.Message);
                    }
                }
            }

            BootstrapperKeyFile = results.KeyFile;
            BootstrapperComponentFiles = results.ComponentFiles;

            return results.Succeeded;
        }

        private ComponentsLocation ConvertStringToComponentsLocation(string parameterValue)
        {
            if (string.IsNullOrEmpty(parameterValue))
            {
                return Deployment.Bootstrapper.ComponentsLocation.HomeSite;
            }

            try
            {
                return (ComponentsLocation)Enum.Parse(typeof(ComponentsLocation), parameterValue, false);
            }
            catch (FormatException)
            {
                Log.LogWarningWithCodeFromResources("GenerateBootstrapper.InvalidComponentsLocation", parameterValue);
                return Deployment.Bootstrapper.ComponentsLocation.HomeSite;
            }
        }
    }

#else

    public sealed class GenerateBootstrapper : TaskRequiresFramework, IGenerateBootstrapperTaskContract
    {
        public GenerateBootstrapper()
            : base(nameof(GenerateBootstrapper))
        {
        }

        #region Properties

        public string ApplicationName { get; set; }

        public string ApplicationFile { get; set; }

        public bool ApplicationRequiresElevation { get; set; }

        public string ApplicationUrl { get; set; }

        public ITaskItem[] BootstrapperItems { get; set; }

        public string ComponentsLocation { get; set; }

        public string ComponentsUrl { get; set; }

        public bool CopyComponents { get; set; }

        public string Culture { get; set; }

        public string FallbackCulture { get; set; }

        public string OutputPath { get; set; }

        public string Path { get; set; }

        public string SupportUrl { get; set; }

        public string VisualStudioVersion { get; set; }

        public bool Validate { get; set; }

        [Output]
        public string BootstrapperKeyFile { get; set; }

        [Output]
        public string[] BootstrapperComponentFiles { get; set; }

        #endregion
    }

#endif

    internal interface IGenerateBootstrapperTaskContract
    {
        #region Properties

        string ApplicationName { get; set; }
        string ApplicationFile { get; set; }
        bool ApplicationRequiresElevation { get; set; }
        string ApplicationUrl { get; set; }
        ITaskItem[] BootstrapperItems { get; set; }
        string ComponentsLocation { get; set; }
        string ComponentsUrl { get; set; }
        bool CopyComponents { get; set; }
        string Culture { get; set; }
        string FallbackCulture { get; set; }
        string OutputPath { get; set; }
        string Path { get; set; }
        string SupportUrl { get; set; }
        string VisualStudioVersion { get; set; }
        bool Validate { get; set; }
        string BootstrapperKeyFile { get; set; }
        string[] BootstrapperComponentFiles { get; set; }

        #endregion
    }
}
