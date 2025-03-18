// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
#if FEATURE_SYSTEM_CONFIGURATION
using System.Configuration;
using System.Security;
#endif
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

#nullable disable

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
        private const string TypeNameSuffix = "_TypeName";
        private const string IsLiteralSuffix = "_IsLiteral";
        private static readonly string[] NamespaceImports = ["System", "System.Reflection"];
        private static readonly IReadOnlyDictionary<string, ParameterType> EmptyParameterTypes = new Dictionary<string, ParameterType>();

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

                OutputFile ??= new TaskItem(FileUtilities.GetTemporaryFile(OutputDirectory.ItemSpec, null, extension));

                FileUtilities.EnsureDirectoryExists(Path.GetDirectoryName(OutputFile.ItemSpec));

                File.WriteAllText(OutputFile.ItemSpec, code); // Overwrites file if it already exists (and can be overwritten)
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
                string itemSpec = OutputFile?.ItemSpec ?? String.Empty;
                string lockedFileMessage = LockCheck.GetLockedFileMessage(itemSpec);
                Log.LogErrorWithCodeFromResources("WriteCodeFragment.CouldNotWriteOutput", itemSpec, ex.Message, lockedFileMessage);
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

            CodeDomProvider provider = null;
            try
            {
                try
                {
                    provider = CodeDomProvider.CreateProvider(Language);
                }
                catch (SystemException e) when
#if FEATURE_SYSTEM_CONFIGURATION
                (e is ConfigurationException || e is SecurityException)
#else
            (e.GetType().Name == "ConfigurationErrorsException") // TODO: catch specific exception type once it is public https://github.com/dotnet/corefx/issues/40456
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
                foreach (string name in NamespaceImports)
                {
                    globalNamespace.Imports.Add(new CodeNamespaceImport(name));
                }

                foreach (ITaskItem attributeItem in AssemblyAttributes)
                {
                    // Some attributes only allow positional constructor arguments, or the user may just prefer them.
                    // To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
                    // If a parameter index is skipped, it's an error.
                    IDictionary customMetadata = attributeItem.CloneCustomMetadata();

                    // Some metadata may indicate the types of parameters. Use that metadata to determine
                    // the parameter types. Those metadata items will be removed from the dictionary.
                    IReadOnlyDictionary<string, ParameterType> parameterTypes = ExtractParameterTypes(customMetadata);

                    var orderedParameters = new List<AttributeParameter?>(new AttributeParameter?[customMetadata.Count + 1] /* max possible slots needed */);
                    var namedParameters = new List<AttributeParameter>();

                    foreach (DictionaryEntry entry in customMetadata)
                    {
                        string name = (string)entry.Key;
                        string value = (string)entry.Value;

                        // Get the declared type information for this parameter.
                        // If a type is not declared, then we infer the type.
                        if (!parameterTypes.TryGetValue(name, out ParameterType type))
                        {
                            type = new ParameterType { Kind = ParameterTypeKind.Inferred };
                        }

                        if (name.StartsWith("_Parameter", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!Int32.TryParse(
#if NET
                                name.AsSpan("_Parameter".Length),
#else
                                name.Substring("_Parameter".Length),
#endif
                                out int index))
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
                            orderedParameters[index - 1] = new AttributeParameter { Type = type, Value = value };
                        }
                        else
                        {
                            namedParameters.Add(new AttributeParameter { Name = name, Type = type, Value = value });
                        }
                    }

                    bool encounteredNull = false;
                    List<AttributeParameter> providedOrderedParameters = new();
                    for (int i = 0; i < orderedParameters.Count; i++)
                    {
                        if (!orderedParameters[i].HasValue)
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

                        providedOrderedParameters.Add(orderedParameters[i].Value);
                    }

                    var attribute = new CodeAttributeDeclaration(new CodeTypeReference(attributeItem.ItemSpec));

                    // We might need the type of the attribute if we need to infer the
                    // types of the parameters. Search for it by the given type name,
                    // as well as within the namespaces that we automatically import.
                    Lazy<Type> attributeType = new(
                        () => Type.GetType(attribute.Name, throwOnError: false) ?? NamespaceImports.Select(x => Type.GetType($"{x}.{attribute.Name}", throwOnError: false)).FirstOrDefault(),
                        System.Threading.LazyThreadSafetyMode.None);

                    if (
                        !AddArguments(attribute, attributeType, providedOrderedParameters, isPositional: true)
                        || !AddArguments(attribute, attributeType, namedParameters, isPositional: false))
                    {
                        return null;
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
            finally
            {
                provider?.Dispose();
            }
        }

        /// <summary>
        /// Finds the metadata items that are used to indicate the types of
        /// parameters, and removes those items from the given dictionary.
        /// Returns a dictionary that maps parameter names to their declared types.
        /// </summary>
        private IReadOnlyDictionary<string, ParameterType> ExtractParameterTypes(IDictionary customMetadata)
        {
            Dictionary<string, ParameterType> parameterTypes = null;
            List<string> keysToRemove = null;

            foreach (DictionaryEntry entry in customMetadata)
            {
                string key = (string)entry.Key;
                string value = (string)entry.Value;

                if (key.EndsWith(TypeNameSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    // Remove the suffix to get the corresponding parameter name.
                    var parameterNameKey = key.Substring(0, key.Length - TypeNameSuffix.Length);

                    // To remain as backward-compatible as possible, we will only treat this metadata
                    // item as a type name if there's a corresponding metadata item for the parameter.
                    // This is done to avoid the very small chance of treating "Foo_TypeName" as a
                    // type indicator when it was previously being used as a named attribute parameter.
                    if (customMetadata.Contains(parameterNameKey))
                    {
                        // Delay-create the collections to avoid allocations
                        // when no parameter types are specified.
                        if (parameterTypes == null)
                        {
                            parameterTypes = new();
                            keysToRemove = new();
                        }

                        // Remove this metadata item so that
                        // we don't use it as a parameter name.
                        keysToRemove.Add(key);

                        // The parameter will have an explicit type. The metadata value is the type name.
                        parameterTypes[parameterNameKey] = new ParameterType
                        {
                            Kind = ParameterTypeKind.Typed,
                            TypeName = value
                        };
                    }
                }
                else if (key.EndsWith(IsLiteralSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    // Remove the suffix to get the corresponding parameter name.
                    var parameterNameKey = key.Substring(0, key.Length - IsLiteralSuffix.Length);

                    // As mentioned above for the type name metadata, we will only treat
                    // this metadata item as a literal flag if there's a corresponding
                    // metadata item for the parameter for backward-compatibility reasons.
                    if (customMetadata.Contains(parameterNameKey))
                    {
                        // Delay-create the collections to avoid allocations
                        // when no parameter types are specified.
                        if (parameterTypes == null)
                        {
                            parameterTypes = new();
                            keysToRemove = new();
                        }

                        // Remove this metadata item so that
                        // we don't use it as a parameter name.
                        keysToRemove.Add(key);

                        // If the value is true, the parameter value will be the exact code
                        // that needs to be written to the generated file for that parameter.
                        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                        {
                            parameterTypes[parameterNameKey] = new ParameterType
                            {
                                Kind = ParameterTypeKind.Literal
                            };
                        }
                    }
                }
            }

            // Remove any metadata items that we used
            // for type names or literal flags.
            if (keysToRemove != null)
            {
                foreach (var key in keysToRemove)
                {
                    customMetadata.Remove(key);
                }
            }

            return parameterTypes ?? EmptyParameterTypes;
        }

        /// <summary>
        /// Uses the given parameters to add CodeDom arguments to the given attribute.
        /// Returns true if the arguments could be defined, or false if the values could
        /// not be converted to the required type. An error is also logged for failures.
        /// </summary>
        private bool AddArguments(
            CodeAttributeDeclaration attribute,
            Lazy<Type> attributeType,
            IReadOnlyList<AttributeParameter> parameters,
            bool isPositional)
        {
            Type[] constructorParameterTypes = null;

            for (int i = 0; i < parameters.Count; i++)
            {
                AttributeParameter parameter = parameters[i];
                CodeExpression value;

                switch (parameter.Type.Kind)
                {
                    case ParameterTypeKind.Literal:
                        // The exact value provided by the metadata is what we use.
                        // Note that this value is used verbatim, so its the user's
                        // responsibility to ensure that it is in the correct language.
                        value = new CodeSnippetExpression(parameter.Value);
                        break;

                    case ParameterTypeKind.Typed:
                        if (string.Equals(parameter.Type.TypeName, "System.Type"))
                        {
                            // Types are a special case, because we can't convert a string to a
                            // type, but because we're using the CodeDom, we don't need to
                            // convert it. we can just create a type expression.
                            value = new CodeTypeOfExpression(parameter.Value);
                        }
                        else
                        {
                            // We've been told what type this parameter needs to be.
                            // If we cannot convert the value to that type, then we need to fail.
                            if (!TryConvertParameterValue(parameter.Type.TypeName, parameter.Value, out value))
                            {
                                return false;
                            }
                        }

                        break;

                    default:
                        if (isPositional)
                        {
                            // For positional parameters, infer the type
                            // using the constructor argument types.
                            if (constructorParameterTypes is null)
                            {
                                constructorParameterTypes = FindPositionalParameterTypes(attributeType.Value, parameters);
                            }

                            value = ConvertParameterValueToInferredType(
                                constructorParameterTypes[i],
                                parameter.Value,
                                $"#{i + 1}"); /* back to 1 based */
                        }
                        else
                        {
                            // For named parameters, use the type of the property if we can find it.
                            value = ConvertParameterValueToInferredType(
                                attributeType.Value?.GetProperty(parameter.Name)?.PropertyType,
                                parameter.Value,
                                parameter.Name);
                        }

                        break;
                }

                attribute.Arguments.Add(new CodeAttributeArgument(parameter.Name, value));
            }

            return true;
        }

        /// <summary>
        /// Finds the types that the parameters are likely to be, by finding a constructor
        /// on the attribute that has the same number of parameters that have been provided.
        /// Returns an array of types with a length equal to the number of positional parameters.
        /// If no suitable constructor is found, the array will contain null types.
        /// </summary>
        private Type[] FindPositionalParameterTypes(Type attributeType, IReadOnlyList<AttributeParameter> positionalParameters)
        {
            // The attribute type might not be known.
            if (attributeType is not null)
            {
                // Find the constructors with the same number
                // of parameters as we will be specifying.
                List<Type[]> candidates = attributeType
                    .GetConstructors()
                    .Select(c => c.GetParameters().Select(p => p.ParameterType).ToArray())
                    .Where(t => t.Length == positionalParameters.Count)
                    .ToList();

                if (candidates.Count == 1)
                {
                    return candidates[0];
                }
                else if (candidates.Count > 1)
                {
                    Log.LogMessageFromResources("WriteCodeFragment.MultipleConstructorsFound");

                    // Before parameter types could be specified, all parameter values were
                    // treated as strings. To be backward-compatible, we need to prefer
                    // the constructor that has all string parameters, if it exists.
                    var allStringParameters = candidates.FirstOrDefault(c => c.All(t => t == typeof(string)));

                    if (allStringParameters is not null)
                    {
                        return allStringParameters;
                    }

                    // There isn't a constructor where all parameters are strings, so we can pick any
                    // of the constructors. This code path is very unlikely to be hit because we can only
                    // infer parameter types for attributes in mscorlib (or System.Private.CoreLib).
                    // The attribute type is loaded using `Type.GetType()`, and when you specify just a
                    // type name and not an assembly-qualified type name, only types in this assembly
                    // or mscorlib will be found.
                    //
                    // There are only about five attributes that would result in this code path being
                    // reached due to those attributes having multiple constructors with the same number
                    // of parameters. For that reason, it's not worth putting too much effort into picking
                    // the best constructor. We will use the simple solution of sorting the constructors
                    // (so that we always pick the same constructor, regardless of the order they are
                    // returned from `Type.GetConstructors()`), and choose the first constructor.
                    return candidates
                        .OrderBy(c => string.Join(",", c.Select(t => t.FullName)))
                        .First();
                }
            }

            // If a matching constructor was not found, or we don't
            // know the attribute type, then return an array of null
            // types to indicate that each parameter type is unknown.
            return positionalParameters.Select(x => default(Type)).ToArray();
        }

        /// <summary>
        /// Attempts to convert the raw value provided in the metadata to the type with the specified name.
        /// Returns true if conversion is successful. An error is logged and false is returned if the conversion fails.
        /// </summary>
        private bool TryConvertParameterValue(string typeName, string rawValue, out CodeExpression value)
        {
            var parameterType = Type.GetType(typeName, throwOnError: false);

            if (parameterType is null)
            {
                Log.LogErrorWithCodeFromResources("WriteCodeFragment.ParameterTypeNotFound", typeName);
                value = null;
                return false;
            }

            try
            {
                value = ConvertToCodeExpression(rawValue, parameterType);
                return true;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                Log.LogErrorWithCodeFromResources("WriteCodeFragment.CouldNotConvertValue", rawValue, typeName, ex.Message);
                value = null;
                return false;
            }
        }

        /// <summary>
        /// Convert the raw value provided in the metadata to the type
        /// that has been inferred based on the parameter position or name.
        /// Returns the converted value as a CodeExpression if successful, or the raw value
        /// as a CodeExpression if conversion fails. No errors are logged if the conversion fails.
        /// </summary>
        private CodeExpression ConvertParameterValueToInferredType(Type inferredType, string rawValue, string parameterName)
        {
            // If we don't know what type the parameter should be, then we
            // can't convert the type. We'll just treat is as a string.
            if (inferredType is null)
            {
                Log.LogMessageFromResources("WriteCodeFragment.CouldNotInferParameterType", parameterName);
                return new CodePrimitiveExpression(rawValue);
            }

            try
            {
                return ConvertToCodeExpression(rawValue, inferredType);
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                // The conversion failed, but since we are inferring the type,
                // we won't fail. We'll just treat the value as a string.
                Log.LogMessageFromResources("WriteCodeFragment.CouldNotConvertToInferredType", parameterName, inferredType.Name, ex.Message);
                return new CodePrimitiveExpression(rawValue);
            }
        }

        /// <summary>
        /// Converts the given value to a CodeExpression object where the value is the specified type.
        /// Returns the CodeExpression if successful, or throws an exception if the conversion fails.
        /// </summary>
        private CodeExpression ConvertToCodeExpression(string value, Type targetType)
        {
            if (targetType == typeof(Type))
            {
                return new CodeTypeOfExpression(value);
            }

            if (targetType.IsEnum)
            {
                return new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(targetType), value);
            }

            return new CodePrimitiveExpression(Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture));
        }

        private enum ParameterTypeKind
        {
            Inferred,
            Typed,
            Literal
        }

        private struct ParameterType
        {
            public ParameterTypeKind Kind { get; init; }
            public string TypeName { get; init; }
        }

        private struct AttributeParameter
        {
            public ParameterType Type { get; init; }
            public string Name { get; init; }
            public string Value { get; init; }
        }
    }
}
