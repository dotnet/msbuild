// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
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
        public string Language { get; set; }

        /// <summary>
        /// Description of attributes to write.
        /// Item include is the full type name of the attribute.
        /// For example, "System.AssemblyVersionAttribute".
        /// Each piece of metadata is the name-value pair of a parameter, which must be of type System.String.
        /// Some attributes only allow positional constructor arguments, or the user may just prefer them.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// If a parameter index is skipped, it's an error.
        /// </summary>
        public ITaskItem[] AssemblyAttributes { get; set; }

        /// <summary>
        /// Destination folder for the generated code.
        /// Typically the intermediate folder.
        /// </summary>
        public ITaskItem OutputDirectory { get; set; }

        /// <summary>
        /// The path to the file that was generated.
        /// If this is set, and a file name, the destination folder will be prepended.
        /// If this is set, and is rooted, the destination folder will be ignored.
        /// If this is not set, the destination folder will be used, an arbitrary file name will be used, and 
        /// the default extension for the language selected.
        /// </summary>
        [Output]
        public ITaskItem OutputFile { get; set; }

        /// <summary>
        /// Main entry point.
        /// </summary>
        public override bool Execute()
        {
            if (String.IsNullOrEmpty(Language))
            {
                Log.LogErrorWithCodeFromResources("General.InvalidValue", nameof(Language), "WriteCodeFragment");
                return false;
            }

            if (OutputFile == null && OutputDirectory == null)
            {
                Log.LogErrorWithCodeFromResources("WriteCodeFragment.MustSpecifyLocation");
                return false;
            }

            var code = GenerateCode(out string extension);

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
            catch (SystemException e) when
#if FEATURE_SYSTEM_CONFIGURATION
            (e is ConfigurationException || e is SecurityException)
#else
            (e.GetType().Name == "ConfigurationErrorsException") //TODO: catch specific exception type once it is public https://github.com/dotnet/corefx/issues/40456
#endif
            {
                Log.LogErrorWithCodeFromResources("WriteCodeFragment.CouldNotCreateProvider", Language, e.Message);
                return null;
            }

            extension = provider.FileExtension;

            var unit = new CodeCompileUnit();

            var globalNamespace = new CodeNamespace();
            unit.Namespaces.Add(globalNamespace);

            // Declare authorship. Unfortunately CodeDOM puts this comment after the attributes.
            string comment = ResourceUtilities.GetResourceString("WriteCodeFragment.Comment");
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
                var attribute = new CodeAttributeDeclaration(new CodeTypeReference(attributeItem.ItemSpec));

                // Some attributes only allow positional constructor arguments, or the user may just prefer them.
                // To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
                // If a parameter index is skipped, it's an error.
                IDictionary customMetadata = attributeItem.CloneCustomMetadata();

                var orderedParameters = new List<CodeAttributeArgument>(new CodeAttributeArgument[customMetadata.Count + 1] /* max possible slots needed */);
                var namedParameters = new List<CodeAttributeArgument>();

                foreach (DictionaryEntry entry in customMetadata)
                {
                    string name = (string)entry.Key;
                    string value = (string)entry.Value;

                    if (name.StartsWith("_Parameter", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Int32.TryParse(name.Substring("_Parameter".Length), out int index))
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

            var generatedCode = new StringBuilder();
            using (var writer = new StringWriter(generatedCode, CultureInfo.CurrentCulture))
            {
                provider.GenerateCodeFromCompileUnit(unit, writer, new CodeGeneratorOptions());
            }

            string code = generatedCode.ToString();

            // If we just generated infrastructure, don't bother returning anything
            // as there's no point writing the file
            return haveGeneratedContent ? code : String.Empty;
        }
    }
}
