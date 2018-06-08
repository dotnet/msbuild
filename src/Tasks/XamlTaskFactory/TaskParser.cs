// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>
// Helper class which converts Xaml rules into data structures 
// suitable for command-line processing
// </summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xaml;
using System.IO;
using Microsoft.Build.Shared;

using XamlTypes = Microsoft.Build.Framework.XamlTypes;

namespace Microsoft.Build.Tasks.Xaml
{
    /// <summary>
    /// The TaskParser class takes an xml file and parses the parameters for a task.
    /// </summary>
    internal class TaskParser
    {
        /// <summary>
        /// The set of switches added so far.
        /// </summary>
        private readonly HashSet<string> _switchesAdded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The ordered list of how the switches get emitted.
        /// </summary>
        private readonly List<string> _switchOrderList = new List<string>();

        #region Properties

        /// <summary>
        /// The name of the task
        /// </summary>
        public string GeneratedTaskName { get; set; }

        /// <summary>
        /// The base type of the class
        /// </summary>
        public string BaseClass { get; } = "DataDrivenToolTask";

        /// <summary>
        /// The namespace of the class
        /// </summary>
        public string Namespace { get; } = "XamlTaskNamespace";

        /// <summary>
        /// Namespace for the resources
        /// </summary>
        public string ResourceNamespace { get; }

        /// <summary>
        /// The name of the executable
        /// </summary>
        public string ToolName { get; private set; }

        /// <summary>
        /// The default prefix for each switch
        /// </summary>
        public string DefaultPrefix { get; private set; } = String.Empty;

        /// <summary>
        /// All of the parameters that were parsed
        /// </summary>
        public LinkedList<Property> Properties { get; } = new LinkedList<Property>();

        /// <summary>
        /// All of the parameters that have a default value
        /// </summary>
        public LinkedList<Property> DefaultSet { get; } = new LinkedList<Property>();

        /// <summary>
        /// All of the properties that serve as fallbacks for unset properties
        /// </summary>
        public Dictionary<string, string> FallbackSet { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The ordered list of properties
        /// </summary>
        public IEnumerable<string> SwitchOrderList => _switchOrderList;

        /// <summary>
        /// Returns the log of errors
        /// </summary>
        public LinkedList<string> ErrorLog { get; } = new LinkedList<string>();

        #endregion

        /// <summary>
        /// Parse the specified string, either as a file path or actual XML content.
        /// </summary>
        public bool Parse(string contentOrFile, string desiredRule)
        {
            ErrorUtilities.VerifyThrowArgumentLength(contentOrFile, nameof(contentOrFile));
            ErrorUtilities.VerifyThrowArgumentLength(desiredRule, nameof(desiredRule));

            bool parseSuccessful = ParseAsContentOrFile(contentOrFile, desiredRule);
            if (!parseSuccessful)
            {
                var parseErrors = new StringBuilder();
                parseErrors.AppendLine();
                foreach (string error in ErrorLog)
                {
                    parseErrors.AppendLine(error);
                }

                throw new ArgumentException(ResourceUtilities.FormatResourceString("Xaml.RuleParseFailed", parseErrors.ToString()));
            }

            return parseSuccessful;
        }

        private bool ParseAsContentOrFile(string contentOrFile, string desiredRule)
        {
            // On Windows:
            // - xml string will be an invalid file path, so, Path.GetFullPath will
            //   return null
            // - xml string cannot be a rooted path ("C:\\<abc />")
            //
            // On Unix:
            // - xml string is a valid path, this is not a definite check as Path.GetFullPath
            //   will return !null in most cases
            // - xml string cannot be a rooted path ("/foo/<abc />")

            bool isRootedPath = false;
            string maybeFullPath = null;
            try
            {
                isRootedPath = Path.IsPathRooted(contentOrFile);
                if (!isRootedPath)
                    maybeFullPath = Path.GetFullPath(contentOrFile);
            }
            catch (Exception e)
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                // We will get an exception if the contents are not a path (for instance, they are actual XML.)
            }

            if (isRootedPath)
            {
                // valid *absolute* file path

                if (!File.Exists(contentOrFile))
                    throw new ArgumentException(ResourceUtilities.FormatResourceString("Xaml.RuleFileNotFound", contentOrFile));

                return ParseXamlDocument(new StreamReader(contentOrFile), desiredRule);
            }

            // On Windows, xml content string is not a valid path, so, maybeFullPath == null
            // On Unix, xml content string would be a valid path, so, maybeFullPath != null
            if (maybeFullPath == null)
                // Unable to convert to a path, parse as XML
                return ParseXamlDocument(new StringReader(contentOrFile), desiredRule);

            if (File.Exists(maybeFullPath))
                // file found, parse as a file
                return ParseXamlDocument(new StreamReader(maybeFullPath), desiredRule);

            // @maybeFullPath is either:
            //  - a non-existent fullpath
            //  - or xml content with the current dir prepended (like "/foo/bar/<abc .. />"),
            //    but not on Windows
            //
            // On Windows, this means that @contentOrFile is really a non-existant file name
            if (NativeMethodsShared.IsWindows)
                throw new ArgumentException(ResourceUtilities.FormatResourceString("Xaml.RuleFileNotFound", maybeFullPath));
            else // On !Windows, try parsing as XML
                return ParseXamlDocument(new StringReader(contentOrFile), desiredRule);
        }

