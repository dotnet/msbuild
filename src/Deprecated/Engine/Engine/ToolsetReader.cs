// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Build.BuildEngine.Shared;
using error = Microsoft.Build.BuildEngine.Shared.ErrorUtilities;

namespace Microsoft.Build.BuildEngine
{
    internal class PropertyDefinition
    {
        private string name = null;
        private string value = null;
        private string source = null;

        public PropertyDefinition(string name, string value, string source)
        {
            error.VerifyThrowArgumentLength(name, nameof(name));
            error.VerifyThrowArgumentLength(source, nameof(source));

            // value can be the empty string but not null
            error.VerifyThrowArgumentNull(value, nameof(value));

            this.name = name;
            this.value = value;
            this.source = source;
        }

        /// <summary>
        /// The name of the property
        /// </summary>
        public string Name
        {
            get
            {
                return name;
            }
        }

        /// <summary>
        /// The value of the property
        /// </summary>
        public string Value
        {
            get
            {
                return value;
            }
        }

        /// <summary>
        /// A description of the location where the property was defined,
        /// such as a registry key path or a path to a config file and
        /// line number.
        /// </summary>
        public string Source
        {
            get
            {
                return source;
            }
        }
    }

    internal abstract class ToolsetReader
    {
        /// <summary>
        /// Gathers toolset data from both the registry and configuration file, if any
        /// </summary>
        /// <param name="toolsets"></param>
        /// <param name="globalProperties"></param>
        /// <param name="initialProperties"></param>
        /// <returns></returns>
        internal static string ReadAllToolsets(ToolsetCollection toolsets, BuildPropertyGroup globalProperties, BuildPropertyGroup initialProperties)
        {
            return ReadAllToolsets(toolsets, null, null, globalProperties, initialProperties, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry);
        }

        /// <summary>
        /// Gathers toolset data from the registry and configuration file, if any:
        /// allows you to specify which of the registry and configuration file to
        /// read from by providing ToolsetInitialization
        /// </summary>
        /// <param name="toolsets"></param>
        /// <param name="globalProperties"></param>
        /// <param name="initialProperties"></param>
        /// <param name="locations"></param>
        /// <returns></returns>
        internal static string ReadAllToolsets(ToolsetCollection toolsets, BuildPropertyGroup globalProperties, BuildPropertyGroup initialProperties, ToolsetDefinitionLocations locations)
        {
            return ReadAllToolsets(toolsets, null, null, globalProperties, initialProperties, locations);
        }

