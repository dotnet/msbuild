// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.CodeDom;

using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.Xaml
{
    /// <summary>
    /// The TaskGenerator class creates code for the specified file
    /// </summary>
    internal class TaskGenerator
    {
        #region Private const strings

        // ---------------------------------------------
        // private static strings used in code for types
        // ---------------------------------------------

        /// <summary>
        /// The property for the tool name.
        /// </summary>
        private const string ToolNamePropertyName = "ToolName";

        /// <summary>
        /// IsOn
        /// </summary>
        private const string IsOn = "true";

        /// <summary>
        /// IsOff
        /// </summary>
        private const string IsOff = "false";

        /// <summary>
        /// The value attribute.
        /// </summary>
        private const string ValueAttribute = "value";

        // --------------------
        // ToolSwitchType types
        // --------------------

        /// <summary>
        /// The boolean type
        /// </summary>
        private const string TypeBoolean = "Boolean";

        /// <summary>
        /// The integer type
        /// </summary>
        private const string TypeInteger = "Integer";

        /// <summary>
        /// The string type
        /// </summary>
        private const string TypeString = "String";

        /// <summary>
        /// The string array type
        /// </summary>
        private const string TypeStringArray = "StringArray";

        /// <summary>
        /// The ITaskItemArray type
        /// </summary>
        private const string TypeITaskItemArray = "ITaskItemArray";

        /// <summary>
        /// The KeyValue pair type.
        /// </summary>
        private const string TypeKeyValuePairStrings = "KeyValuePair<string,string>";

        // -----------
        // Other types
        // -----------

        /// <summary>
        /// The import type.
        /// </summary>
        private const string ImportType = "import";

        /// <summary>
        /// The ToolSwitch.
        /// </summary>
        private const string TypeToolSwitch = "CommandLineToolSwitch";

        /// <summary>
        /// The ToolSwitch type.
        /// </summary>
        private const string TypeToolSwitchType = "CommandLineToolSwitchType";

        // ----------------
        // Common variables
        // ----------------

        /// <summary>
        /// The switchToAdd field.
        /// </summary>
        private const string SwitchToAdd = "switchToAdd";

        /// <summary>
        /// The ActiveToolSwitches property.
        /// </summary>
        private const string DictionaryOfSwitches = "ActiveToolSwitches";

        /// <summary>
        /// The switchMap field.
        /// </summary>
        private const string SwitchMap = "switchMap";

        /// <summary>
        /// The MultiValues property.
        /// </summary>
        private const string MultiValues = "AllowMultipleValues";

        // --------------
        // Common methods
        // --------------

        /// <summary>
        /// The AddLast method.
        /// </summary>
        private const string AddLastMethod = "AddLast";

        /// <summary>
        /// The ValidateInteger method.
        /// </summary>
        private const string ValidateIntegerMethod = "ValidateInteger";

        /// <summary>
        /// The ReadSwitchMap method.
        /// </summary>
        private const string ReadSwitchMapMethod = "ReadSwitchMap2";

        /// <summary>
        /// The IsPropertySet method.
        /// </summary>
        private const string IsPropertySetMethod = "IsPropertySet";

        /// <summary>
        /// The IsSwitchValueSet method.
        /// </summary>
        private const string IsSwitchValueSetMethod = "IsSwitchValueSet";

        /// <summary>
        /// The AddDefaultsToActiveSwitchList method.
        /// </summary>
        private const string AddDefaultsToActiveSwitchList = "AddDefaultsToActiveSwitchList";

        /// <summary>
        /// The AddFallbacksToActiveSwitchList method.
        /// </summary>
        private const string AddFallbacksToActiveSwitchList = "AddFallbacksToActiveSwitchList";

        /// <summary>
        /// The ValidateRelations method.
        /// </summary>
        private const string ValidateRelationsMethod = "ValidateRelations";

        /// <summary>
        /// The ReplaceToolSwitch method.
        /// </summary>
        private const string ReplaceToolSwitchMethod = "ReplaceToolSwitch";

        /// <summary>
        /// The Overrides method.
        /// </summary>
        private const string Overrides = "Overrides";

        // ------------------------
        // properties of ToolSwitch
        // ------------------------

        /// <summary>
        /// The Name property
        /// </summary>
        private const string NameProperty = "Name";

        /// <summary>
        /// The BooleanValue property
        /// </summary>
        private const string BooleanValueProperty = "BooleanValue";

        /// <summary>
        /// The FileName property
        /// </summary>
        private const string FileNameProperty = "Value";

        /// <summary>
        /// The TaskItemArray property
        /// </summary>
        private const string TaskItemArrayProperty = "TaskItemArray";

        /// <summary>
        /// The StringList property
        /// </summary>
        private const string StringListProperty = "StringList";

        /// <summary>
        /// The Number property
        /// </summary>
        private const string NumberProperty = "Number";

        /// <summary>
        /// The FalseSuffix property
        /// </summary>
        private const string FalseSuffixProperty = "FalseSuffix";

        /// <summary>
        /// The TrueSuffix property
        /// </summary>
        private const string TrueSuffixProperty = "TrueSuffix";

        /// <summary>
        /// The Separator property
        /// </summary>
        private const string SeparatorProperty = "Separator";

        /// <summary>
        /// The FallbackArgumentParameter property
        /// </summary>
        private const string FallbackProperty = "FallbackArgumentParameter";

        /// <summary>
        /// The Output property
        /// </summary>
        private const string OutputProperty = "Output";

        /// <summary>
        /// The ArgumentParameter property
        /// </summary>
        private const string ArgumentProperty = "ArgumentParameter";

        /// <summary>
        /// The Required property
        /// </summary>
        private const string PropertyRequiredProperty = "Required";

        /// <summary>
        /// The Parents property
        /// </summary>
        private const string ParentProperty = "Parents";

        /// <summary>
        /// The Reversible property
        /// </summary>
        private const string ReversibleProperty = "Reversible";

        /// <summary>
        /// The SwitchValue property
        /// </summary>
        private const string SwitchValueProperty = "SwitchValue";

        /// <summary>
        /// The Value property
        /// </summary>
        private const string ValueProperty = "Value";

        /// <summary>
        /// The Required property
        /// </summary>
        private const string RequiredProperty = "Required";

        /// <summary>
        /// The DisplayName property
        /// </summary>
        private const string DisplayNameProperty = "DisplayName";

        /// <summary>
        /// The Description property
        /// </summary>
        private const string DescriptionProperty = "Description";

        /// <summary>
        /// The ReverseSwitchValue property
        /// </summary>
        private const string ReverseSwitchValueProperty = "ReverseSwitchValue";

        /// <summary>
        /// The IsValid property
        /// </summary>
        private const string IsValidProperty = "IsValid";

        /// <summary>
        /// Types to ignore.
        /// </summary>
        private static readonly string[] PropertiesTypesToIgnore = { "AdditionalOptions", "CommandLineTemplate" };

        #endregion

        /// <summary>
        /// The xml parsers
        /// </summary>
        private readonly TaskParser _taskParser = new TaskParser();

        /// <summary>
        /// The relations parser
        /// </summary>
        private readonly RelationsParser _relationsParser = new RelationsParser();

        #region Constructor

        /// <summary>
        /// The default constructor
        /// </summary>
        public TaskGenerator()
        {
            // do nothing
        }

        /// <summary>
        /// When set to true, the generated code will include comments.
        /// </summary>
        public bool GenerateComments { get; set; }

        /// <summary>
        /// Constructor that takes a parser
        /// </summary>
        internal TaskGenerator(TaskParser parser)
        {
            _taskParser = parser;
        }

        #endregion

        /// <summary>
        /// The platform
        /// </summary>
        private string Platform { get; } = String.Empty;

        #region Generate code methods

        /// <summary>
        /// Removes properties that have types we are ignoring.
        /// </summary>
        internal void RemovePropertiesWithIgnoredTypes(LinkedList<Property> propertyList)
        {
            var propertyToIgnoreList = new LinkedList<Property>();
            foreach (Property property in propertyList)
            {
                foreach (string propertyToIgnore in PropertiesTypesToIgnore)
                {
                    if (String.Equals(property.Name, propertyToIgnore, StringComparison.OrdinalIgnoreCase))
                    {
                        propertyToIgnoreList.AddFirst(property);
                    }
                }
            }

            foreach (Property property in propertyToIgnoreList)
            {
                propertyList.Remove(property);
            }
        }

        /// <summary>
        /// Generates the source code for the task in the specified file
        /// </summary>
        internal CodeCompileUnit GenerateCode()
        {
            try
            {
                // set up the class namespace
                var compileUnit = new CodeCompileUnit();
                var dataDrivenToolTaskNamespace = new CodeNamespace(_taskParser.Namespace);
                var taskClass = new CodeTypeDeclaration(_taskParser.GeneratedTaskName);

                if (GenerateComments)
                {
                    // add comments to the class
                    taskClass.Comments.Add(new CodeCommentStatement(ResourceUtilities.GetResourceString("StartSummary"), true));
                    string commentContent = ResourceUtilities.FormatResourceString("ClassDescription", _taskParser.GeneratedTaskName);
                    taskClass.Comments.Add(new CodeCommentStatement(commentContent, true));
                    taskClass.Comments.Add(new CodeCommentStatement(ResourceUtilities.GetResourceString("EndSummary"), true));
                }

                // set up the class attributes
                taskClass.IsClass = true;
                taskClass.IsPartial = true;
                taskClass.BaseTypes.Add(new CodeTypeReference("XamlDataDrivenToolTask"));
                dataDrivenToolTaskNamespace.Types.Add(taskClass);
                compileUnit.Namespaces.Add(dataDrivenToolTaskNamespace);

                RemovePropertiesWithIgnoredTypes(_taskParser.Properties);

                // generate the using statements
                GenerateImports(dataDrivenToolTaskNamespace);

                // generate the constructor for this class
                GenerateConstructor(taskClass);

                // generate the property for ToolName 
                GenerateToolNameProperty(taskClass);

                // generate all of the properties
                GenerateProperties(taskClass, _taskParser.Properties);

                // generate the method to set all of the properties that have default values
                GenerateDefaultSetProperties(taskClass);

                // generate the method to set all of the fallback properties in case the main property is not set
                GenerateFallbacks(taskClass);

                GenerateRelations(taskClass);

                return compileUnit;
            }
            catch (ConfigurationException e)
            {
                LogError("InvalidLanguage", e.Message);
            }

            return null;
        }

        /// <summary>
        /// Generates a method called "AddDefaultsToActiveSwitchList" that takes all of the properties that have 
        /// default values and adds them to the active switch list
        /// </summary>
        private void GenerateDefaultSetProperties(CodeTypeDeclaration taskClass)
        {
            if (_taskParser.DefaultSet.Count > 0)
            {
                var addToActiveSwitchList = new CodeMemberMethod
                {
                    Name = AddDefaultsToActiveSwitchList,
                    Attributes = MemberAttributes.Family | MemberAttributes.Override
                };
                foreach (Property property in _taskParser.DefaultSet)
                {
                    var removeExisting = new CodeConditionStatement
                    {
                        Condition = new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(
                                new CodeMethodReferenceExpression(
                                    new CodeThisReferenceExpression(),
                                    IsPropertySetMethod),
                                new CodeSnippetExpression(SurroundWithQuotes(property.Name))),
                            CodeBinaryOperatorType.IdentityEquality,
                            new CodeSnippetExpression("false"))
                    };
                    if (property.Type == PropertyType.Boolean)
                    {
                        removeExisting.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression(property.Name), new CodeSnippetExpression(property.DefaultValue)));
                    }
                    else
                    {
                        removeExisting.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression(property.Name), new CodeSnippetExpression(SurroundWithQuotes(property.DefaultValue))));
                    }
                    addToActiveSwitchList.Statements.Add(removeExisting);
                }
                taskClass.Members.Add(addToActiveSwitchList);

                if (GenerateComments)
                {
                    // comments
                    addToActiveSwitchList.Comments.Add(new CodeCommentStatement(ResourceUtilities.GetResourceString("StartSummary"), true));
                    string commentContent = ResourceUtilities.GetResourceString("AddDefaultsToActiveSwitchListDescription");
                    addToActiveSwitchList.Comments.Add(new CodeCommentStatement(commentContent, true));
                    addToActiveSwitchList.Comments.Add(new CodeCommentStatement(ResourceUtilities.GetResourceString("EndSummary"), true));
                }
            }
        }

        /// <summary>
        /// Generates a method called "AddFallbacksToActiveSwitchList" that takes all of the properties that 
        /// are not set but have fallbacks and adds the fallbacks to the active list if they are set.
        /// </summary>
        private void GenerateFallbacks(CodeTypeDeclaration taskClass)
        {
            if (_taskParser.FallbackSet.Count > 0)
            {
                var addToActiveSwitchList = new CodeMemberMethod
                {
                    Name = AddFallbacksToActiveSwitchList,
                    Attributes = MemberAttributes.Family | MemberAttributes.Override
                };
                foreach (KeyValuePair<string, string> fallbackParameter in _taskParser.FallbackSet)
                {
                    var removeExisting = new CodeConditionStatement();
                    var isPropertySet = new CodeMethodInvokeExpression(new CodeThisReferenceExpression(), IsPropertySetMethod, new CodeSnippetExpression(SurroundWithQuotes(fallbackParameter.Value)));
                    var propertyNotSet = new CodeBinaryOperatorExpression(new CodeMethodInvokeExpression(new CodeThisReferenceExpression(), IsPropertySetMethod, new CodeSnippetExpression(SurroundWithQuotes(fallbackParameter.Key))), CodeBinaryOperatorType.ValueEquality, new CodeSnippetExpression(IsOff));
                    removeExisting.Condition = new CodeBinaryOperatorExpression(propertyNotSet, CodeBinaryOperatorType.BooleanAnd, isPropertySet);
                    removeExisting.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression(fallbackParameter.Key), new CodeVariableReferenceExpression(fallbackParameter.Value)));
                    addToActiveSwitchList.Statements.Add(removeExisting);
                }
                taskClass.Members.Add(addToActiveSwitchList);

                if (GenerateComments)
                {
                    // comments
                    addToActiveSwitchList.Comments.Add(new CodeCommentStatement(ResourceUtilities.GetResourceString("StartSummary"), true));
                    string commentContent = ResourceUtilities.GetResourceString("AddFallbacksToActiveSwitchListDescription");
                    addToActiveSwitchList.Comments.Add(new CodeCommentStatement(commentContent, true));
                    addToActiveSwitchList.Comments.Add(new CodeCommentStatement(ResourceUtilities.GetResourceString("EndSummary"), true));
                }
            }
        }

        /// <summary>
        /// Generates code for the different properties in a task
        /// </summary>
        private void GenerateProperties(CodeTypeDeclaration taskClass, LinkedList<Property> propertyList)
        {
            foreach (Property property in propertyList)
            {
                if (!String.Equals(property.Name, ImportType, StringComparison.OrdinalIgnoreCase))
                {
                    if (!ContainsCurrentPlatform(property))
                    {
                        continue;
                    }

                    var collection = new CodeAttributeDeclarationCollection();
                    var propertyName = new CodeMemberProperty
                    {
                        Name = property.Name,
                        HasGet = true,
                        HasSet = true,
                        Attributes = MemberAttributes.Public
                    };

                    // check to see if the property has a default value set
                    if (!String.IsNullOrEmpty(property.DefaultValue))
                    {
                        _taskParser.DefaultSet.AddLast(property);
                    }

                    // check to see whether it is required, whether it is an output, etc.
                    if (!String.IsNullOrEmpty(property.Required) && property.Required == IsOn)
                    {
                        collection.Add(new CodeAttributeDeclaration(PropertyRequiredProperty));
                    }
                    if (property.Output)
                    {
                        collection.Add(new CodeAttributeDeclaration(OutputProperty));
                    }
                    if (String.IsNullOrEmpty(property.Argument) && !String.IsNullOrEmpty(property.Fallback))
                    {
                        _taskParser.FallbackSet.Add(property.Name, property.Fallback);
                    }
                    if (property.Type == PropertyType.StringArray)
                    {
                        GenerateStringArrays(property, propertyName);
                    }
                    else if (property.Type == PropertyType.String)
                    {
                        GenerateStrings(property, propertyName);
                    }
                    else if (property.Type == PropertyType.Boolean)
                    {
                        GenerateBooleans(property, propertyName);
                    }
                    else if (property.Type == PropertyType.Integer)
                    {
                        GenerateIntegers(property, propertyName);
                    }
                    else if (property.Type == PropertyType.ItemArray)
                    {
                        GenerateITaskItemArray(property, propertyName);
                    }
                    else
                    {
                        LogError("ImproperType", property.Name, property.Type);
                    }

                    // also assign a parent for each one
                    foreach (Property dependentProperty in property.DependentArgumentProperties)
                    {
                        // Does not exist already add it to the list of parents
                        if (!dependentProperty.Parents.Contains(property.Name))
                        {
                            dependentProperty.Parents.AddLast(property.Name);
                        }
                    }

                    GenerateOverrides(property, propertyName);

                    propertyName.CustomAttributes = collection;
                    taskClass.Members.Add(propertyName);
                }
            }
        }

        /// <summary>
        /// Generates an assignment statment for the setters of properties, where the rhs is a string
        /// e.g., switchToAdd.Name = "Optimizations";
        /// </summary>
        private static void GenerateAssignPropertyToString(CodeMemberProperty propertyName, string property, string value)
        {
            if (!String.IsNullOrEmpty(value))
            {
                CodeAssignStatement setStatement = new CodeAssignStatement(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(SwitchToAdd), property), new CodeSnippetExpression(SurroundWithQuotes(value)));
                propertyName.SetStatements.Add(setStatement);
            }
        }

        /// <summary>
        /// Generates an assignment statment for the setters of properties, where the rhs is an expression
        /// e.g., switchToAdd.ArgumentRequired = true;
        /// </summary>
        private static void GenerateAssignPropertyToValue(CodeMemberProperty propertyName, string property, CodeExpression value)
        {
            ErrorUtilities.VerifyThrow(value != null, "NullValue", property);
            CodeAssignStatement setStatement = new CodeAssignStatement(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(SwitchToAdd), property), value);
            propertyName.SetStatements.Add(setStatement);
        }

        /// <summary>
        /// Generates an assignment for the toolswitch, with a prefix included
        /// i.e., switchToAdd.ToolSwitchName = "/Ox";
        /// </summary>
        private static void GenerateAssignToolSwitch(CodeMemberProperty propertyName, string property, string prefix, string toolSwitchName)
        {
            if (!String.IsNullOrEmpty(toolSwitchName))
            {
                GenerateAssignPropertyToString(propertyName, property, prefix + toolSwitchName);
            }
        }

        /// <summary>
        /// This method generates all of the common cases between different property types.
        /// The common cases are:
        /// 1) A new ToolSwitch object has to be created for each property
        /// 2) The newly created ToolSwitch has to be added to the ActiveToolSwitches list
        /// 4) For all non-empty common attributes that don't need customization, set the property
        ///    These would be:
        ///     name, type, separator, argument, argumentRequired, fallback, dependencies
        /// </summary>
        /// <param name="property">The property</param>
        /// <param name="propertyName">The CodeDom property</param>
        /// <param name="type">The type of the property</param>
        /// <param name="returnType">The return type of the property</param>
        /// <param name="valueName">The lhs of the assignment statement lhs = value</param>
        private void GenerateCommon(Property property, CodeMemberProperty propertyName, string type, Type returnType, string valueName)
        {
            // get statements
            propertyName.Type = new CodeTypeReference(returnType);
            var isSet = new CodeConditionStatement
            {
                Condition = new CodeMethodInvokeExpression(
                    new CodeThisReferenceExpression(),
                    "IsPropertySet",
                    new CodeSnippetExpression(SurroundWithQuotes(property.Name)))
            };
            isSet.TrueStatements.Add(new CodeMethodReturnStatement(new CodePropertyReferenceExpression(new CodeArrayIndexerExpression(new CodeVariableReferenceExpression(DictionaryOfSwitches), new CodeVariableReferenceExpression(SurroundWithQuotes(property.Name))), valueName)));
            if (property.Type == PropertyType.Boolean)
            {
                isSet.FalseStatements.Add(new CodeMethodReturnStatement(new CodeSnippetExpression(IsOff)));
            }
            else if (property.Type == PropertyType.Integer)
            {
                isSet.FalseStatements.Add(new CodeMethodReturnStatement(new CodePrimitiveExpression(0)));
            }
            else
            {
                isSet.FalseStatements.Add(new CodeMethodReturnStatement(new CodeSnippetExpression("null")));
            }
            propertyName.GetStatements.Add(isSet);

            // set statements
            var createNewToolSwitch = new CodeVariableDeclarationStatement(new CodeTypeReference(TypeToolSwitch), SwitchToAdd, new CodeObjectCreateExpression(TypeToolSwitch, new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(TypeToolSwitchType), type)));
            propertyName.SetStatements.Add(createNewToolSwitch);
            if (!String.IsNullOrEmpty(property.Reversible) && String.Equals(property.Reversible, IsOn, StringComparison.OrdinalIgnoreCase))
            {
                GenerateAssignPropertyToValue(propertyName, ReversibleProperty, new CodeSnippetExpression(property.Reversible));
            }

            GenerateAssignPropertyToString(propertyName, ArgumentProperty, property.Argument);
            GenerateAssignPropertyToString(propertyName, SeparatorProperty, property.Separator);
            GenerateAssignPropertyToString(propertyName, DisplayNameProperty, property.DisplayName);
            GenerateAssignPropertyToString(propertyName, DescriptionProperty, property.Description);
            if (!String.IsNullOrEmpty(property.Required) && String.Equals(
                    property.Required,
                    IsOn,
                    StringComparison.OrdinalIgnoreCase))
            {
                GenerateAssignPropertyToValue(propertyName, RequiredProperty, new CodeSnippetExpression(property.Required));
            }
            GenerateAssignPropertyToString(propertyName, FallbackProperty, property.Fallback);
            GenerateAssignPropertyToString(propertyName, FalseSuffixProperty, property.FalseSuffix);
            GenerateAssignPropertyToString(propertyName, TrueSuffixProperty, property.TrueSuffix);
            if (property.Parents.Count > 0)
            {
                foreach (string parentName in property.Parents)
                {
                    CodeMethodInvokeExpression setParent = new CodeMethodInvokeExpression(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(SwitchToAdd), ParentProperty), AddLastMethod, new CodeSnippetExpression(SurroundWithQuotes(parentName)));
                    propertyName.SetStatements.Add(setParent);
                }
            }

            if (property.IncludeInCommandLine)
            {
                var setInclude = new CodeAssignStatement(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(SwitchToAdd), "IncludeInCommandLine"), new CodePrimitiveExpression(true));
                propertyName.SetStatements.Add(setInclude);
            }

            if (GenerateComments)
            {
                // comments
                propertyName.Comments.Add(new CodeCommentStatement(ResourceUtilities.GetResourceString("StartSummary"), true));
                string commentContent = ResourceUtilities.FormatResourceString("PropertyNameDescription", property.Name);
                propertyName.Comments.Add(new CodeCommentStatement(commentContent, true));
                commentContent = ResourceUtilities.FormatResourceString("PropertyTypeDescription", type);
                propertyName.Comments.Add(new CodeCommentStatement(commentContent, true));
                commentContent = ResourceUtilities.FormatResourceString("PropertySwitchDescription", property.SwitchName);
                propertyName.Comments.Add(new CodeCommentStatement(commentContent, true));
                propertyName.Comments.Add(new CodeCommentStatement(ResourceUtilities.GetResourceString("EndSummary"), true));
            }
        }

        /// <summary>
        /// Generates standart set statements for properties.
        /// </summary>
        private static void GenerateCommonSetStatements(CodeMemberProperty propertyName, string referencedProperty)
        {
            if (referencedProperty != null)
            {
                CodeAssignStatement setValue = new CodeAssignStatement(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(SwitchToAdd), referencedProperty), new CodePropertySetValueReferenceExpression());
                propertyName.SetStatements.Add(setValue);
            }

            propertyName.SetStatements.Add(new CodeMethodInvokeExpression(new CodeThisReferenceExpression(), ReplaceToolSwitchMethod, new CodeSnippetExpression(SwitchToAdd)));
        }

        /// <summary>
        /// Generates an ITaskItem array property type.
        /// </summary>
        private void GenerateITaskItemArray(Property property, CodeMemberProperty propertyName)
        {
            var ctr = new CodeTypeReference
            {
                BaseType = "ITaskItem",
                ArrayRank = 1
            };
            GenerateCommon(property, propertyName, TypeITaskItemArray, typeof(Array), TaskItemArrayProperty);
            propertyName.Type = ctr;
            var setToolName = new CodeAssignStatement(
                new CodePropertyReferenceExpression(
                    new CodeVariableReferenceExpression(SwitchToAdd), NameProperty),
                    new CodeSnippetExpression(SurroundWithQuotes(property.Name)));
            propertyName.SetStatements.Add(setToolName);

            GenerateAssignToolSwitch(propertyName, SwitchValueProperty, property.Prefix, property.SwitchName);
            GenerateCommonSetStatements(propertyName, TaskItemArrayProperty);
        }

        /// <summary>
        /// This method generates all of the switches for integer typed properties.
        /// </summary>
        private void GenerateIntegers(Property property, CodeMemberProperty propertyName)
        {
            // set statments
            GenerateCommon(property, propertyName, TypeInteger, typeof(Int32), NumberProperty);

            // if a min or max exists, check those boundaries        
            CodeExpression[] parameters;
            string name = property.SwitchName != String.Empty ? property.Prefix + property.SwitchName : property.Name;
            if (!String.IsNullOrEmpty(property.Min) && !String.IsNullOrEmpty(property.Max))
            {
                parameters = new CodeExpression[] { new CodeSnippetExpression(SurroundWithQuotes(name)), new CodePrimitiveExpression(Int32.Parse(property.Min, CultureInfo.CurrentCulture)), new CodePrimitiveExpression(Int32.Parse(property.Max, CultureInfo.CurrentCulture)), new CodePropertySetValueReferenceExpression() };
            }
            else if (!String.IsNullOrEmpty(property.Min))
            {
                parameters = new CodeExpression[] { new CodeSnippetExpression(SurroundWithQuotes(name)), new CodePrimitiveExpression(Int32.Parse(property.Min, CultureInfo.CurrentCulture)), new CodeSnippetExpression("Int32.MaxValue"), new CodePropertySetValueReferenceExpression() };
            }
            else if (!String.IsNullOrEmpty(property.Max))
            {
                parameters = new CodeExpression[] { new CodeSnippetExpression(SurroundWithQuotes(name)), new CodeSnippetExpression("Int32.MinValue"), new CodePrimitiveExpression(Int32.Parse(property.Max, CultureInfo.CurrentCulture)), new CodePropertySetValueReferenceExpression() };
            }
            else
            {
                parameters = new CodeExpression[] { new CodeSnippetExpression(SurroundWithQuotes(name)), new CodeSnippetExpression("Int32.MinValue"), new CodeSnippetExpression("Int32.MaxValue"), new CodePropertySetValueReferenceExpression() };
            }

            var validateInt = new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), ValidateIntegerMethod);

            var isValid =
                new CodeConditionStatement { Condition = new CodeMethodInvokeExpression(validateInt, parameters) };
            isValid.TrueStatements.Add(new CodeAssignStatement(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(SwitchToAdd), IsValidProperty), new CodeSnippetExpression(IsOn)));
            isValid.FalseStatements.Add(new CodeAssignStatement(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(SwitchToAdd), IsValidProperty), new CodeSnippetExpression(IsOff)));
            propertyName.SetStatements.Add(isValid);

            var setToolName = new CodeAssignStatement(
                  new CodePropertyReferenceExpression(
                      new CodeVariableReferenceExpression(SwitchToAdd), NameProperty),
                      new CodeSnippetExpression(SurroundWithQuotes(property.Name)));
            propertyName.SetStatements.Add(setToolName);

            GenerateAssignToolSwitch(propertyName, SwitchValueProperty, property.Prefix, property.SwitchName);
            GenerateCommonSetStatements(propertyName, NumberProperty);
        }

        /// <summary>
        /// This method generates the switches for all of the nonreversible properties.
        /// </summary>
        private void GenerateBooleans(Property property, CodeMemberProperty propertyName)
        {
            // set statments
            GenerateCommon(property, propertyName, TypeBoolean, typeof(Boolean), BooleanValueProperty);
            GenerateAssignToolSwitch(propertyName, SwitchValueProperty, property.Prefix, property.SwitchName);
            GenerateAssignToolSwitch(propertyName, ReverseSwitchValueProperty, property.Prefix, property.ReverseSwitchName);

            var setToolName = new CodeAssignStatement(
                new CodePropertyReferenceExpression(
                    new CodeVariableReferenceExpression(SwitchToAdd), NameProperty),
                    new CodeSnippetExpression(SurroundWithQuotes(property.Name)));
            propertyName.SetStatements.Add(setToolName);

            GenerateCommonSetStatements(propertyName, BooleanValueProperty);
        }

        /// <summary>
        /// This method generates all of the switches for the string type property.
        /// </summary>
        private void GenerateStrings(Property property, CodeMemberProperty propertyName)
        {
            GenerateCommon(property, propertyName, TypeString, typeof(string), FileNameProperty);
            string propertyToReceiveValue;

            // if there are no enums, the value is the fileName, otherwise, the value is the enum's name
            if (property.Values.Count > 0)
            {
                var createArray = new CodeVariableDeclarationStatement("Tuple<string, string, Tuple<string, bool>[]>[]", SwitchMap);
                var codeExpressions = new List<CodeExpression>();

                foreach (Value val in property.Values)
                {
                    if (ContainsCurrentPlatform(val.SwitchName))
                    {
                        // Create the array of argument expressions.                        
                        var argumentInitializers = new List<CodeObjectCreateExpression>(val.Arguments.Count);
                        foreach (Argument arg in val.Arguments)
                        {
                            argumentInitializers.Add(new CodeObjectCreateExpression(new CodeTypeReference("Tuple<string, bool>"),
                                new CodeSnippetExpression(SurroundWithQuotes(arg.Parameter)),
                                new CodePrimitiveExpression(arg.Required)));
                        }

                        // Now create the entry for the switch itself.
                        var valueExpression = new CodeObjectCreateExpression(new CodeTypeReference("Tuple<string, string, Tuple<string, bool>[]>"),
                            new CodeSnippetExpression(SurroundWithQuotes(val.Name)),
                            val.SwitchName != String.Empty ? new CodeSnippetExpression(SurroundWithQuotes(val.Prefix + val.SwitchName)) : new CodeSnippetExpression(SurroundWithQuotes("")),
                            new CodeArrayCreateExpression(new CodeTypeReference("Tuple<string, bool>"), argumentInitializers.ToArray()));

                        codeExpressions.Add(valueExpression);
                    }
                }

                // Initialize the switch array
                var initializeArray = new CodeArrayCreateExpression("Tuple<string, string, Tuple<string, bool>[]>[]", codeExpressions.ToArray());
                createArray.InitExpression = initializeArray;
                propertyName.SetStatements.Add(createArray);

                // Create an index variable to hold the entry in the array we matched
                var indexDecl = new CodeVariableDeclarationStatement(typeof(int), "i", new CodeMethodInvokeExpression(
                            new CodeThisReferenceExpression(), ReadSwitchMapMethod,
                            new CodeSnippetExpression(SurroundWithQuotes(property.Name)),
                            new CodeVariableReferenceExpression(SwitchMap),
                            new CodeVariableReferenceExpression(ValueAttribute)));
                propertyName.SetStatements.Add(indexDecl);

                // Set the switch value from the index into the array
                var setToolSwitchNameGoodIndex = new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        new CodeVariableReferenceExpression(SwitchToAdd), SwitchValueProperty),
                        new CodePropertyReferenceExpression(new CodeArrayIndexerExpression(new CodeVariableReferenceExpression("switchMap"), new CodeVariableReferenceExpression("i")), "Item2"));

                // Set the arguments
                var setArgumentsGoodIndex = new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        new CodeVariableReferenceExpression(SwitchToAdd), "Arguments"),
                        new CodePropertyReferenceExpression(new CodeArrayIndexerExpression(new CodeVariableReferenceExpression("switchMap"), new CodeVariableReferenceExpression("i")), "Item3"));

                // Set the switch value from the index into the array
                var setToolSwitchNameBadIndex = new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        new CodeVariableReferenceExpression(SwitchToAdd), SwitchValueProperty),
                        new CodePrimitiveExpression(String.Empty));

                // Set the arguments
                var setArgumentsBadIndex = new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        new CodeVariableReferenceExpression(SwitchToAdd), "Arguments"),
                       new CodePrimitiveExpression(null));

                // Create a CodeConditionStatement that tests a boolean value named boolean.
                var conditionalStatement = new CodeConditionStatement(
                    // The condition to test.
                    new CodeVariableReferenceExpression("i >= 0"),
                    // The statements to execute if the condition evaluates to true.
                    new CodeStatement[] { setToolSwitchNameGoodIndex, setArgumentsGoodIndex },
                    // The statements to execute if the condition evalues to false.
                    new CodeStatement[] { setToolSwitchNameBadIndex, setArgumentsBadIndex });

                propertyName.SetStatements.Add(conditionalStatement);
                // Set the separator
                var setSeparator = new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        new CodeVariableReferenceExpression(SwitchToAdd), "Separator"),
                        new CodeSnippetExpression(SurroundWithQuotes(property.Separator)));
                propertyName.SetStatements.Add(setSeparator);

                // Set the tool name
                var setToolName = new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        new CodeVariableReferenceExpression(SwitchToAdd), NameProperty),
                        new CodeSnippetExpression(SurroundWithQuotes(property.Name)));
                propertyName.SetStatements.Add(setToolName);

                propertyToReceiveValue = ValueProperty;
                GenerateAssignPropertyToValue(propertyName, MultiValues, new CodeSnippetExpression(IsOn));
            }
            else
            {
                var setToolName = new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        new CodeVariableReferenceExpression(SwitchToAdd), NameProperty),
                        new CodeSnippetExpression(SurroundWithQuotes(property.Name)));
                propertyName.SetStatements.Add(setToolName);

                propertyToReceiveValue = FileNameProperty;
                CodeAssignStatement setToolSwitchName = new CodeAssignStatement(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(SwitchToAdd), SwitchValueProperty), property.SwitchName != String.Empty ? new CodeSnippetExpression(SurroundWithQuotes(property.Prefix + property.SwitchName)) : new CodeSnippetExpression(SurroundWithQuotes("")));
                propertyName.SetStatements.Add(setToolSwitchName);
                GenerateAssignToolSwitch(propertyName, ReverseSwitchValueProperty, property.Prefix, property.ReverseSwitchName);
            }

            GenerateCommonSetStatements(propertyName, propertyToReceiveValue);
        }

        /// <summary>
        /// Returns true if the property refers to the current platform.
        /// </summary>
        private bool ContainsCurrentPlatform(Property property)
        {
            if (Platform == null)
                return true;

            if (property.Values.Count > 0)
            {
                bool containsCurrentPlatform = false;
                foreach (Value val in property.Values)
                {
                    containsCurrentPlatform = ContainsCurrentPlatform(val.SwitchName) || containsCurrentPlatform;
                }
                return containsCurrentPlatform;
            }
            else
            {
                return ContainsCurrentPlatform(property.SwitchName);
            }
        }

        /// <summary>
        /// Returns true if the switch value refers to the current platform.
        /// </summary>
        private bool ContainsCurrentPlatform(string SwitchValue)
        {
            // If we don't have a platform defined it meens all
            if (Platform == null)
                return true;

            if (_relationsParser.SwitchRelationsList.ContainsKey(SwitchValue))
            {
                SwitchRelations rel = _relationsParser.SwitchRelationsList[SwitchValue];
                if (rel.ExcludedPlatforms.Count > 0)
                {
                    foreach (string excludedPlatform in rel.ExcludedPlatforms)
                    {
                        if (Platform == excludedPlatform)
                            return false;
                    }
                }
                if (rel.IncludedPlatforms.Count > 0)
                {
                    bool isIncluded = false;
                    foreach (string includedPlatform in rel.IncludedPlatforms)
                    {
                        if (Platform == includedPlatform)
                            isIncluded = true;
                    }
                    return isIncluded;
                }
            }
            return true;
        }

        /// <summary>
        /// This method generates overrides array 
        /// </summary>
        private void GenerateOverrides(Property property, CodeMemberProperty propertyName)
        {
            if (_relationsParser.SwitchRelationsList.ContainsKey(property.SwitchName))
            {
                SwitchRelations rel = _relationsParser.SwitchRelationsList[property.SwitchName];
                if (rel.Overrides.Count > 0)
                {
                    foreach (string overrided in rel.Overrides)
                    {
                        propertyName.SetStatements.Add(new CodeMethodInvokeExpression(
                            new CodeFieldReferenceExpression(
                                new CodeVariableReferenceExpression(SwitchToAdd), Overrides), AddLastMethod,
                                    new CodeObjectCreateExpression(
                                        new CodeTypeReference(TypeKeyValuePairStrings),
                                        new CodeSnippetExpression(SurroundWithQuotes(rel.SwitchValue)),
                                        new CodeSnippetExpression(SurroundWithQuotes(overrided)))));
                    }
                }

                if (property.ReverseSwitchName != "")
                {
                    rel = _relationsParser.SwitchRelationsList[property.ReverseSwitchName];
                    if (rel.Overrides.Count > 0)
                    {
                        foreach (string overrided in rel.Overrides)
                        {
                            propertyName.SetStatements.Add(new CodeMethodInvokeExpression(
                                new CodeFieldReferenceExpression(
                                    new CodeVariableReferenceExpression(SwitchToAdd), Overrides), AddLastMethod,
                                new CodeObjectCreateExpression(
                                    new CodeTypeReference(TypeKeyValuePairStrings),
                                    new CodeSnippetExpression(SurroundWithQuotes(rel.SwitchValue)),
                                    new CodeSnippetExpression(SurroundWithQuotes(overrided)))));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This method generates switches for all the properties that are of type
        /// string array
        /// </summary>
        private void GenerateStringArrays(Property property, CodeMemberProperty propertyName)
        {
            var ctr = new CodeTypeReference
            {
                BaseType = "System.String",
                ArrayRank = 1
            };
            GenerateCommon(property, propertyName, TypeStringArray, typeof(Array), StringListProperty);
            propertyName.Type = ctr;
            GenerateAssignToolSwitch(propertyName, SwitchValueProperty, property.Prefix, property.SwitchName);
            CodeAssignStatement setToolName = new CodeAssignStatement(
                   new CodePropertyReferenceExpression(
                       new CodeVariableReferenceExpression(SwitchToAdd), NameProperty),
                       new CodeSnippetExpression(SurroundWithQuotes(property.Name)));
            propertyName.SetStatements.Add(setToolName);
            GenerateCommonSetStatements(propertyName, StringListProperty);
        }

        /// <summary>
        /// This method generates the property that returns the tool exe value set by the ToolExe property
        /// </summary>
        private void GenerateToolNameProperty(CodeTypeDeclaration taskClass)
        {
            var toolNameAccessor = new CodeMemberProperty
            {
                Name = ToolNamePropertyName,
                HasGet = true,
                HasSet = false,
                Attributes = MemberAttributes.Override | MemberAttributes.Family,
                Type = new CodeTypeReference(typeof(string))
            };

            string commentContent;

            if (GenerateComments)
            {
                // Comment on this property assignment
                commentContent = ResourceUtilities.GetResourceString("ToolExeFieldDescription");
                toolNameAccessor.GetStatements.Add(new CodeCommentStatement(commentContent, false));
            }

            toolNameAccessor.GetStatements.Add(new CodeMethodReturnStatement(new CodeSnippetExpression(SurroundWithQuotes(_taskParser.ToolName))));
            taskClass.Members.Add(toolNameAccessor);

            if (GenerateComments)
            {
                // comments
                toolNameAccessor.Comments.Add(new CodeCommentStatement(ResourceUtilities.GetResourceString("StartSummary"), true));
                commentContent = ResourceUtilities.GetResourceString("ToolNameDescription");
                toolNameAccessor.Comments.Add(new CodeCommentStatement(commentContent, true));
                toolNameAccessor.Comments.Add(new CodeCommentStatement(ResourceUtilities.GetResourceString("EndSummary"), true));
            }
        }

        /// <summary>
        /// This method generates the code that appears at the top of each class (that imports other libraries)
        /// </summary>
        private static void GenerateImports(CodeNamespace codeNamespace)
        {
            string[] imports =
            {
                "System",
                "System.Globalization",
                "System.Collections",
                "System.Collections.Generic",
                "System.Diagnostics",
                "System.IO",
                "Microsoft.Build.Utilities",
                "Microsoft.Build.Framework",
                "Microsoft.Build.Tasks.Xaml",
            };

            foreach (string reference in imports)
            {
                codeNamespace.Imports.Add(new CodeNamespaceImport(reference));
            }
        }

        /// <summary>
        /// This method generates the default constructor for the generated task
        /// </summary>
        private void GenerateConstructor(CodeTypeDeclaration taskClass)
        {
            var defaultConstructor = new CodeConstructor { Attributes = MemberAttributes.Public };

            // new System.Resources.ResourceManager("Microsoft.Build.NativeTasks.Strings", System.Reflection.Assembly.GetExecutingAssembly()))
            var resourceManagerType = new CodeTypeReference("System.Resources.ResourceManager");
            var resourceNamespaceString = new CodeSnippetExpression(SurroundWithQuotes(_taskParser.ResourceNamespace));
            var systemReflectionAssembly = new CodeTypeReferenceExpression("System.Reflection.Assembly");
            var getExecutingAssemblyReference = new CodeMethodReferenceExpression(systemReflectionAssembly, "GetExecutingAssembly");
            var getExecutingAssembly = new CodeMethodInvokeExpression(getExecutingAssemblyReference);
            var resourceManager = new CodeObjectCreateExpression(resourceManagerType, resourceNamespaceString, getExecutingAssembly);

            var switchOrderArrayType = new CodeTypeReference(new CodeTypeReference("System.String"), 1);
            var valueExpressions = new List<CodeExpression>();
            foreach (string switchName in _taskParser.SwitchOrderList)
            {
                valueExpressions.Add(new CodeSnippetExpression(SurroundWithQuotes(switchName)));
            }

            var arrayExpression = new CodeArrayCreateExpression(switchOrderArrayType, valueExpressions.ToArray());
            defaultConstructor.BaseConstructorArgs.Add(arrayExpression);
            defaultConstructor.BaseConstructorArgs.Add(resourceManager);

            taskClass.Members.Add(defaultConstructor);

            if (GenerateComments)
            {
                // comments
                defaultConstructor.Comments.Add(new CodeCommentStatement(ResourceUtilities.GetResourceString("StartSummary"), true));
                string commentContent = ResourceUtilities.GetResourceString("ConstructorDescription");
                defaultConstructor.Comments.Add(new CodeCommentStatement(commentContent, true));
                defaultConstructor.Comments.Add(new CodeCommentStatement(ResourceUtilities.GetResourceString("EndSummary"), true));
            }
        }

        /// <summary>
        /// This method generates the relations which will be used at runtime to validate the command line
        /// </summary>
        private void GenerateRelations(CodeTypeDeclaration taskClass)
        {
            if (_relationsParser.SwitchRelationsList.Count > 0)
            {
                var addValidateRelationsMethod = new CodeMemberMethod
                {
                    Name = ValidateRelationsMethod,
                    Attributes = MemberAttributes.Family | MemberAttributes.Override
                };

                foreach (KeyValuePair<string, SwitchRelations> switchRelations in _relationsParser.SwitchRelationsList)
                {
                    if (switchRelations.Value.Requires.Count > 0)
                    {
                        var checkRequired = new CodeConditionStatement { Condition = null };
                        
                        foreach (string required in switchRelations.Value.Requires)
                        {
                            if (checkRequired.Condition != null)
                            {
                                checkRequired.Condition = new CodeBinaryOperatorExpression(
                                    checkRequired.Condition, CodeBinaryOperatorType.BooleanAnd, new CodeBinaryOperatorExpression(new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), IsSwitchValueSetMethod), new CodeSnippetExpression(SurroundWithQuotes(required))), CodeBinaryOperatorType.IdentityEquality, new CodeSnippetExpression("false")));
                            }
                            else
                            {
                                checkRequired.Condition = new CodeBinaryOperatorExpression(new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), IsSwitchValueSetMethod), new CodeSnippetExpression(SurroundWithQuotes(required))), CodeBinaryOperatorType.IdentityEquality, new CodeSnippetExpression("false"));
                            }
                        }

                        checkRequired.TrueStatements.Add(new CodeMethodInvokeExpression
                            (new CodeThisReferenceExpression(), "RemoveSwitchToolBasedOnValue",
                            new CodeSnippetExpression(SurroundWithQuotes(switchRelations.Key))));

                        addValidateRelationsMethod.Statements.Add(checkRequired);
                    }
                }

                taskClass.Members.Add(addValidateRelationsMethod);

                if (GenerateComments)
                {
                    // comments
                    addValidateRelationsMethod.Comments.Add(new CodeCommentStatement(ResourceUtilities.GetResourceString("StartSummary"), true));
                    string commentContent = ResourceUtilities.GetResourceString("AddValidateRelationsMethod");
                    addValidateRelationsMethod.Comments.Add(new CodeCommentStatement(commentContent, true));
                    addValidateRelationsMethod.Comments.Add(new CodeCommentStatement(ResourceUtilities.GetResourceString("EndSummary"), true));
                }
            }
        }

        #endregion

        #region Miscellaneous methods

        /// <summary>
        /// Increases the error count by 1, and logs the error message
        /// </summary>
        private void LogError(string messageResourceName, params object[] messageArgs)
        {
            ErrorLog.AddLast(ResourceUtilities.FormatResourceString(messageResourceName, messageArgs));
            ErrorCount++;
        }

        /// <summary>
        /// Puts a string inside two quotes
        /// </summary>
        private static string SurroundWithQuotes(string unformattedText)
        {
            if (String.IsNullOrEmpty(unformattedText))
            {
                return "@\"\"";
            }
            else
            {
                return "@\"" + unformattedText.Replace("\"", "\"\"") + "\"";
            }
        }

        #endregion

        /// <summary>
        /// Returns the number of errors encountered
        /// </summary>
        internal int ErrorCount { get; private set; }

        /// <summary>
        /// Returns the log of errors
        /// </summary>
        internal LinkedList<string> ErrorLog { get; } = new LinkedList<string>();
    }
}
