// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using error = Microsoft.Build.BuildEngine.Shared.ErrorUtilities;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Reads registry at the base key and returns a Dictionary keyed on ToolsVersion.
    /// Dictionary contains another dictionary of (property name, property value) pairs.
    /// If a registry value is not a string, this will throw a InvalidToolsetDefinitionException.
    /// An example of how the registry will look (note that the DefaultToolsVersion is per-MSBuild-version)
    /// [HKLM]\SOFTWARE\Microsoft
    ///   msbuild
    ///     3.5
    ///       @DefaultToolsVersion = 2.0
    ///     ToolsVersions
    ///       2.0
    ///         @MSBuildToolsPath = D:\SomeFolder
    ///       3.5
    ///         @MSBuildToolsPath = D:\SomeOtherFolder
    ///         @MSBuildBinPath = D:\SomeOtherFolder
    ///         @SomePropertyName = PropertyOtherValue
    /// </summary>
    internal class ToolsetRegistryReader : ToolsetReader
    {
        // Registry location for storing tools version dependent data for msbuild
        private const string msbuildRegistryPath = @"SOFTWARE\Microsoft\MSBuild";
        
        // Cached registry wrapper at root of the msbuild entries
        private RegistryKeyWrapper msbuildRegistryWrapper;

        /// <summary>
        /// Default constructor
        /// </summary>
        internal ToolsetRegistryReader()
            : this (new RegistryKeyWrapper(msbuildRegistryPath))
        {
        }

        /// <summary>
        /// Constructor overload accepting a registry wrapper for unit testing purposes only
        /// </summary>
        /// <param name="msbuildRegistryWrapper"></param>
        internal ToolsetRegistryReader(RegistryKeyWrapper msbuildRegistryWrapper)
        {
            error.VerifyThrowArgumentNull(msbuildRegistryWrapper, nameof(msbuildRegistryWrapper));
       
            this.msbuildRegistryWrapper = msbuildRegistryWrapper;
        }

        /// <summary>
        /// Returns the list of tools versions
        /// </summary>
        protected override IEnumerable<PropertyDefinition> ToolsVersions
        {
            get
            {
                string[] toolsVersionNames = new string[] { };
                try
                {
                    toolsVersionNames = msbuildRegistryWrapper.OpenSubKey("ToolsVersions").GetSubKeyNames();
                }
                catch (RegistryException ex)
                {
                    InvalidToolsetDefinitionException.Throw(ex, "RegistryReadError", ex.Source, ex.Message);
                }

                foreach (string toolsVersionName in toolsVersionNames)
                {
                    yield return new PropertyDefinition(toolsVersionName, string.Empty, msbuildRegistryWrapper.Name + "\\ToolsVersions\\" + toolsVersionName);
                }
            }
        }

        /// <summary>
        /// Returns the default tools version, or null if none was specified
        /// </summary>
        protected override string DefaultToolsVersion
        {
            get
            {
                // We expect to find the DefaultToolsVersion value under a registry key named for our
                // version, e.g., "3.5"
                RegistryKeyWrapper defaultToolsVersionKey =
                    msbuildRegistryWrapper.OpenSubKey(Constants.AssemblyVersion);

                if (defaultToolsVersionKey != null)
                {
                    return GetValue(defaultToolsVersionKey, "DefaultToolsVersion");
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Provides an enumerator over property definitions for a specified tools version
        /// </summary>
        /// <param name="toolsVersion"></param>
        /// <returns></returns>
        protected override IEnumerable<PropertyDefinition> GetPropertyDefinitions(string toolsVersion)
        {
            RegistryKeyWrapper toolsVersionWrapper = null;
            
            try
            {
                toolsVersionWrapper = msbuildRegistryWrapper.OpenSubKey("ToolsVersions\\" + toolsVersion);
            }
            catch (RegistryException ex)
            {
                InvalidToolsetDefinitionException.Throw(ex, "RegistryReadError", ex.Source, ex.Message);
            }

            foreach (string propertyName in toolsVersionWrapper.GetValueNames())
            {
                string propertyValue = null;

                if (propertyName?.Length == 0)
                {
                    InvalidToolsetDefinitionException.Throw("PropertyNameInRegistryHasZeroLength", toolsVersionWrapper.Name);
                }
                
                try
                {
                    propertyValue = GetValue(toolsVersionWrapper, propertyName);
                }
                catch (RegistryException ex)
                {
                    InvalidToolsetDefinitionException.Throw(ex, "RegistryReadError", ex.Source, ex.Message);
                }

                yield return new PropertyDefinition(propertyName, propertyValue, toolsVersionWrapper.Name + "@" + propertyName);
            }
        }
        
        /// <summary>
        /// Reads a string value from the specified registry key
        /// </summary>
        /// <param name="baseKeyWrapper">wrapper around key</param>
        /// <param name="valueName">name of the value</param>
        /// <returns>string data in the value</returns>
        private static string GetValue(RegistryKeyWrapper wrapper, string valueName)
        {
            if (wrapper.Exists())
            {
                object result = wrapper.GetValue(valueName);

                // RegistryKey.GetValue returns null if the value is not present
                // and String.Empty if the value is present and no data is defined. 
                // We preserve this distinction, because a string property in the registry with
                // no value really has an empty string for a value (which is a valid property value)
                // rather than null for a value (which is an invalid property value)
                if (result != null)
                {
                    // Must be a value of string type
                    if (!(result is string))
                    {
                        InvalidToolsetDefinitionException.Throw("NonStringDataInRegistry", wrapper.Name + "@" + valueName);
                    }

                    return result.ToString();
                }
            }

            return null;
        }
    }
}