        /// <summary>
        /// Gathers toolset data from the registry and configuration file, if any.
        /// NOTE:  this method is internal for unit testing purposes only.
        /// </summary>
        /// <param name="toolsets"></param>
        /// <param name="registryReader"></param>
        /// <param name="configurationReader"></param>
        /// <param name="globalProperties"></param>
        /// <param name="initialProperties"></param>
        /// <param name="locations"></param>
        /// <returns></returns>
        internal static string ReadAllToolsets(ToolsetCollection toolsets,
                                               ToolsetRegistryReader registryReader,
                                               ToolsetConfigurationReader configurationReader,
                                               BuildPropertyGroup globalProperties,
                                               BuildPropertyGroup initialProperties,
                                               ToolsetDefinitionLocations locations)
        {
            // The 2.0 .NET Framework installer did not write a ToolsVersion key for itself in the registry. 
            // The 3.5 installer writes one for 2.0, but 3.5 might not be installed.  
            // The 4.0 and subsequent installers can't keep writing the 2.0 one, because (a) it causes SxS issues and (b) we 
            // don't want it unless 2.0 is installed.
            // So if the 2.0 framework is actually installed, and we're reading the registry, create a toolset for it. 
            // The registry and config file can overwrite it.
            if (
                ((locations & ToolsetDefinitionLocations.Registry) != 0) &&
                !toolsets.Contains("2.0") &&
                FrameworkLocationHelper.PathToDotNetFrameworkV20 != null
              )
            {
                Toolset synthetic20Toolset = new Toolset("2.0", FrameworkLocationHelper.PathToDotNetFrameworkV20, initialProperties);
                toolsets.Add(synthetic20Toolset);
            }

            // The ordering here is important because the configuration file should have greater precedence
            // than the registry
            string defaultToolsVersionFromRegistry = null;
            if ((locations & ToolsetDefinitionLocations.Registry) == ToolsetDefinitionLocations.Registry)
            {
                ToolsetRegistryReader registryReaderToUse = registryReader ?? new ToolsetRegistryReader();
                // We do not accumulate properties when reading them from the registry, because the order
                // in which values are returned to us is essentially random: so we disallow one property
                // in the registry to refer to another also in the registry
                defaultToolsVersionFromRegistry =
                    registryReaderToUse.ReadToolsets(toolsets, globalProperties, initialProperties, false /* do not accumulate properties */);
            }

            string defaultToolsVersionFromConfiguration = null;
            if ((locations & ToolsetDefinitionLocations.ConfigurationFile) == ToolsetDefinitionLocations.ConfigurationFile)
            {
                if (configurationReader == null && ConfigurationFileMayHaveToolsets())
                {
                    // We haven't been passed in a fake configuration reader by a unit test,
                    // and it looks like we have a .config file to read, so create a real
                    // configuration reader
                    configurationReader = new ToolsetConfigurationReader();
                }

                if (configurationReader != null)
                {
                    ToolsetConfigurationReader configurationReaderToUse = configurationReader ?? new ToolsetConfigurationReader();
                    // Accumulation of properties is okay in the config file because it's deterministically ordered
                    defaultToolsVersionFromConfiguration =
                        configurationReaderToUse.ReadToolsets(toolsets, globalProperties, initialProperties, true /* accumulate properties */);
                }
            }

            // We'll use the default from the configuration file if it was specified, otherwise we'll try
            // the one from the registry.  It's possible (and valid) that neither the configuration file
            // nor the registry specify a default, in which case we'll just return null.
            string defaultToolsVersion = defaultToolsVersionFromConfiguration ?? defaultToolsVersionFromRegistry;

            // If we got a default version from the registry or config file, and it
            // actually exists, fine.
            // Otherwise we have to come up with one.
            if (defaultToolsVersion == null || !toolsets.Contains(defaultToolsVersion))
            {
                // We're going to choose a hard coded default tools version of 2.0.
                defaultToolsVersion = Constants.defaultToolsVersion;

                // But don't overwrite any existing tools path for this default we're choosing.
                if (!toolsets.Contains(Constants.defaultToolsVersion))
                {
                    // There's no tools path already for 2.0, so use the path to the v2.0 .NET Framework.
                    // If an old-fashioned caller sets BinPath property, or passed a BinPath to the constructor,
                    // that will overwrite what we're setting here.
                    ErrorUtilities.VerifyThrow(Constants.defaultToolsVersion == "2.0", "Getting 2.0 FX path so default should be 2.0");
                    string pathToFramework = FrameworkLocationHelper.PathToDotNetFrameworkV20;

                    // We could not find the default toolsversion because it was not installed on the machine. Fallback to the 
                    // one we expect to always be there when running msbuild 4.0.
                    if (pathToFramework == null)
                    {
                        pathToFramework = FrameworkLocationHelper.PathToDotNetFrameworkV40;
                        defaultToolsVersion = Constants.defaultFallbackToolsVersion;
                    }

                    // Again don't overwrite any existing tools path for this default we're choosing.
                    if (!toolsets.Contains(defaultToolsVersion))
                    {
                        Toolset defaultToolset = new Toolset(defaultToolsVersion, pathToFramework, initialProperties);
                        toolsets.Add(defaultToolset);
                    }
                }
            }

            return defaultToolsVersion;
        }

