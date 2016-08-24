// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Generates a temporary code file with the specified generated code fragment.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Globalization;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Security;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Generates a temporary code file with the specified generated code fragment.
    /// Does not delete the file.
    /// </summary>
    /// <comment>
    /// Currently only supports writing .NET attributes.
    /// </comment>
    public class WriteCodeFragment : TaskExtension
    {
        /// <summary>
        /// Language of code to generate.
        /// Language name can be any language for which a CodeDom provider is
        /// available. For example, "C#", "VisualBasic".
        /// Emitted file will have the default extension for that language.
        /// </summary>
        [Required]
        public string Language
        {
            get;
            set;
        }

        /// <summary>
        /// Description of attributes to write.
        /// Item include is the full type name of the attribute.
        /// For example, "System.AssemblyVersionAttribute".
        /// Each piece of metadata is the name-value pair of a parameter, which must be of type System.String.
        /// Some attributes only allow positional constructor arguments, or the user may just prefer them.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// If a parameter index is skipped, it's an error.
        /// </summary>
        public ITaskItem[] AssemblyAttributes
        {
            get;
            set;
        }

        /// <summary>
        /// Destination folder for the generated code.
        /// Typically the intermediate folder.
        /// </summary>
        public ITaskItem OutputDirectory
        {
            get;
            set;
        }

        /// <summary>
        /// The path to the file that was generated.
        /// If this is set, and a file name, the destination folder will be prepended.
        /// If this is set, and is rooted, the destination folder will be ignored.
        /// If this is not set, the destination folder will be used, an arbitrary file name will be used, and 
        /// the default extension for the language selected.
        /// </summary>
        [Output]
        public ITaskItem OutputFile
        {
            get;
            set;
        }

        /// <summary>
        /// Main entry point.
        /// </summary>
        public override bool Execute()
        {
            if (String.IsNullOrEmpty(Language))
            {
                Log.LogErrorWithCodeFromResources("General.InvalidValue", "Language", "WriteCodeFragment");
                return false;
            }

            if (OutputFile == null && OutputDirectory == null)
            {
                Log.LogErrorWithCodeFromResources("WriteCodeFragment.MustSpecifyLocation");
                return false;
            }

            string extension;

#if FEATURE_CODEDOM
            var code = GenerateCode(out extension);
#else
            var code = GenerateCodeCoreClr(out extension);
#endif

            if (Log.HasLoggedErrors)
            {
                return false;
            }

            if (code.Length == 0)
            {
                Log.LogMessageFromResources(MessageImportance.Low, "WriteCodeFragment.NoWorkToDo");
                OutputFile = null;
                return true;
            }

            try
            {
                if (OutputFile != null && OutputDirectory != null && !Path.IsPathRooted(OutputFile.ItemSpec))
                {
                    OutputFile = new TaskItem(Path.Combine(OutputDirectory.ItemSpec, OutputFile.ItemSpec));
                }

                OutputFile = OutputFile ?? new TaskItem(FileUtilities.GetTemporaryFile(OutputDirectory.ItemSpec, extension));

                File.WriteAllText(OutputFile.ItemSpec, code); // Overwrites file if it already exists (and can be overwritten)
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
                Log.LogErrorWithCodeFromResources("WriteCodeFragment.CouldNotWriteOutput", (OutputFile == null) ? String.Empty : OutputFile.ItemSpec, ex.Message);
                return false;
            }

            Log.LogMessageFromResources(MessageImportance.Low, "WriteCodeFragment.GeneratedFile", OutputFile.ItemSpec);

            return !Log.HasLoggedErrors;
        }

#if FEATURE_CODEDOM
        /// <summary>
        /// Generates the code into a string.
        /// If it fails, logs an error and returns null.
        /// If no meaningful code is generated, returns empty string.
        /// Returns the default language extension as an out parameter.
        /// </summary>
        [SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider", MessageId = "System.IO.StringWriter.#ctor(System.Text.StringBuilder)", Justification = "Reads fine to me")]
        private string GenerateCode(out string extension)
        {
            extension = null;
            bool haveGeneratedContent = false;

            CodeDomProvider provider;

            try
            {
                provider = CodeDomProvider.CreateProvider(Language);
            }
            catch (System.Configuration.ConfigurationException ex)
            {
                Log.LogErrorWithCodeFromResources("WriteCodeFragment.CouldNotCreateProvider", Language, ex.Message);
                return null;
            }
            catch (SecurityException ex)
            {
                Log.LogErrorWithCodeFromResources("WriteCodeFragment.CouldNotCreateProvider", Language, ex.Message);
                return null;
            }

            extension = provider.FileExtension;

            CodeCompileUnit unit = new CodeCompileUnit();

            CodeNamespace globalNamespace = new CodeNamespace();
            unit.Namespaces.Add(globalNamespace);

            // Declare authorship. Unfortunately CodeDOM puts this comment after the attributes.
            string comment = ResourceUtilities.FormatResourceString("WriteCodeFragment.Comment");
            globalNamespace.Comments.Add(new CodeCommentStatement(comment));

            if (AssemblyAttributes == null)
            {
                return String.Empty;
            }

            // For convenience, bring in the namespaces, where many assembly attributes lie
            globalNamespace.Imports.Add(new CodeNamespaceImport("System"));
            globalNamespace.Imports.Add(new CodeNamespaceImport("System.Reflection"));

            foreach (ITaskItem attributeItem in AssemblyAttributes)
            {
                CodeAttributeDeclaration attribute = new CodeAttributeDeclaration(new CodeTypeReference(attributeItem.ItemSpec));

                // Some attributes only allow positional constructor arguments, or the user may just prefer them.
                // To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
                // If a parameter index is skipped, it's an error.
                IDictionary customMetadata = attributeItem.CloneCustomMetadata();

                List<CodeAttributeArgument> orderedParameters = new List<CodeAttributeArgument>(new CodeAttributeArgument[customMetadata.Count + 1] /* max possible slots needed */);
                List<CodeAttributeArgument> namedParameters = new List<CodeAttributeArgument>();

                foreach (DictionaryEntry entry in customMetadata)
                {
                    string name = (string)entry.Key;
                    string value = (string)entry.Value;

                    if (name.StartsWith("_Parameter", StringComparison.OrdinalIgnoreCase))
                    {
                        int index;

                        if (!Int32.TryParse(name.Substring("_Parameter".Length), out index))
                        {
                            Log.LogErrorWithCodeFromResources("General.InvalidValue", name, "WriteCodeFragment");
                            return null;
                        }

                        if (index > orderedParameters.Count || index < 1)
                        {
                            Log.LogErrorWithCodeFromResources("WriteCodeFragment.SkippedNumberedParameter", index);
                            return null;
                        }

                        // "_Parameter01" and "_Parameter1" would overwrite each other
                        orderedParameters[index - 1] = new CodeAttributeArgument(String.Empty, new CodePrimitiveExpression(value));
                    }
                    else
                    {
                        namedParameters.Add(new CodeAttributeArgument(name, new CodePrimitiveExpression(value)));
                    }
                }

                bool encounteredNull = false;
                for (int i = 0; i < orderedParameters.Count; i++)
                {
                    if (orderedParameters[i] == null)
                    {
                        // All subsequent args should be null, else a slot was missed
                        encounteredNull = true;
                        continue;
                    }

                    if (encounteredNull)
                    {
                        Log.LogErrorWithCodeFromResources("WriteCodeFragment.SkippedNumberedParameter", i + 1 /* back to 1 based */);
                        return null;
                    }

                    attribute.Arguments.Add(orderedParameters[i]);
                }

                foreach (CodeAttributeArgument namedParameter in namedParameters)
                {
                    attribute.Arguments.Add(namedParameter);
                }

                unit.AssemblyCustomAttributes.Add(attribute);
                haveGeneratedContent = true;
            }

            StringBuilder generatedCode = new StringBuilder();

            using (StringWriter writer = new StringWriter(generatedCode, CultureInfo.CurrentCulture))
            {
                provider.GenerateCodeFromCompileUnit(unit, writer, new CodeGeneratorOptions());
            }

            string code = generatedCode.ToString();

            // If we just generated infrastructure, don't bother returning anything
            // as there's no point writing the file
            return haveGeneratedContent ? code : String.Empty;
        }
#endif

        /// <summary>
        /// Generates the code into a string.
        /// If it fails, logs an error and returns null.
        /// If no meaningful code is generated, returns empty string.
        /// Returns the default language extension as an out parameter.
        /// </summary>
        private string GenerateCodeCoreClr(out string extension)
        {
            extension = null;
            bool haveGeneratedContent = false;

            StringBuilder code = new StringBuilder();
            switch (Language.ToLowerInvariant())
            {
                case "c#":
                    if (AssemblyAttributes == null) return string.Empty; 

                    extension = "cs";
                    code.AppendLine("// " + ResourceUtilities.FormatResourceString("WriteCodeFragment.Comment"));
                    code.AppendLine();
                    code.AppendLine("using System;");
                    code.AppendLine("using System.Reflection;");
                    code.AppendLine();

                    foreach (ITaskItem attributeItem in AssemblyAttributes)
                    {
                        string args = GetAttributeArguments(attributeItem, "=");
                        if (args == null) return null;

                        code.AppendLine(string.Format($"[assembly: {attributeItem.ItemSpec}({args})]"));
                        haveGeneratedContent = true;
                    }

                    break;
                case "visual basic":
                case "visualbasic":
                    if (AssemblyAttributes == null) return string.Empty;

                    extension = "vb";
                    code.AppendLine("' " + ResourceUtilities.FormatResourceString("WriteCodeFragment.Comment", DateTime.Now));
                    code.AppendLine();
                    code.AppendLine("Option Strict Off");
                    code.AppendLine("Option Explicit On");
                    code.AppendLine();
                    code.AppendLine("Imports System");
                    code.AppendLine("Imports System.Reflection");

                    foreach (ITaskItem attributeItem in AssemblyAttributes)
                    {
                        string args = GetAttributeArguments(attributeItem, ":=");
                        if (args == null) return null;

                        code.AppendLine(string.Format($"<Assembly: {attributeItem.ItemSpec}({args})>"));
                        haveGeneratedContent = true;
                    }
                    break;
                default:
                    Log.LogErrorWithCodeFromResources("WriteCodeFragment.CouldNotCreateProvider", Language, string.Empty);
                    return null;
            }

            // If we just generated infrastructure, don't bother returning anything
            // as there's no point writing the file
            return haveGeneratedContent ? code.ToString() : string.Empty; 
        }

        private string GetAttributeArguments(ITaskItem attributeItem, string namedArgumentString)
        {
            // Some attributes only allow positional constructor arguments, or the user may just prefer them.
            // To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
            // If a parameter index is skipped, it's an error.
            IDictionary customMetadata = attributeItem.CloneCustomMetadata();
            
            // Initialize count + 1 to access starting at 1
            List<string> orderedParameters = new List<string>(new string[customMetadata.Count + 1]);
            List<string> namedParameters = new List<string>();

            foreach (DictionaryEntry entry in customMetadata)
            {
                string name = (string) entry.Key;
                string value = entry.Value is string ? $@"""{entry.Value}""" : entry.Value.ToString();

                if (name.StartsWith("_Parameter", StringComparison.OrdinalIgnoreCase))
                {
                    int index;

                    if (!int.TryParse(name.Substring("_Parameter".Length), out index))
                    {
                        Log.LogErrorWithCodeFromResources("General.InvalidValue", name, "WriteCodeFragment");
                        return null;
                    }

                    if (index > orderedParameters.Count || index < 1)
                    {
                        Log.LogErrorWithCodeFromResources("WriteCodeFragment.SkippedNumberedParameter", index);
                        return null;
                    }

                    // "_Parameter01" and "_Parameter1" would overwrite each other
                    orderedParameters[index - 1] = value;
                }
                else
                {
                    namedParameters.Add($"{name}{namedArgumentString}{value}");
                }
            }

            bool encounteredNull = false;
            
            for (int i = 0; i < orderedParameters.Count; i++)
            {
                if (orderedParameters[i] == null)
                {
                    // All subsequent args should be null, else a slot was missed
                    encounteredNull = true;
                    continue;
                }

                if (encounteredNull)
                {
                    Log.LogErrorWithCodeFromResources("WriteCodeFragment.SkippedNumberedParameter", i + 1 /* back to 1 based */);
                    return null;
                }
            }

            return string.Join(", ", orderedParameters.Union(namedParameters).Where(p => !string.IsNullOrWhiteSpace(p)));
        }
    }
}
