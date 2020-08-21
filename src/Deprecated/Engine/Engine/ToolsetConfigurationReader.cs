// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;
using error = Microsoft.Build.BuildEngine.Shared.ErrorUtilities;
using System.Globalization;
using System.Reflection;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    internal class ToolsetConfigurationReader : ToolsetReader
    {
        private ToolsetConfigurationSection configurationSection = null;
        private ReadApplicationConfiguration readApplicationConfiguration = null;
        private bool configurationReadAttempted = false;

        /// <summary>
        /// Default constructor
        /// </summary>
        internal ToolsetConfigurationReader()
            : this(new ReadApplicationConfiguration(ToolsetConfigurationReader.ReadApplicationConfiguration))
        {
        }

        /// <summary>
        /// Constructor taking a delegate for unit test purposes only
        /// </summary>
        /// <param name="readApplicationConfiguration"></param>
        internal ToolsetConfigurationReader(ReadApplicationConfiguration readApplicationConfiguration)
        {
            error.VerifyThrowArgumentNull(readApplicationConfiguration, nameof(readApplicationConfiguration));
            this.readApplicationConfiguration = readApplicationConfiguration;
        }

        /// <summary>
        /// Returns the list of tools versions
        /// </summary>
        protected override IEnumerable<PropertyDefinition> ToolsVersions
        {
            get
            {
                if (ConfigurationSection != null)
                {
                    foreach (ToolsetElement toolset in ConfigurationSection.Toolsets)
                    {
                        string location = ResourceUtilities.FormatResourceString
                                          (
                                              "ConfigFileLocation",
                                              toolset.ElementInformation.Source,
                                              toolset.ElementInformation.LineNumber
                                          );

                        if (toolset.toolsVersion?.Length == 0)
                        {
                            InvalidToolsetDefinitionException.Throw("InvalidToolsetValueInConfigFileValue", location);
                        }

                        yield return new PropertyDefinition(toolset.toolsVersion, string.Empty, location);
                    }
                }
                else
                {
                    yield break;
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
                return ConfigurationSection?.Default;
            }
        }

        /// <summary>
        /// Provides an enumerator over property definitions for a specified tools version
        /// </summary>
        /// <param name="toolsVersion"></param>
        /// <returns></returns>
        protected override IEnumerable<PropertyDefinition> GetPropertyDefinitions(string toolsVersion)
        {
            ToolsetElement toolsetElement = ConfigurationSection.Toolsets.GetElement(toolsVersion);

            if (toolsetElement == null)
            {
                yield break;
            }

            foreach (ToolsetElement.PropertyElement propertyElement in toolsetElement.PropertyElements)
            {
                string location = ResourceUtilities.FormatResourceString
                                  (
                                      "ConfigFileLocation",
                                      propertyElement.ElementInformation.Source,
                                      propertyElement.ElementInformation.LineNumber
                                  );

                if (propertyElement.Name?.Length == 0)
                {
                    InvalidToolsetDefinitionException.Throw("InvalidToolsetValueInConfigFileValue", location);
                }
                
                yield return new PropertyDefinition(propertyElement.Name, propertyElement.Value, location);
            }
        }
        
        /// <summary>
        /// Reads the application configuration file.
        /// NOTE: this is abstracted into a method to support unit testing GetToolsetDataFromConfiguration().
        /// Unit tests wish to avoid reading (nunit.exe) application configuration file.
        /// </summary>
        private static Configuration ReadApplicationConfiguration()
        {
            return ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        }

        /// <summary>
        /// Lazy getter for the ToolsetConfigurationSection
        /// Returns null if the section is not present
        /// </summary>
        private ToolsetConfigurationSection ConfigurationSection
        {
            get
            {
                if (configurationSection == null && !configurationReadAttempted)
                {
                    try
                    {
                        Configuration configuration = readApplicationConfiguration();

                        // This will be null if the application config file does not have the following section 
                        // definition for the msbuildToolsets section as the first child element.
                        //   <configSections>
                        //     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                        //   </configSections>";
                        // Note that the application config file may or may not contain an msbuildToolsets element.
                        // For example:
                        // If section definition is present and section is not present, this value is not null
                        // If section definition is not present and section is also not present, this value is null
                        // If the section definition is not present and section is present, then this value is null

                        if (configuration != null)
                        {
                            configurationSection = configuration.GetSection("msbuildToolsets") as ToolsetConfigurationSection;
                        }
                    }
                    // ConfigurationException is obsolete, but we catch it rather than 
                    // ConfigurationErrorsException (which is what we throw below) because it is more 
                    // general and we don't want to miss catching some other derived exception.
                    catch (ConfigurationException ex)
                    {
                        string location = ResourceUtilities.FormatResourceString
                                          (
                                             "ConfigFileLocation", 
                                             ex.Filename, 
                                             ex.Line
                                          );

                        InvalidToolsetDefinitionException.Throw(ex, "ConfigFileReadError", location, ex.BareMessage);
                    }
                    finally
                    {
                        configurationReadAttempted = true;
                    }
                }

                return configurationSection;
            }
        }
    }

    /// <summary>
    /// This class is used to programmatically read msbuildToolsets section
    /// in from the configuration file.  An example of application config file:
    /// 
    /// &lt;configuration&gt;
    ///     &lt;msbuildToolsets default="2.0"&gt;
    ///         &lt;toolset toolsVersion="2.0"&gt;
    ///             &lt;property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/&gt;
    ///             &lt;property name="SomeOtherProperty" value="SomeOtherPropertyValue"/&gt;
    ///         &lt;/toolset&gt;
    ///         &lt;toolset toolsVersion="3.5"&gt;
    ///             &lt;property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v3.5.x86ret\"/&gt;
    ///         &lt;/toolset&gt;
    ///     &lt;/msbuildToolsets&gt;
    /// &lt;/configuration&gt;
    /// 
    /// </summary>
    /// <remarks>
    /// Internal for unit testing only
    /// </remarks>
    internal sealed class ToolsetConfigurationSection : ConfigurationSection
    {
        /// <summary>
        /// toolsVersion element collection 
        /// </summary>
        [ConfigurationProperty("", IsDefaultCollection = true)]
        public ToolsetElementCollection Toolsets
        {
            get
            {
                return (ToolsetElementCollection)base[""];
            }
        }

        /// <summary>
        /// default attribute on msbuildToolsets element, specifying the default ToolsVersion
        /// </summary>
        [ConfigurationProperty("default")]
        public string Default
        {
            get
            {
                // The ConfigurationPropertyAttribute constructor accepts a named parameter "DefaultValue"
                // that doesn't seem to work if null is the desired default value.  So here we return null
                // whenever the base class gives us an empty string.
                // Note this means we can't distinguish between the attribute being present but containing
                // an empty string for its value and the attribute not being present at all.
                string defaultValue = (string)base["default"];
                return String.IsNullOrEmpty(defaultValue) ? null : defaultValue;
            }
            set
            {
                base["default"] = value;
            }
        }
    }

    /// <summary>
    /// Class representing the collection of toolset elements
    /// </summary>
    /// <remarks>
    /// Internal for unit testing only
    /// </remarks>
    internal sealed class ToolsetElementCollection : ConfigurationElementCollection
    {
        /// <summary>
        /// We use this dictionary to track whether or not we've seen a given
        /// toolset definition before, since the .NET configuration classes
        /// won't perform this check without respect for case.
        /// </summary>
        private Dictionary<string, string> previouslySeenToolsVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a new element of the collection
        /// </summary>
        /// <returns>Created element</returns>
        protected override ConfigurationElement CreateNewElement()
        {
            return new ToolsetElement();
        }

        /// <summary>
        /// overridden so we can track previously seen tools versions
        /// </summary>
        /// <param name="index"></param>
        /// <param name="element"></param>
        protected override void BaseAdd(int index, ConfigurationElement element)
        {
            UpdateToolsVersionMap(element);

            base.BaseAdd(index, element);
        }

        /// <summary>
        /// overridden so we can track previously seen tools versions
        /// </summary>
        /// <param name="element"></param>
        protected override void BaseAdd(ConfigurationElement element)
        {
            UpdateToolsVersionMap(element);

            base.BaseAdd(element);
        }

        /// <summary>
        /// Stores the name of the tools version in a case-insensitive map
        /// so we can detect if it is specified more than once but with
        /// different case
        /// </summary>
        /// <param name="element"></param>
        private void UpdateToolsVersionMap(ConfigurationElement element)
        {
            string toolsVersion = GetElementKey(element).ToString();

            if (previouslySeenToolsVersions.ContainsKey(toolsVersion))
            {
                string message = ResourceUtilities.FormatResourceString("MultipleDefinitionsForSameToolset", toolsVersion);

                throw new ConfigurationErrorsException(message, element.ElementInformation.Source, element.ElementInformation.LineNumber);
            }
            
            previouslySeenToolsVersions.Add(toolsVersion, string.Empty);
        }

        /// <summary>
        /// Returns the key value for the given element
        /// </summary>
        /// <param name="element">element whose key is returned</param>
        /// <returns>key</returns>
        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((ToolsetElement)element).toolsVersion;
        }

        /// <summary>
        /// Throw exception if an element with a duplicate key is added to the collection
        /// </summary>
        protected override bool ThrowOnDuplicate
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Type of the collection
        /// This has to be public as cannot change access modifier when overriding
        /// </summary>
        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.BasicMap;
            }
        }

        /// <summary>
        /// Name of the element
        /// </summary>
        protected override string ElementName
        {
            get
            {
                return "toolset";
            }
        }

        /// <summary>
        /// Gets an element with the specified name
        /// </summary>
        /// <param name="toolsVersion">toolsVersion of the element</param>
        /// <returns>element</returns>
        public ToolsetElement GetElement(string toolsVersion)
        {
            return (ToolsetElement)this.BaseGet(toolsVersion);
        }

        /// <summary>
        /// Gets an element based at the specified position
        /// </summary>
        /// <param name="index">position</param>
        /// <returns>element</returns>
        public ToolsetElement GetElement(int index)
        {
            return (ToolsetElement)this.BaseGet(index);
        }
    }

    /// <summary>
    /// Class representing the Toolset element
    /// </summary>
    /// <remarks>
    /// Internal for unit testing only
    /// </remarks>
    internal sealed class ToolsetElement : ConfigurationElement
    {
        /// <summary>
        /// ToolsVersion attribute of the element
        /// </summary>
        [ConfigurationProperty("toolsVersion", IsKey = true, IsRequired = true)]
        public string toolsVersion
        {
            get
            {
                return (string)base["toolsVersion"];
            }
            set
            {
                base[nameof(toolsVersion)] = value;
            }
        }

        /// <summary>
        /// Property element collection 
        /// </summary>
        [ConfigurationProperty("", IsDefaultCollection = true)]
        public PropertyElementCollection PropertyElements
        {
            get
            {
                return (PropertyElementCollection)base[""];
            }
        }

        /// <summary>
        /// Class representing collection of property elements
        /// </summary>
        internal sealed class PropertyElementCollection : ConfigurationElementCollection
        {
            /// <summary>
            /// We use this dictionary to track whether or not we've seen a given
            /// property definition before, since the .NET configuration classes
            /// won't perform this check without respect for case.
            /// </summary>
            private Dictionary<string, string> previouslySeenPropertyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            /// <summary>
            /// Creates a new element
            /// </summary>
            /// <returns>element</returns>
            protected override ConfigurationElement CreateNewElement()
            {
                return new PropertyElement();
            }

            /// <summary>
            /// overridden so we can track previously seen property names
            /// </summary>
            /// <param name="index"></param>
            /// <param name="element"></param>
            protected override void BaseAdd(int index, ConfigurationElement element)
            {
                UpdatePropertyNameMap(element);

                base.BaseAdd(index, element);
            }

            /// <summary>
            /// overridden so we can track previously seen property names
            /// </summary>
            /// <param name="element"></param>
            protected override void BaseAdd(ConfigurationElement element)
            {
                UpdatePropertyNameMap(element);

                base.BaseAdd(element);
            }

            /// <summary>
            /// Stores the name of the tools version in a case-insensitive map
            /// so we can detect if it is specified more than once but with
            /// different case
            /// </summary>
            /// <param name="element"></param>
            private void UpdatePropertyNameMap(ConfigurationElement element)
            {
                string propertyName = GetElementKey(element).ToString();

                if (previouslySeenPropertyNames.ContainsKey(propertyName))
                {
                    string message = ResourceUtilities.FormatResourceString("MultipleDefinitionsForSameProperty", propertyName);

                    throw new ConfigurationErrorsException(message, element.ElementInformation.Source, element.ElementInformation.LineNumber);
                }

                previouslySeenPropertyNames.Add(propertyName, string.Empty);
            }
            
            /// <summary>
            /// Gets the key for the element
            /// </summary>
            /// <param name="element">element</param>
            /// <returns>key</returns>
            protected override object GetElementKey(ConfigurationElement element)
            {
                return ((PropertyElement)element).Name;
            }

            /// <summary>
            /// Collection type
            /// This has to be public as cannot change access modifier when overriding  
            /// </summary>
            public override ConfigurationElementCollectionType CollectionType
            {
                get
                {
                    return ConfigurationElementCollectionType.BasicMap;
                }
            }

            /// <summary>
            /// Throw exception if an element with a duplicate is added
            /// </summary>
            protected override bool ThrowOnDuplicate
            {
                get
                {
                    return false;
                }
            }

            /// <summary>
            /// name of the element
            /// </summary>
            protected override string ElementName
            {
                get
                {
                    return "property";
                }
            }

            /// <summary>
            /// Gets an element with the specified name
            /// </summary>
            /// <param name="name">name of the element</param>
            /// <returns>element</returns>
            public PropertyElement GetElement(string name)
            {
                return (PropertyElement)this.BaseGet(name);
            }

            /// <summary>
            /// Gets an element at the specified position
            /// </summary>
            /// <param name="index">position</param>
            /// <returns>element</returns>
            public PropertyElement GetElement(int index)
            {
                return (PropertyElement)this.BaseGet(index);
            }
        }

        /// <summary>
        /// This class represents property element
        /// </summary>
        internal sealed class PropertyElement : ConfigurationElement
        {
            /// <summary>
            /// name attribute
            /// </summary>
            [ConfigurationProperty("name", IsKey = true, IsRequired = true)]
            public string Name
            {
                get
                {
                    return (string)base["name"];
                }
                set
                {
                    base["name"] = value;
                }
            }

            /// <summary>
            /// value attribute
            /// </summary>
            [ConfigurationProperty("value", IsRequired = true)]
            public string Value
            {
                get
                {
                    return (string)base["value"];
                }
                set
                {
                    base["value"] = value;
                }
            }
        }
    }

    /// <summary>
    /// Delegate for unit test purposes only
    /// </summary>
    /// <returns></returns>
    internal delegate Configuration ReadApplicationConfiguration();
}