        /// <summary>
        /// Creating a ToolsetConfigurationReader, and also reading toolsets from the
        /// configuration file, are a little expensive. To try to avoid this cost if it's
        /// not necessary, we'll check if the file exists first. If it exists, we'll scan for
        /// the string "toolsVersion" to see if it might actually have any tools versions
        /// defined in it.
        /// </summary>
        /// <returns>True if there may be toolset definitions, otherwise false</returns>
        private static bool ConfigurationFileMayHaveToolsets()
        {
            bool result;

            try
            {
                result = (File.Exists(FileUtilities.CurrentExecutableConfigurationFilePath)
                          && File.ReadAllText(FileUtilities.CurrentExecutableConfigurationFilePath).Contains("toolsVersion"));
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's an IO related exception.
            {
                if (ExceptionHandling.NotExpectedException(e))
                    throw;

                // There was some problem reading the config file: let the configuration reader
                // encounter it
                result = true;
            }

            return result;
        }

        /// <summary>
        /// Populates the toolset collection passed in with the toolsets read from some location.
        /// </summary>
        /// <remarks>Internal for unit testing only</remarks>
        /// <param name="toolsets"></param>
        /// <param name="globalProperties"></param>
        /// <param name="initialProperties"></param>
        /// <param name="accumulateProperties"></param>
        /// <returns>the default tools version if available, or null otherwise</returns>
        internal string ReadToolsets(ToolsetCollection toolsets,
                                     BuildPropertyGroup globalProperties,
                                     BuildPropertyGroup initialProperties,
                                     bool accumulateProperties)
        {
            error.VerifyThrowArgumentNull(toolsets, nameof(toolsets));

            ReadEachToolset(toolsets, globalProperties, initialProperties, accumulateProperties);

            string defaultToolsVersion = DefaultToolsVersion;

            // We don't check whether the default tools version actually
            // corresponds to a toolset definition. That's because our default for
            // the indefinite future is 2.0, and 2.0 might not be installed, which is fine.
            // If a project tries to use 2.0 (or whatever the default is) in these circumstances
            // they'll get a nice error saying that toolset isn't available and listing those that are.
            return defaultToolsVersion;
        }

        /// <summary>
        /// Reads all the toolsets and populates the given ToolsetCollection with them
        /// </summary>
        /// <param name="toolsets"></param>
        /// <param name="globalProperties"></param>
        /// <param name="initialProperties"></param>
        /// <param name="accumulateProperties"></param>
        private void ReadEachToolset(ToolsetCollection toolsets,
                                     BuildPropertyGroup globalProperties,
                                     BuildPropertyGroup initialProperties,
                                     bool accumulateProperties)
        {
            foreach (PropertyDefinition toolsVersion in ToolsVersions)
            {
                // We clone here because we don't want to interfere with the evaluation 
                // of subsequent Toolsets; otherwise, properties found during the evaluation
                // of this Toolset would be persisted in initialProperties and appear
                // to later Toolsets as Global or Environment properties from the Engine.
                BuildPropertyGroup initialPropertiesClone = initialProperties.Clone(true /* deep clone */);
                Toolset toolset = ReadToolset(toolsVersion, globalProperties, initialPropertiesClone, accumulateProperties);
                if (toolset != null)
                {
                    toolsets.Add(toolset);
                }
            }
        }

        /// <summary>
        /// Reads the settings for a specified tools version
        /// </summary>
        /// <param name="toolsVersion"></param>
        /// <param name="globalProperties"></param>
        /// <param name="initialProperties"></param>
        /// <param name="accumulateProperties"></param>
        /// <returns></returns>
        private Toolset ReadToolset(PropertyDefinition toolsVersion,
                                    BuildPropertyGroup globalProperties,
                                    BuildPropertyGroup initialProperties,
                                    bool accumulateProperties)
        {
            // Initial properties is the set of properties we're going to use to expand property expressions like $(foo)
            // in the values we read out of the registry or config file. We'll add to it as we pick up properties (including binpath)
            // from the registry or config file, so that properties there can be referenced in values below them.
            // After processing all the properties, we don't need initialProperties anymore.
            string toolsPath = null;
            string binPath = null;
            BuildPropertyGroup properties = new BuildPropertyGroup();

            IEnumerable<PropertyDefinition> rawProperties = GetPropertyDefinitions(toolsVersion.Name);
            Expander expander = new Expander(initialProperties);

            foreach (PropertyDefinition property in rawProperties)
            {
                if (String.Equals(property.Name, ReservedPropertyNames.toolsPath, StringComparison.OrdinalIgnoreCase))
                {
                    toolsPath = ExpandProperty(property, expander);
                    toolsPath = ExpandRelativePathsRelativeToExeLocation(toolsPath);

                    if (accumulateProperties)
                    {
                        SetProperty
                        (
                            new PropertyDefinition(ReservedPropertyNames.toolsPath, toolsPath, property.Source),
                            initialProperties,
                            globalProperties
                        );
                    }
                }
                else if (String.Equals(property.Name, ReservedPropertyNames.binPath, StringComparison.OrdinalIgnoreCase))
                {
                    binPath = ExpandProperty(property, expander);
                    binPath = ExpandRelativePathsRelativeToExeLocation(binPath);

                    if (accumulateProperties)
                    {
                        SetProperty
                        (
                            new PropertyDefinition(ReservedPropertyNames.binPath, binPath, property.Source),
                            initialProperties,
                            globalProperties
                        );
                    }
                }
                else if(ReservedPropertyNames.IsReservedProperty(property.Name))
                {
                    // We don't allow toolsets to define reserved properties
                    string baseMessage = ResourceUtilities.FormatResourceString("CannotModifyReservedProperty", property.Name);
                    InvalidToolsetDefinitionException.Throw("InvalidPropertyNameInToolset", property.Name, property.Source, baseMessage);
                }
                else
                {
                    // It's an arbitrary property
                    string propertyValue = ExpandProperty(property, expander);
                    PropertyDefinition expandedProperty = new PropertyDefinition(property.Name, propertyValue, property.Source);

                    SetProperty(expandedProperty, properties, globalProperties);

                    if (accumulateProperties)
                    {
                        SetProperty(expandedProperty, initialProperties, globalProperties);
                    }
                }

                if (accumulateProperties)
                {
                    expander = new Expander(initialProperties);
                }
            }

            // All tools versions must specify a value for MSBuildToolsPath (or MSBuildBinPath)
            if (String.IsNullOrEmpty(toolsPath) && String.IsNullOrEmpty(binPath))
            {
                InvalidToolsetDefinitionException.Throw("MSBuildToolsPathIsNotSpecified", toolsVersion.Name, toolsVersion.Source);
            }

            // If both MSBuildBinPath and MSBuildToolsPath are present, they must be the same
            if (toolsPath != null && binPath != null && !toolsPath.Equals(binPath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            Toolset toolset = null;

            try
            {
                toolset = new Toolset(toolsVersion.Name, toolsPath ?? binPath, properties);
            }
            catch (ArgumentException e)
            {
                InvalidToolsetDefinitionException.Throw("ErrorCreatingToolset", toolsVersion.Name, e.Message);
            }

            return toolset;
        }

        /// <summary>
        /// Expands the given unexpanded property expression using the properties in the
        /// given BuildPropertyGroup.
        /// </summary>
        /// <param name="unexpandedProperty"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        private string ExpandProperty(PropertyDefinition property, Expander expander)
        {
            try
            {
                return expander.ExpandAllIntoStringLeaveEscaped(property.Value, null);
            }
            catch (InvalidProjectFileException ex)
            {
                InvalidToolsetDefinitionException.Throw(ex, "ErrorEvaluatingToolsetPropertyExpression", property.Value, property.Source, ex.Message);
            }

            return string.Empty;
        }

        /// <summary>
        /// Sets the given property in the given property group.
        /// </summary>
        /// <param name="property"></param>
        /// <param name="propertyGroup"></param>
        /// <param name="globalProperties"></param>
        private void SetProperty(PropertyDefinition property, BuildPropertyGroup propertyGroup, BuildPropertyGroup globalProperties)
        {
            try
            {
                // Global properties cannot be overwritten
                if (globalProperties[property.Name] == null)
                {
                    propertyGroup.SetProperty(property.Name, property.Value);
                }
            }
            catch (ArgumentException ex)
            {
                InvalidToolsetDefinitionException.Throw(ex, "InvalidPropertyNameInToolset", property.Name, property.Source, ex.Message);
            }
        }

        /// <summary>
        /// Given a path, de-relativizes it using the location of the currently
        /// executing .exe as the base directory. For example, the path "..\foo"
        /// becomes "c:\windows\microsoft.net\framework\foo" if the current exe is
        /// "c:\windows\microsoft.net\framework\v3.5.1234\msbuild.exe".
        /// If the path is not relative, it is returned without modification.
        /// If the path is invalid, it is returned without modification.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string ExpandRelativePathsRelativeToExeLocation(string path)
        {
            try
            {
                // Trim, because we don't want to do anything with empty values
                // (those should cause an error)
                string trimmedValue = path.Trim();
                if (trimmedValue.Length > 0 && !Path.IsPathRooted(trimmedValue))
                {
                    path = Path.GetFullPath(
                        Path.Combine(FileUtilities.CurrentExecutableDirectory, trimmedValue));
                }
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's an IO related exception.
            {
                if (ExceptionHandling.NotExpectedException(e))
                    throw;
                // This means that the path looked relative, but was an invalid path. In this case, we'll
                // just not expand it, and carry on - to be consistent with what happens when there's a
                // non-relative bin path with invalid characters. The problem will be detected later when
                // it's used in a project file.
            }

            return path;
        }

        /// <summary>
        /// Returns the list of tools versions
        /// </summary>
        protected abstract IEnumerable<PropertyDefinition> ToolsVersions { get; }

        /// <summary>
        /// Returns the default tools version, or null if none was specified
        /// </summary>
        protected abstract string DefaultToolsVersion { get; }

        /// <summary>
        /// Provides an enumerator over property definitions for a specified tools version
        /// </summary>
        /// <param name="toolsVersion"></param>
        /// <returns></returns>
        protected abstract IEnumerable<PropertyDefinition> GetPropertyDefinitions(string toolsVersion);
    }
}
