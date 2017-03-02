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
        /// The base class for the task
        /// </summary>
        private const string BaseClass = "DataDrivenToolTask";

        /// <summary>
        /// The namespace for the task.
        /// </summary>
        private const string NamespaceOfGeneratedTask = "MyDataDrivenTasks";

        /// <summary>
        /// The property for the tool name.
        /// </summary>
        private const string ToolNamePropertyName = "ToolName";

        /// <summary>
        /// The property for the tool exe.
        /// </summary>
        private const string ToolExePropertyName = "ToolExe";

        /// <summary>
        /// The field for the tool exe.
        /// </summary>
        private const string ToolExeFieldName = "toolExe";

        /// <summary>
        /// IsOn
        /// </summary>
        private const string IsOn = "true";

        /// <summary>
        /// IsOff
        /// </summary>
        private const string IsOff = "false";

        /// <summary>
        /// AlwaysType
        /// </summary>
        private const string AlwaysType = "always";

        /// <summary>
        /// The value attribute.
        /// </summary>
        private const string ValueAttribute = "value";

        // --------------------
        // ToolSwitchType types
        // --------------------

        /// <summary>
        /// The always type
        /// </summary>
        private const string TypeAlways = "always";

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
        /// The file type
        /// </summary>
        private const string TypeFile = "File";

        /// <summary>
        /// The directory type
        /// </summary>
        private const string TypeDirectory = "Directory";

        /// <summary>
        /// The ITaskItem type
        /// </summary>
        private const string TypeITaskItem = "ITaskItem";

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

        /// <summary>
        /// The AlwaysAppend type.
        /// </summary>
        private const string TypeAlwaysAppend = "AlwaysAppend";

        /// <summary>
        /// The ArgumentRelation type.
        /// </summary>
        private const string TypeArgumentRelation = "CommandLineArgumentRelation";

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
        /// The ActiveToolSwitchesValues property.
        /// </summary>
        private const string DictionaryOfSwitchesValues = "ActiveToolSwitchesValues";

        /// <summary>
        /// The switchMap field.
        /// </summary>
        private const string SwitchMap = "switchMap";

        /// <summary>
        /// The MultiValues property.
        /// </summary>
        private const string MultiValues = "AllowMultipleValues";

        /// <summary>
        /// The relation field.
        /// </summary>
        private const string Relation = "relation";

        // --------------
        // Common methods
        // --------------

        /// <summary>
        /// The Add method.
        /// </summary>
        private const string AddMethod = "Add";

        /// <summary>
        /// The AddLast method.
        /// </summary>
        private const string AddLastMethod = "AddLast";

        /// <summary>
        /// The AlwaysAppend method.
        /// </summary>
        private const string AppendAlwaysMethod = "AlwaysAppend";

        /// <summary>
        /// The ValidateInteger method.
        /// </summary>
        private const string ValidateIntegerMethod = "ValidateInteger";

        /// <summary>
        /// The ReadSwitchMap method.
        /// </summary>
        private const string ReadSwitchMapMethod = "ReadSwitchMap2";

        /// <summary>
        /// The Remove method.
        /// </summary>
        private const string RemoveMethod = "Remove";

        /// <summary>
        /// The IsPropertySet method.
        /// </summary>
        private const string IsPropertySetMethod = "IsPropertySet";

        /// <summary>
        /// The IsSwitchValueSet method.
        /// </summary>
        private const string IsSwitchValueSetMethod = "IsSwitchValueSet";

        /// <summary>
        /// The EnsureTrailingSlash method.
        /// </summary>
        private const string EnsureTrailingSlashMethod = "EnsureTrailingSlash";

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
        /// The AddActiveSwitchToolValue method.
        /// </summary>
        private const string AddActiveSwitchToolValueMethod = "AddActiveSwitchToolValue";

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
        /// The TaskItem property
        /// </summary>
        private const string TaskItemProperty = "TaskItem";

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
        /// The ArgumentRequired property
        /// </summary>
        private const string ArgumentRequiredProperty = "ArgumentRequired";

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
        /// The ArgumentRelationList property
        /// </summary>
        private const string ArgumentRelationList = "ArgumentRelationList";

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
        /// The Type property
        /// </summary>
        private const string TypeProperty = "Type";

        /// <summary>
        /// Types to ignore.
        /// </summary>
        private string[] _propertiesTypesToIgnore = { "AdditionalOptions", "CommandLineTemplate" };

        #endregion

        /// <summary>
        /// The current platform.
        /// </summary>
        private string _platform = String.Empty;

        /// <summary>
        /// The number of errors that occurred while parsing the xml file or generating the code
        /// </summary>
        private int _errorCount;

        /// <summary>
        /// The errors that occurred while parsing the xml file or generating the code
        /// </summary>
        private LinkedList<string> _errorLog = new LinkedList<string>();

        /// <summary>
        /// The xml parsers
        /// </summary>
        private TaskParser _taskParser = new TaskParser();

        /// <summary>
        /// The relations parser
        /// </summary>
        private RelationsParser _relationsParser = new RelationsParser();

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
        public bool GenerateComments
        {
            get;
            set;
        }

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
        private string Platform
        {
            get
            {
                return _platform;
            }
        }

        #region Generate code methods

        /// <summary>
        /// Removes properties that have types we are ignoring.
        /// </summary>
        internal void RemovePropertiesWithIgnoredTypes(LinkedList<Property> propertyList)
        {
            LinkedList<Property> propertyToIgnoreList = new LinkedList<Property>();
            foreach (Property property in propertyList)
            {
                foreach (string propertyToIgnore in _propertiesTypesToIgnore)
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
                CodeCompileUnit compileUnit = new CodeCompileUnit();
                CodeNamespace dataDrivenToolTaskNamespace = new CodeNamespace(_taskParser.Namespace);
                CodeTypeDeclaration taskClass = new CodeTypeDeclaration(_taskParser.GeneratedTaskName);

                if (GenerateComments)
                {
                    // add comments to the class
                    taskClass.Comments.Add(new CodeCommentStatement(ResourceUtilities.FormatResourceString("StartSummary"), true));
                    string commentContent = ResourceUtilities.FormatResourceString("ClassDescription", _taskParser.GeneratedTaskName);
                    taskClass.Comments.Add(new CodeCommentStatement(commentContent, true));
                    taskClass.Comments.Add(new CodeCommentStatement(ResourceUtilities.FormatResourceString("EndSummary"), true));
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
                CodeMemberMethod addToActiveSwitchList = new CodeMemberMethod();
                addToActiveSwitchList.Name = AddDefaultsToActiveSwitchList;
                addToActiveSwitchList.Attributes = MemberAttributes.Family | MemberAttributes.Override;
                foreach (Property Property in _taskParser.DefaultSet)
                {
                    CodeConditionStatement removeExisting = new CodeConditionStatement();
                    removeExisting.Condition = new CodeBinaryOperatorExpression(new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), IsPropertySetMethod), new CodeSnippetExpression(SurroundWithQuotes(Property.Name))), CodeBinaryOperatorType.IdentityEquality, new CodeSnippetExpression("false"));
                    if (Property.Type == PropertyType.Boolean)
                    {
                        removeExisting.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression(Property.Name), new CodeSnippetExpression(Property.DefaultValue)));
                    }
                    else
                    {
                        removeExisting.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression(Property.Name), new CodeSnippetExpression(SurroundWithQuotes(Property.DefaultValue))));
                    }
                    addToActiveSwitchList.Statements.Add(removeExisting);
                }
                taskClass.Members.Add(addToActiveSwitchList);

                if (GenerateComments)
                {
                    // comments
                    addToActiveSwitchList.Comments.Add(new CodeCommentStatement(ResourceUtilities.FormatResourceString("StartSummary"), true));
                    string commentContent = ResourceUtilities.FormatResourceString("AddDefaultsToActiveSwitchListDescription");
                    addToActiveSwitchList.Comments.Add(new CodeCommentStatement(commentContent, true));
                    addToActiveSwitchList.Comments.Add(new CodeCommentStatement(ResourceUtilities.FormatResourceString("EndSummary"), true));
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
                CodeMemberMethod addToActiveSwitchList = new CodeMemberMethod();
                addToActiveSwitchList.Name = AddFallbacksToActiveSwitchList;
                addToActiveSwitchList.Attributes = MemberAttributes.Family | MemberAttributes.Override;
                foreach (KeyValuePair<string, string> fallbackParameter in _taskParser.FallbackSet)
                {
                    CodeConditionStatement removeExisting = new CodeConditionStatement();
                    CodeMethodInvokeExpression isPropertySet = new CodeMethodInvokeExpression(new CodeThisReferenceExpression(), IsPropertySetMethod, new CodeSnippetExpression(SurroundWithQuotes(fallbackParameter.Value)));
                    CodeBinaryOperatorExpression propertyNotSet = new CodeBinaryOperatorExpression(new CodeMethodInvokeExpression(new CodeThisReferenceExpression(), IsPropertySetMethod, new CodeSnippetExpression(SurroundWithQuotes(fallbackParameter.Key))), CodeBinaryOperatorType.ValueEquality, new CodeSnippetExpression(IsOff));
                    removeExisting.Condition = new CodeBinaryOperatorExpression(propertyNotSet, CodeBinaryOperatorType.BooleanAnd, isPropertySet);
                    removeExisting.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression(fallbackParameter.Key), new CodeVariableReferenceExpression(fallbackParameter.Value)));
                    addToActiveSwitchList.Statements.Add(removeExisting);
                }
                taskClass.Members.Add(addToActiveSwitchList);

                if (GenerateComments)
                {
                    // comments
                    addToActiveSwitchList.Comments.Add(new CodeCommentStatement(ResourceUtilities.FormatResourceString("StartSummary"), true));
                    string commentContent = ResourceUtilities.FormatResourceString("AddFallbacksToActiveSwitchListDescription");
                    addToActiveSwitchList.Comments.Add(new CodeCommentStatement(commentContent, true));
                    addToActiveSwitchList.Comments.Add(new CodeCommentStatement(ResourceUtilities.FormatResourceString("EndSummary"), true));
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
                        continue;

                    CodeAttributeDeclarationCollection collection = new CodeAttributeDeclarationCollection();
                    CodeMemberProperty propertyName = new CodeMemberProperty();
                    propertyName.Name = property.Name;
                    propertyName.HasGet = true;
                    propertyName.HasSet = true;
                    propertyName.Attributes = MemberAttributes.Public;

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
        private void GenerateAssignPropertyToString(CodeMemberProperty propertyName, string property, string value)
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
        private void GenerateAssignPropertyToValue(CodeMemberProperty propertyName, string property, CodeExpression value)
        {
            ErrorUtilities.VerifyThrow(value != null, "NullValue", property);
            CodeAssignStatement setStatement = new CodeAssignStatement(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(SwitchToAdd), property), value);
            propertyName.SetStatements.Add(setStatement);
        }

        /// <summary>
        /// Generates an assignment for the toolswitch, with a prefix included
        /// i.e., switchToAdd.ToolSwitchName = "/Ox";
        /// </summary>
        private void GenerateAssignToolSwitch(CodeMemberProperty propertyName, string property, string prefix, string toolSwitchName)
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
            CodeConditionStatement isSet = new CodeConditionStatement();
            isSet.Condition = new CodeMethodInvokeExpression(new CodeThisReferenceExpression(), "IsPropertySet", new CodeExpression[] { new CodeSnippetExpression(SurroundWithQuotes(property.Name)) });
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
            CodeVariableDeclarationStatement createNewToolSwitch = new CodeVariableDeclarationStatement(new CodeTypeReference(TypeToolSwitch), SwitchToAdd, new CodeObjectCreateExpression(TypeToolSwitch, new CodeExpression[] { new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(TypeToolSwitchType), type) }));
            propertyName.SetStatements.Add(createNewToolSwitch);
            if (!String.IsNullOrEmpty(property.Reversible) && String.Equals(property.Reversible, IsOn, StringComparison.OrdinalIgnoreCase))
            {
                GenerateAssignPropertyToValue(propertyName, ReversibleProperty, new CodeSnippetExpression(property.Reversible));
            }

            GenerateAssignPropertyToString(propertyName, ArgumentProperty, property.Argument);
            GenerateAssignPropertyToString(propertyName, SeparatorProperty, property.Separator);
            GenerateAssignPropertyToString(propertyName, DisplayNameProperty, property.DisplayName);
            GenerateAssignPropertyToString(propertyName, DescriptionProperty, property.Description);
            if (!String.IsNullOrEmpty(property.Required) && String.Equals(property.Required, IsOn, StringComparison.OrdinalIgnoreCase))
                GenerateAssignPropertyToValue(propertyName, RequiredProperty, new CodeSnippetExpression(property.Required));
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
                CodeAssignStatement setInclude = new CodeAssignStatement(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(SwitchToAdd), "IncludeInCommandLine"), new CodePrimitiveExpression(true));
                propertyName.SetStatements.Add(setInclude);
            }

            if (GenerateComments)
            {
                // comments
                propertyName.Comments.Add(new CodeCommentStatement(ResourceUtilities.FormatResourceString("StartSummary"), true));
                string commentContent = ResourceUtilities.FormatResourceString("PropertyNameDescription", property.Name);
                propertyName.Comments.Add(new CodeCommentStatement(commentContent, true));
                commentContent = ResourceUtilities.FormatResourceString("PropertyTypeDescription", type);
                propertyName.Comments.Add(new CodeCommentStatement(commentContent, true));
                commentContent = ResourceUtilities.FormatResourceString("PropertySwitchDescription", property.SwitchName);
                propertyName.Comments.Add(new CodeCommentStatement(commentContent, true));
                propertyName.Comments.Add(new CodeCommentStatement(ResourceUtilities.FormatResourceString("EndSummary"), true));
            }
        }

        /// <summary>
        /// Generates standart set statements for properties.
        /// </summary>
        private void GenerateCommonSetStatements(Property property, CodeMemberProperty propertyName, string referencedProperty)
        {
            if (referencedProperty != null)
            {
                CodeAssignStatement setValue = new CodeAssignStatement(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(SwitchToAdd), referencedProperty), new CodePropertySetValueReferenceExpression());
                propertyName.SetStatements.Add(setValue);
            }

            propertyName.SetStatements.Add(new CodeMethodInvokeExpression(new CodeThisReferenceExpression(), ReplaceToolSwitchMethod, new CodeExpression[] { new CodeSnippetExpression(SwitchToAdd) }));
        }

        /// <summary>
        /// Generates an ITaskItem array property type.
        /// </summary>
        private void GenerateITaskItemArray(Property property, CodeMemberProperty propertyName)
        {
            CodeTypeReference ctr = new CodeTypeReference();
            ctr.BaseType = "ITaskItem";
            ctr.ArrayRank = 1;
            GenerateCommon(property, propertyName, TypeITaskItemArray, typeof(Array), TaskItemArrayProperty);
            propertyName.Type = ctr;
            CodeAssignStatement setToolName = new CodeAssignStatement(
              new CodePropertyReferenceExpression(
                  new CodeVariableReferenceExpression(SwitchToAdd), NameProperty),
                  new CodeSnippetExpression(SurroundWithQuotes(property.Name)));
            propertyName.SetStatements.Add(setToolName);

            GenerateAssignToolSwitch(propertyName, SwitchValueProperty, property.Prefix, property.SwitchName);
            GenerateCommonSetStatements(property, propertyName, TaskItemArrayProperty);
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

            CodeMethodReferenceExpression validateInt = new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), ValidateIntegerMethod);

            CodeConditionStatement isValid = new CodeConditionStatement();
            isValid.Condition = new CodeMethodInvokeExpression(validateInt, parameters);
            isValid.TrueStatements.Add(new CodeAssignStatement(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(SwitchToAdd), IsValidProperty), new CodeSnippetExpression(IsOn)));
            isValid.FalseStatements.Add(new CodeAssignStatement(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(SwitchToAdd), IsValidProperty), new CodeSnippetExpression(IsOff)));
            propertyName.SetStatements.Add(isValid);

            CodeAssignStatement setToolName = new CodeAssignStatement(
                  new CodePropertyReferenceExpression(
                      new CodeVariableReferenceExpression(SwitchToAdd), NameProperty),
                      new CodeSnippetExpression(SurroundWithQuotes(property.Name)));
            propertyName.SetStatements.Add(setToolName);

            GenerateAssignToolSwitch(propertyName, SwitchValueProperty, property.Prefix, property.SwitchName);
            GenerateCommonSetStatements(property, propertyName, NumberProperty);
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

            CodeAssignStatement setToolName = new CodeAssignStatement(
            new CodePropertyReferenceExpression(
                new CodeVariableReferenceExpression(SwitchToAdd), NameProperty),
                new CodeSnippetExpression(SurroundWithQuotes(property.Name)));
            propertyName.SetStatements.Add(setToolName);

            GenerateCommonSetStatements(property, propertyName, BooleanValueProperty);
        }

        /// <summary>
        /// This method generates all of the switches for the string type property.
        /// </summary>
        private void GenerateStrings(Property property, CodeMemberProperty propertyName)
        {
            GenerateCommon(property, propertyName, TypeString, typeof(string), FileNameProperty);
            string propertyToReceiveValue = null;

            // if there are no enums, the value is the fileName, otherwise, the value is the enum's name
            if (property.Values.Count > 0)
            {
                CodeVariableDeclarationStatement createArray = new CodeVariableDeclarationStatement("Tuple<string, string, Tuple<string, bool>[]>[]", SwitchMap);
                List<CodeExpression> codeExpressions = new List<CodeExpression>();

                CodeTypeReference temporaryArrayType = new CodeTypeReference(typeof(string));
                foreach (Value val in property.Values)
                {
                    if (ContainsCurrentPlatform(val.SwitchName))
                    {
                        // Create the array of argument expressions.                        
                        List<CodeObjectCreateExpression> argumentInitializers = new List<CodeObjectCreateExpression>(val.Arguments.Count);
                        foreach (Argument arg in val.Arguments)
                        {
                            argumentInitializers.Add(new CodeObjectCreateExpression(new CodeTypeReference("Tuple<string, bool>"),
                                new CodeSnippetExpression(SurroundWithQuotes(arg.Parameter)),
                                new CodePrimitiveExpression(arg.Required)));
                        }

                        // Now create the entry for the switch itself.
                        CodeObjectCreateExpression valueExpression = new CodeObjectCreateExpression(new CodeTypeReference("Tuple<string, string, Tuple<string, bool>[]>"),
                            new CodeSnippetExpression(SurroundWithQuotes(val.Name)),
                            val.SwitchName != String.Empty ? new CodeSnippetExpression(SurroundWithQuotes(val.Prefix + val.SwitchName)) : new CodeSnippetExpression(SurroundWithQuotes("")),
                            new CodeArrayCreateExpression(new CodeTypeReference("Tuple<string, bool>"), argumentInitializers.ToArray()));

                        codeExpressions.Add(valueExpression);
                    }
                }

                // Initialize the switch array
                CodeArrayCreateExpression initializeArray = new CodeArrayCreateExpression("Tuple<string, string, Tuple<string, bool>[]>[]", codeExpressions.ToArray());
                createArray.InitExpression = initializeArray;
                propertyName.SetStatements.Add(createArray);

                // Create an index variable to hold the entry in the array we matched
                CodeVariableDeclarationStatement indexDecl = new CodeVariableDeclarationStatement(typeof(int), "i", new CodeMethodInvokeExpression(
                            new CodeThisReferenceExpression(), ReadSwitchMapMethod,
                            new CodeExpression[] { new CodeSnippetExpression(SurroundWithQuotes(property.Name)),
                                new CodeVariableReferenceExpression(SwitchMap),
                                new CodeVariableReferenceExpression(ValueAttribute) }));
                propertyName.SetStatements.Add(indexDecl);


                // Set the switch value from the index into the array
                CodeAssignStatement setToolSwitchNameGoodIndex = new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        new CodeVariableReferenceExpression(SwitchToAdd), SwitchValueProperty),
                        new CodePropertyReferenceExpression(new CodeArrayIndexerExpression(new CodeVariableReferenceExpression("switchMap"), new CodeVariableReferenceExpression("i")), "Item2"));

                // Set the arguments
                CodeAssignStatement setArgumentsGoodIndex = new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        new CodeVariableReferenceExpression(SwitchToAdd), "Arguments"),
                        new CodePropertyReferenceExpression(new CodeArrayIndexerExpression(new CodeVariableReferenceExpression("switchMap"), new CodeVariableReferenceExpression("i")), "Item3"));

                // Set the switch value from the index into the array
                CodeAssignStatement setToolSwitchNameBadIndex = new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        new CodeVariableReferenceExpression(SwitchToAdd), SwitchValueProperty),
                        new CodePrimitiveExpression(String.Empty));

                // Set the arguments
                CodeAssignStatement setArgumentsBadIndex = new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        new CodeVariableReferenceExpression(SwitchToAdd), "Arguments"),
                       new CodePrimitiveExpression(null));

                // Create a CodeConditionStatement that tests a boolean value named boolean.
                CodeConditionStatement conditionalStatement = new CodeConditionStatement(
                    // The condition to test.
                    new CodeVariableReferenceExpression("i >= 0"),
                    // The statements to execute if the condition evaluates to true.
                    new CodeStatement[] { setToolSwitchNameGoodIndex, setArgumentsGoodIndex },
                    // The statements to execute if the condition evalues to false.
                    new CodeStatement[] { setToolSwitchNameBadIndex, setArgumentsBadIndex });

                propertyName.SetStatements.Add(conditionalStatement);
                // Set the separator
                CodeAssignStatement setSeparator = new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        new CodeVariableReferenceExpression(SwitchToAdd), "Separator"),
                        new CodeSnippetExpression(SurroundWithQuotes(property.Separator)));
                propertyName.SetStatements.Add(setSeparator);

                // Set the tool name
                CodeAssignStatement setToolName = new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        new CodeVariableReferenceExpression(SwitchToAdd), NameProperty),
                        new CodeSnippetExpression(SurroundWithQuotes(property.Name)));
                propertyName.SetStatements.Add(setToolName);

                propertyToReceiveValue = ValueProperty;
                GenerateAssignPropertyToValue(propertyName, MultiValues, new CodeSnippetExpression(IsOn));
            }
            else
            {
                CodeAssignStatement setToolName = new CodeAssignStatement(
                    new CodePropertyReferenceExpression(
                        new CodeVariableReferenceExpression(SwitchToAdd), NameProperty),
                        new CodeSnippetExpression(SurroundWithQuotes(property.Name)));
                propertyName.SetStatements.Add(setToolName);

                propertyToReceiveValue = FileNameProperty;
                CodeAssignStatement setToolSwitchName = new CodeAssignStatement(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(SwitchToAdd), SwitchValueProperty), property.SwitchName != String.Empty ? new CodeSnippetExpression(SurroundWithQuotes(property.Prefix + property.SwitchName)) : new CodeSnippetExpression(SurroundWithQuotes("")));
                propertyName.SetStatements.Add(setToolSwitchName);
                GenerateAssignToolSwitch(propertyName, ReverseSwitchValueProperty, property.Prefix, property.ReverseSwitchName);
            }

            GenerateCommonSetStatements(property, propertyName, propertyToReceiveValue);
        }

        /// <summary>
        /// Returns true if the property refers to the current platform.
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        private bool ContainsCurrentPlatform(Property property)
        {
            if (Platform == null)
                return true;

            if (property.Values.Count > 0)
            {
                bool containsCurrentPlatform = false;
                foreach (Value val in property.Values)
                {
                    containsCurrentPlatform = ContainsCurrentPlatform(val.SwitchName) ? true : containsCurrentPlatform;
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
                                        new CodeExpression[] { new CodeObjectCreateExpression(
                                            new CodeTypeReference(TypeKeyValuePairStrings), new CodeExpression[] {
                                                new CodeSnippetExpression(SurroundWithQuotes(rel.SwitchValue)),
                                                new CodeSnippetExpression(SurroundWithQuotes(overrided))})}));
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
                                            new CodeExpression[] {  new CodeObjectCreateExpression(
                                                new CodeTypeReference(TypeKeyValuePairStrings), new CodeExpression[] {
                                                    new CodeSnippetExpression(SurroundWithQuotes(rel.SwitchValue)) ,
                                                    new CodeSnippetExpression(SurroundWithQuotes(overrided))})}));
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
            CodeTypeReference ctr = new CodeTypeReference();
            ctr.BaseType = "System.String";
            ctr.ArrayRank = 1;
            GenerateCommon(property, propertyName, TypeStringArray, typeof(Array), StringListProperty);
            propertyName.Type = ctr;
            GenerateAssignToolSwitch(propertyName, SwitchValueProperty, property.Prefix, property.SwitchName);
            CodeAssignStatement setToolName = new CodeAssignStatement(
                   new CodePropertyReferenceExpression(
                       new CodeVariableReferenceExpression(SwitchToAdd), NameProperty),
                       new CodeSnippetExpression(SurroundWithQuotes(property.Name)));
            propertyName.SetStatements.Add(setToolName);
            GenerateCommonSetStatements(property, propertyName, StringListProperty);
        }

        /// <summary>
        /// This method generates the property that returns the tool exe value set by the ToolExe property
        /// </summary>
        private void GenerateToolNameProperty(CodeTypeDeclaration taskClass)
        {
            CodeMemberProperty toolNameAccessor = new CodeMemberProperty();
            toolNameAccessor.Name = ToolNamePropertyName;
            toolNameAccessor.HasGet = true;
            toolNameAccessor.HasSet = false;
            toolNameAccessor.Attributes = MemberAttributes.Override | MemberAttributes.Family;
            toolNameAccessor.Type = new CodeTypeReference(typeof(string));

            string commentContent = null;

            if (GenerateComments)
            {
                // Comment on this property assignment
                commentContent = ResourceUtilities.FormatResourceString("ToolExeFieldDescription");
                toolNameAccessor.GetStatements.Add(new CodeCommentStatement(commentContent, false));
            }

            toolNameAccessor.GetStatements.Add(new CodeMethodReturnStatement(new CodeSnippetExpression(SurroundWithQuotes(_taskParser.ToolName))));
            taskClass.Members.Add(toolNameAccessor);

            if (GenerateComments)
            {
                // comments
                toolNameAccessor.Comments.Add(new CodeCommentStatement(ResourceUtilities.FormatResourceString("StartSummary"), true));
                commentContent = ResourceUtilities.FormatResourceString("ToolNameDescription");
                toolNameAccessor.Comments.Add(new CodeCommentStatement(commentContent, true));
                toolNameAccessor.Comments.Add(new CodeCommentStatement(ResourceUtilities.FormatResourceString("EndSummary"), true));
            }
        }

        /// <summary>
        /// This method generates the code that appears at the top of each class (that imports other libraries)
        /// </summary>
        private void GenerateImports(CodeNamespace codeNamespace)
        {
            string[] imports = new string[]
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
            CodeConstructor defaultConstructor = new CodeConstructor();
            defaultConstructor.Attributes = MemberAttributes.Public;

            // new System.Resources.ResourceManager("Microsoft.Build.NativeTasks.Strings", System.Reflection.Assembly.GetExecutingAssembly()))
            CodeTypeReference resourceManagerType = new CodeTypeReference("System.Resources.ResourceManager");
            CodeSnippetExpression resourceNamespaceString = new CodeSnippetExpression(SurroundWithQuotes(_taskParser.ResourceNamespace));
            CodeTypeReferenceExpression systemReflectionAssembly = new CodeTypeReferenceExpression("System.Reflection.Assembly");
            CodeMethodReferenceExpression getExecutingAssemblyReference = new CodeMethodReferenceExpression(systemReflectionAssembly, "GetExecutingAssembly");
            CodeMethodInvokeExpression getExecutingAssembly = new CodeMethodInvokeExpression(getExecutingAssemblyReference);
            CodeObjectCreateExpression resourceManager = new CodeObjectCreateExpression(resourceManagerType, new CodeExpression[] { resourceNamespaceString, getExecutingAssembly });

            CodeTypeReference switchOrderArrayType = new CodeTypeReference(new CodeTypeReference("System.String"), 1);
            List<CodeExpression> valueExpressions = new List<CodeExpression>();
            foreach (string switchName in _taskParser.SwitchOrderList)
            {
                valueExpressions.Add(new CodeSnippetExpression(SurroundWithQuotes(switchName)));
            }

            CodeArrayCreateExpression arrayExpression = new CodeArrayCreateExpression(switchOrderArrayType, valueExpressions.ToArray());
            defaultConstructor.BaseConstructorArgs.Add(arrayExpression);
            defaultConstructor.BaseConstructorArgs.Add(resourceManager);

            taskClass.Members.Add(defaultConstructor);

            if (GenerateComments)
            {
                // comments
                defaultConstructor.Comments.Add(new CodeCommentStatement(ResourceUtilities.FormatResourceString("StartSummary"), true));
                string commentContent = ResourceUtilities.FormatResourceString("ConstructorDescription");
                defaultConstructor.Comments.Add(new CodeCommentStatement(commentContent, true));
                defaultConstructor.Comments.Add(new CodeCommentStatement(ResourceUtilities.FormatResourceString("EndSummary"), true));
            }
        }

        /// <summary>
        /// This method generates the relations which will be used at runtime to validate the command line
        /// </summary>
        private void GenerateRelations(CodeTypeDeclaration taskClass)
        {
            if (_relationsParser.SwitchRelationsList.Count > 0)
            {
                CodeMemberMethod addValidateRelationsMethod = new CodeMemberMethod();
                addValidateRelationsMethod.Name = ValidateRelationsMethod;
                addValidateRelationsMethod.Attributes = MemberAttributes.Family | MemberAttributes.Override;

                foreach (KeyValuePair<string, SwitchRelations> switchRelations in _relationsParser.SwitchRelationsList)
                {
                    if (switchRelations.Value.Requires.Count > 0)
                    {
                        CodeConditionStatement checkRequired = new CodeConditionStatement();

                        checkRequired.Condition = null;

                        foreach (string required in switchRelations.Value.Requires)
                        {
                            if (checkRequired.Condition != null)
                                checkRequired.Condition = new CodeBinaryOperatorExpression(
                                checkRequired.Condition, CodeBinaryOperatorType.BooleanAnd, new CodeBinaryOperatorExpression(new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), IsSwitchValueSetMethod), new CodeSnippetExpression(SurroundWithQuotes(required))), CodeBinaryOperatorType.IdentityEquality, new CodeSnippetExpression("false")));
                            else
                                checkRequired.Condition = new CodeBinaryOperatorExpression(new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), IsSwitchValueSetMethod), new CodeSnippetExpression(SurroundWithQuotes(required))), CodeBinaryOperatorType.IdentityEquality, new CodeSnippetExpression("false"));
                        }

                        checkRequired.TrueStatements.Add(new CodeMethodInvokeExpression
                            (new CodeThisReferenceExpression(), "RemoveSwitchToolBasedOnValue",
                              new CodeExpression[]{new CodeSnippetExpression(SurroundWithQuotes(switchRelations.Key)),
                              }));

                        addValidateRelationsMethod.Statements.Add(checkRequired);
                    }
                }

                taskClass.Members.Add(addValidateRelationsMethod);

                if (GenerateComments)
                {
                    // comments
                    addValidateRelationsMethod.Comments.Add(new CodeCommentStatement(ResourceUtilities.FormatResourceString("StartSummary"), true));
                    string commentContent = ResourceUtilities.FormatResourceString("AddValidateRelationsMethod");
                    addValidateRelationsMethod.Comments.Add(new CodeCommentStatement(commentContent, true));
                    addValidateRelationsMethod.Comments.Add(new CodeCommentStatement(ResourceUtilities.FormatResourceString("EndSummary"), true));
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
            _errorLog.AddLast(ResourceUtilities.FormatResourceString(messageResourceName, messageArgs));
            _errorCount++;
        }

        /// <summary>
        /// Puts a string inside two quotes
        /// </summary>
        private string SurroundWithQuotes(string unformattedText)
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
        internal int ErrorCount
        {
            get
            {
                return _errorCount;
            }
        }

        /// <summary>
        /// Returns the log of errors
        /// </summary>
        internal LinkedList<string> ErrorLog
        {
            get
            {
                return _errorLog;
            }
        }
    }
}