        /// <summary>
        /// Parse a Xaml document from a TextReader
        /// </summary>
        internal bool ParseXamlDocument(TextReader reader, string desiredRule)
        {
            ErrorUtilities.VerifyThrowArgumentNull(reader, nameof(reader));
            ErrorUtilities.VerifyThrowArgumentLength(desiredRule, nameof(desiredRule));

            object rootObject = XamlServices.Load(reader);
            if (null != rootObject)
            {
                XamlTypes.ProjectSchemaDefinitions schemas = rootObject as XamlTypes.ProjectSchemaDefinitions;
                if (schemas != null)
                {
                    foreach (XamlTypes.IProjectSchemaNode node in schemas.Nodes)
                    {
                        XamlTypes.Rule rule = node as XamlTypes.Rule;
                        if (rule != null)
                        {
                            if (String.Equals(rule.Name, desiredRule, StringComparison.OrdinalIgnoreCase))
                            {
                                return ParseXamlDocument(rule);
                            }
                        }
                    }

                    throw new XamlParseException(ResourceUtilities.FormatResourceString("Xaml.RuleNotFound", desiredRule));
                }
                else
                {
                    throw new XamlParseException(ResourceUtilities.GetResourceString("Xaml.InvalidRootObject"));
                }
            }

            return false;
        }

