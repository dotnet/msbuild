// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Generates a temporary code file with the specified generated code fragment.</summary>
//-----------------------------------------------------------------------

using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
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

#if FEATURE_CODEDOM
            var code = GenerateCode(out string extension);
#else
            var code = GenerateCodeCoreClr(out string extension);
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

            var code = new StringBuilder();
            switch (Language.ToLowerInvariant())
            {
                case "c#":
                    if (AssemblyAttributes == null) return string.Empty; 

                    extension = "cs";
                    code.AppendLine("//------------------------------------------------------------------------------");
                    code.AppendLine("// <auto-generated>");
                    code.AppendLine("//     " + ResourceUtilities.GetResourceString("WriteCodeFragment.Comment"));
                    code.AppendLine("// </auto-generated>");
                    code.AppendLine("//------------------------------------------------------------------------------");
                    code.AppendLine();
                    code.AppendLine("using System;");
                    code.AppendLine("using System.Reflection;");
                    code.AppendLine();

                    foreach (ITaskItem attributeItem in AssemblyAttributes)
                    {
                        string args = GetAttributeArguments(attributeItem, "=", QuoteSnippetStringCSharp);
                        if (args == null) return null;

                        code.AppendLine(string.Format($"[assembly: {attributeItem.ItemSpec}({args})]"));
                        haveGeneratedContent = true;
                    }

                    break;
                case "visual basic":
                case "visualbasic":
                case "vb":
                    if (AssemblyAttributes == null) return string.Empty;

                    extension = "vb";
                    code.AppendLine("'------------------------------------------------------------------------------");
                    code.AppendLine("' <auto-generated>");
                    code.AppendLine("'     " + ResourceUtilities.GetResourceString("WriteCodeFragment.Comment"));
                    code.AppendLine("' </auto-generated>");
                    code.AppendLine("'------------------------------------------------------------------------------");
                    code.AppendLine();
                    code.AppendLine("Option Strict Off");
                    code.AppendLine("Option Explicit On");
                    code.AppendLine();
                    code.AppendLine("Imports System");
                    code.AppendLine("Imports System.Reflection");

                    foreach (ITaskItem attributeItem in AssemblyAttributes)
                    {
                        string args = GetAttributeArguments(attributeItem, ":=", QuoteSnippetStringVisualBasic);
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

        private string GetAttributeArguments(ITaskItem attributeItem, string namedArgumentString, Func<string, string> quoteString)
        {
            // Some attributes only allow positional constructor arguments, or the user may just prefer them.
            // To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
            // If a parameter index is skipped, it's an error.
            IDictionary customMetadata = attributeItem.CloneCustomMetadata();
            
            // Initialize count + 1 to access starting at 1
            var orderedParameters = new List<string>(new string[customMetadata.Count + 1]);
            var namedParameters = new List<string>();

            foreach (DictionaryEntry entry in customMetadata)
            {
                string name = (string) entry.Key;
                string value = entry.Value is string ? quoteString(entry.Value.ToString()) : entry.Value.ToString();

                if (name.StartsWith("_Parameter", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(name.Substring("_Parameter".Length), out int index))
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

        private const int MaxLineLength = 80;

        // copied from Microsoft.CSharp.CSharpCodeProvider
        private static string QuoteSnippetStringCSharp(string value)
        {
            // If the string is short, use C style quoting (e.g "\r\n")
            // Also do it if it is too long to fit in one line
            // If the string contains '\0', verbatim style won't work.
            if (value.Length < 256 || value.Length > 1500 || (value.IndexOf('\0') != -1))
                return QuoteSnippetStringCStyle(value);

            // Otherwise, use 'verbatim' style quoting (e.g. @"foo")
            return QuoteSnippetStringVerbatimStyle(value);
        }

        // copied from Microsoft.CSharp.CSharpCodeProvider
        private static string QuoteSnippetStringCStyle(string value)
        {
            var b = new StringBuilder(value.Length + 5);

            b.Append("\"");

            int i = 0;
            while (i < value.Length)
            {
                switch (value[i])
                {
                    case '\r':
                        b.Append("\\r");
                        break;
                    case '\t':
                        b.Append("\\t");
                        break;
                    case '\"':
                        b.Append("\\\"");
                        break;
                    case '\'':
                        b.Append("\\\'");
                        break;
                    case '\\':
                        b.Append("\\\\");
                        break;
                    case '\0':
                        b.Append("\\0");
                        break;
                    case '\n':
                        b.Append("\\n");
                        break;
                    case '\u2028':
                    case '\u2029':
                        b.Append("\\n");
                        break;

                    default:
                        b.Append(value[i]);
                        break;
                }

                if (i > 0 && i%MaxLineLength == 0)
                {
                    //
                    // If current character is a high surrogate and the following 
                    // character is a low surrogate, don't break them. 
                    // Otherwise when we write the string to a file, we might lose 
                    // the characters.
                    // 
                    if (Char.IsHighSurrogate(value[i])
                        && (i < value.Length - 1)
                        && Char.IsLowSurrogate(value[i + 1]))
                    {
                        b.Append(value[++i]);
                    }

                    b.Append("\" +");
                    b.Append(Environment.NewLine);
                    b.Append('\"');
                }
                ++i;
            }

            b.Append("\"");

            return b.ToString();
        }

        // copied from Microsoft.CSharp.CSharpCodeProvider
        private static string QuoteSnippetStringVerbatimStyle(string value)
        {
            var b = new StringBuilder(value.Length + 5);

            b.Append("@\"");

            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\"')
                    b.Append("\"\"");
                else
                    b.Append(value[i]);
            }

            b.Append("\"");

            return b.ToString();
        }

        // copied from Microsoft.VisualBasic.VBCodeProvider
        private static string QuoteSnippetStringVisualBasic(string value)
        {
            var b = new StringBuilder(value.Length + 5);

            bool fInDoubleQuotes = true;

            b.Append("\"");

            int i = 0;
            while (i < value.Length)
            {
                char ch = value[i];
                switch (ch)
                {
                    case '\"':
                    // These are the inward sloping quotes used by default in some cultures like CHS. 
                    // VBC.EXE does a mapping ANSI that results in it treating these as syntactically equivalent to a
                    // regular double quote.
                    case '\u201C':
                    case '\u201D':
                    case '\uFF02':
                        EnsureInDoubleQuotes(ref fInDoubleQuotes, b);
                        b.Append(ch);
                        b.Append(ch);
                        break;
                    case '\r':
                        EnsureNotInDoubleQuotes(ref fInDoubleQuotes, b);
                        if (i < value.Length - 1 && value[i + 1] == '\n')
                        {
                            b.Append("&Global.Microsoft.VisualBasic.ChrW(13)&Global.Microsoft.VisualBasic.ChrW(10)");
                            i++;
                        }
                        else
                        {
                            b.Append("&Global.Microsoft.VisualBasic.ChrW(13)");
                        }
                        break;
                    case '\t':
                        EnsureNotInDoubleQuotes(ref fInDoubleQuotes, b);
                        b.Append("&Global.Microsoft.VisualBasic.ChrW(9)");
                        break;
                    case '\0':
                        EnsureNotInDoubleQuotes(ref fInDoubleQuotes, b);
                        b.Append("&Global.Microsoft.VisualBasic.ChrW(0)");
                        break;
                    case '\n':
                    case '\u2028':
                    case '\u2029':
                        EnsureNotInDoubleQuotes(ref fInDoubleQuotes, b);
                        b.Append("&Global.Microsoft.VisualBasic.ChrW(10)");
                        break;
                    default:
                        EnsureInDoubleQuotes(ref fInDoubleQuotes, b);
                        b.Append(value[i]);
                        break;
                }

                if (i > 0 && i%MaxLineLength == 0)
                {
                    //
                    // If current character is a high surrogate and the following 
                    // character is a low surrogate, don't break them. 
                    // Otherwise when we write the string to a file, we might lose 
                    // the characters.
                    // 
                    if (Char.IsHighSurrogate(value[i])
                        && (i < value.Length - 1)
                        && Char.IsLowSurrogate(value[i + 1]))
                    {
                        b.Append(value[++i]);
                    }

                    if (fInDoubleQuotes)
                        b.Append("\"");
                    fInDoubleQuotes = true;

                    b.Append("& _ ");
                    b.Append(Environment.NewLine);
                    b.Append('\"');
                }
                ++i;
            }

            if (fInDoubleQuotes)
                b.Append("\"");

            return b.ToString();
        }

        private static void EnsureNotInDoubleQuotes(ref bool fInDoubleQuotes, StringBuilder b)
        {
            if (!fInDoubleQuotes) return;
            b.Append("\"");
            fInDoubleQuotes = false;
        }

        private static void EnsureInDoubleQuotes(ref bool fInDoubleQuotes, StringBuilder b)
        {
            if (fInDoubleQuotes) return;
            b.Append("&\"");
            fInDoubleQuotes = true;
        }
    }
}