        /// <summary>
        /// Parse a Xaml document from a rule
        /// </summary>
        internal bool ParseXamlDocument(XamlTypes.Rule rule)
        {
            if (rule == null)
            {
                return false;
            }

            DefaultPrefix = rule.SwitchPrefix;

            ToolName = rule.ToolName;
            GeneratedTaskName = rule.Name;

            // Dictionary of property name strings to property objects. If a property is in the argument list of the current property then we want to make sure
            // that the argument property is a dependency of the current property.

            // As properties are parsed they are added to this dictionary so that after we can find the property instances from the names quickly.
            var argumentDependencyLookup = new Dictionary<string, Property>(StringComparer.OrdinalIgnoreCase);

            // baseClass = attribute.InnerText;
            // namespaceValue = attribute.InnerText;
            // resourceNamespaceValue = attribute.InnerText;
            foreach (XamlTypes.BaseProperty property in rule.Properties)
            {
                if (!ParseParameterGroupOrParameter(property, Properties, null, argumentDependencyLookup /*Add to the dictionary properties as they are parsed*/))
                {
                    return false;
                }
            }

            // Go through each property and their arguments to set up the correct dependency mappings.
            foreach (Property property in Properties)
            {
                // Get the arguments on the property itself
                List<Argument> arguments = property.Arguments;

                // Find all of the properties in arguments list.
                foreach (Argument argument in arguments)
                {
                    if (argumentDependencyLookup.TryGetValue(argument.Parameter, out Property argumentProperty))
                    {
                        property.DependentArgumentProperties.AddLast(argumentProperty);
                    }
                }

                // Properties may be enumeration types, this would mean they have sub property values which themselves can have arguments.
                List<Value> values = property.Values;

                // Find all of the properties for the aruments in sub property.
                foreach (Value value in values)
                {
                    List<Argument> valueArguments = value.Arguments;
                    foreach (Argument argument in valueArguments)
                    {
                        if (argumentDependencyLookup.TryGetValue(argument.Parameter, out Property argumentProperty))
                        {
                            // If the property contains a value sub property that has a argument then we will declare that the original property has the same dependenecy.
                            property.DependentArgumentProperties.AddLast(argumentProperty);
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Reads in the nodes of the xml file one by one and builds the data structure of all existing properties
        /// </summary>
        private bool ParseParameterGroupOrParameter(XamlTypes.BaseProperty baseProperty, LinkedList<Property> propertyList, Property property, Dictionary<string, Property> argumentDependencyLookup)
        {
            // node is a property
            if (!ParseParameter(baseProperty, propertyList, property, argumentDependencyLookup))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Fills in the property data structure
        /// </summary>
        private bool ParseParameter(XamlTypes.BaseProperty baseProperty, LinkedList<Property> propertyList, Property property, Dictionary<string, Property> argumentDependencyLookup)
        {
            Property propertyToAdd = ObtainAttributes(baseProperty, property);

            if (String.IsNullOrEmpty(propertyToAdd.Name))
            {
                propertyToAdd.Name = "AlwaysAppend";
            }

            // generate the list of parameters in order
            if (!_switchesAdded.Contains(propertyToAdd.Name))
            {
                _switchOrderList.Add(propertyToAdd.Name);
            }

            // Inherit the Prefix from the Tool
            if (String.IsNullOrEmpty(propertyToAdd.Prefix))
            {
                propertyToAdd.Prefix = DefaultPrefix;
            }

            // If the property is an enum type, parse that.
            XamlTypes.EnumProperty enumProperty = baseProperty as XamlTypes.EnumProperty;
            if (enumProperty != null)
            {
                foreach (XamlTypes.EnumValue enumValue in enumProperty.AdmissibleValues)
                {
                    var value = new Value
                    {
                        Name = enumValue.Name,
                        SwitchName = enumValue.Switch
                    };

                    if (value.SwitchName == null)
                    {
                        value.SwitchName = String.Empty;
                    }

                    value.DisplayName = enumValue.DisplayName;
                    value.Description = enumValue.Description;
                    value.Prefix = enumValue.SwitchPrefix;
                    if (String.IsNullOrEmpty(value.Prefix))
                    {
                        value.Prefix = enumProperty.SwitchPrefix;
                    }

                    if (String.IsNullOrEmpty(value.Prefix))
                    {
                        value.Prefix = DefaultPrefix;
                    }

                    if (enumValue.Arguments.Count > 0)
                    {
                        value.Arguments = new List<Argument>();
                        foreach (XamlTypes.Argument argument in enumValue.Arguments)
                        {
                            var arg = new Argument
                            {
                                Parameter = argument.Property,
                                Separator = argument.Separator,
                                Required = argument.IsRequired
                            };
                            value.Arguments.Add(arg);
                        }
                    }

                    if (value.Prefix == null)
                    {
                        value.Prefix = propertyToAdd.Prefix;
                    }

                    propertyToAdd.Values.Add(value);
                }
            }

            // build the dependencies and the values for a parameter
            foreach (XamlTypes.Argument argument in baseProperty.Arguments)
            {
                // To refactor into a separate func
                if (propertyToAdd.Arguments == null)
                {
                    propertyToAdd.Arguments = new List<Argument>();
                }

                var arg = new Argument
                {
                    Parameter = argument.Property,
                    Separator = argument.Separator,
                    Required = argument.IsRequired
                };
                propertyToAdd.Arguments.Add(arg);
            }

            if (argumentDependencyLookup != null && !argumentDependencyLookup.ContainsKey(propertyToAdd.Name))
            {
                argumentDependencyLookup.Add(propertyToAdd.Name, propertyToAdd);
            }

            // We've read any enumerated values and any dependencies, so we just 
            // have to add the property
            propertyList.AddLast(propertyToAdd);
            return true;
        }

        /// <summary>
        /// Gets all the attributes assigned in the xml file for this parameter or all of the nested switches for 
        /// this parameter group
        /// </summary>
        private static Property ObtainAttributes(XamlTypes.BaseProperty baseProperty, Property parameterGroup)
        {
            Property parameter;
            if (parameterGroup != null)
            {
                parameter = parameterGroup.Clone();
            }
            else
            {
                parameter = new Property();
            }

            XamlTypes.BoolProperty boolProperty = baseProperty as XamlTypes.BoolProperty;
            XamlTypes.DynamicEnumProperty dynamicEnumProperty = baseProperty as XamlTypes.DynamicEnumProperty;
            XamlTypes.EnumProperty enumProperty = baseProperty as XamlTypes.EnumProperty;
            XamlTypes.IntProperty intProperty = baseProperty as XamlTypes.IntProperty;
            XamlTypes.StringProperty stringProperty = baseProperty as XamlTypes.StringProperty;
            XamlTypes.StringListProperty stringListProperty = baseProperty as XamlTypes.StringListProperty;

            parameter.IncludeInCommandLine = baseProperty.IncludeInCommandLine;

            if (baseProperty.Name != null)
            {
                parameter.Name = baseProperty.Name;
            }

            if (boolProperty != null && !String.IsNullOrEmpty(boolProperty.ReverseSwitch))
            {
                parameter.Reversible = "true";
            }

            // Determine the type for this property.
            if (boolProperty != null)
            {
                parameter.Type = PropertyType.Boolean;
            }
            else if (enumProperty != null)
            {
                parameter.Type = PropertyType.String;
            }
            else if (dynamicEnumProperty != null)
            {
                parameter.Type = PropertyType.String;
            }
            else if (intProperty != null)
            {
                parameter.Type = PropertyType.Integer;
            }
            else if (stringProperty != null)
            {
                parameter.Type = PropertyType.String;
            }
            else if (stringListProperty != null)
            {
                parameter.Type = PropertyType.StringArray;
            }

            // We might need to override this type based on the data source, if it specifies a source type of 'Item'.
            if (baseProperty.DataSource != null)
            {
                if (!String.IsNullOrEmpty(baseProperty.DataSource.SourceType))
                {
                    if (baseProperty.DataSource.SourceType.Equals("Item", StringComparison.OrdinalIgnoreCase))
                    {
                        parameter.Type = PropertyType.ItemArray;
                    }
                }
            }

            if (intProperty != null)
            {
                parameter.Max = intProperty.MaxValue != null ? intProperty.MaxValue.ToString() : null;
                parameter.Min = intProperty.MinValue != null ? intProperty.MinValue.ToString() : null;
            }

            if (boolProperty != null)
            {
                parameter.ReverseSwitchName = boolProperty.ReverseSwitch;
            }

            if (baseProperty.Switch != null)
            {
                parameter.SwitchName = baseProperty.Switch;
            }

            if (stringListProperty != null)
            {
                parameter.Separator = stringListProperty.Separator;
            }

            if (baseProperty.Default != null)
            {
                parameter.DefaultValue = baseProperty.Default;
            }

            parameter.Required = baseProperty.IsRequired.ToString().ToLower(CultureInfo.InvariantCulture);

            if (baseProperty.Category != null)
            {
                parameter.Category = baseProperty.Category;
            }

            if (baseProperty.DisplayName != null)
            {
                parameter.DisplayName = baseProperty.DisplayName;
            }

            if (baseProperty.Description != null)
            {
                parameter.Description = baseProperty.Description;
            }

            if (baseProperty.SwitchPrefix != null)
            {
                parameter.Prefix = baseProperty.SwitchPrefix;
            }

            return parameter;
        }
    }
}
