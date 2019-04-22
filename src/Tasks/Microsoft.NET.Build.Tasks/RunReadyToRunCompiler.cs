// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public class RunReadyToRunCompiler : ToolTask
    {
        [Required]
        public ITaskItem CrossgenTool { get; set; }
        [Required]
        public ITaskItem CompilationEntry { get; set; }
        [Required]
        public ITaskItem[] ImplementationAssemblies { get; set; }

        private string _crossgenPath;
        private string _clrjitPath;
        private string _diasymreaderPath;

        private string _inputAssembly;
        private string _outputR2RImage;
        private string _outputPDBImage;
        private string _createPDBCommand;

        private bool IsPdbCompilation => !String.IsNullOrEmpty(_createPDBCommand);

        protected override string ToolName => _crossgenPath;

        protected override string GenerateFullPathToTool() => _crossgenPath;

        public RunReadyToRunCompiler()
        {
            LogStandardErrorAsError = true;
        }

        protected override bool ValidateParameters()
        {
            _crossgenPath = CrossgenTool.ItemSpec;
            _clrjitPath = CrossgenTool.GetMetadata("JitPath");
            _diasymreaderPath = CrossgenTool.GetMetadata("DiaSymReader");

            if (!File.Exists(_crossgenPath) || !File.Exists(_clrjitPath))
            {
                return false;
            }

            _createPDBCommand = CompilationEntry.GetMetadata("CreatePDBCommand");

            if (IsPdbCompilation)
            {
                _outputR2RImage = CompilationEntry.ItemSpec;
                _outputPDBImage = CompilationEntry.GetMetadata("OutputPDBImage");

                if (!String.IsNullOrEmpty(_diasymreaderPath) && !File.Exists(_diasymreaderPath))
                {
                    return false;
                }

                // R2R image has to be created before emitting native symbols (crossgen needs this as an input argument)
                if (String.IsNullOrEmpty(_outputPDBImage) || !File.Exists(_outputR2RImage))
                {
                    return false;
                }
            }
            else
            {
                _inputAssembly = CompilationEntry.ItemSpec;
                _outputR2RImage = CompilationEntry.GetMetadata("OutputR2RImage");

                if (!File.Exists(_inputAssembly))
                {
                    return false;
                }
            }

            return true;
        }

        // TEMP LOGIC - SDK Should provide correct list - When fixing https://github.com/dotnet/sdk/issues/3110, delete
        // both IsManagedAssemblyToUseAsCrossgenReference and IsReferenceAssembly
        bool IsManagedAssemblyToUseAsCrossgenReference(ITaskItem file)
        {
            // Reference only managed assemblies that will be published to the root directory.
            string relativeOutputPath = file.GetMetadata(MetadataKeys.RelativePath);
            if (!String.IsNullOrEmpty(Path.GetDirectoryName(relativeOutputPath)))
            {
                return false;
            }

            using (FileStream fs = new FileStream(file.ItemSpec, FileMode.Open, FileAccess.Read))
            {
                try
                {
                    using (var pereader = new PEReader(fs))
                    {
                        if (!pereader.HasMetadata)
                            return false;

                        MetadataReader mdReader = pereader.GetMetadataReader();
                        if (!mdReader.IsAssembly)
                            return false;

                        // Reference assemblies should never be given to crossgen
                        if (IsReferenceAssembly(mdReader))
                            return false;
                    }
                }
                catch (BadImageFormatException)
                {
                    // Not a valid assembly file
                    return false;
                }
            }

            return true;
        }

        private bool IsReferenceAssembly(MetadataReader mdReader)
        {
            foreach (var attributeHandle in mdReader.GetAssemblyDefinition().GetCustomAttributes())
            {
                StringHandle attributeTypeName = default;
                StringHandle attributeTypeNamespace = default;
                EntityHandle attributeCtor = mdReader.GetCustomAttribute(attributeHandle).Constructor;

                if (attributeCtor.Kind == HandleKind.MemberReference)
                {
                    EntityHandle attributeMemberParent = mdReader.GetMemberReference((MemberReferenceHandle)attributeCtor).Parent;
                    if (attributeMemberParent.Kind == HandleKind.TypeReference)
                    {
                        TypeReference attributeTypeRef = mdReader.GetTypeReference((TypeReferenceHandle)attributeMemberParent);
                        attributeTypeName = attributeTypeRef.Name;
                        attributeTypeNamespace = attributeTypeRef.Namespace;
                    }
                }
                else if (attributeCtor.Kind == HandleKind.MethodDefinition)
                {
                    TypeDefinitionHandle attributeTypeDefHandle = mdReader.GetMethodDefinition((MethodDefinitionHandle)attributeCtor).GetDeclaringType();
                    TypeDefinition attributeTypeDef = mdReader.GetTypeDefinition(attributeTypeDefHandle);
                    attributeTypeName = attributeTypeDef.Name;
                    attributeTypeNamespace = attributeTypeDef.Namespace;
                }

                if (!attributeTypeName.IsNil &&
                    !attributeTypeNamespace.IsNil &&
                    mdReader.StringComparer.Equals(attributeTypeName, "ReferenceAssemblyAttribute") &&
                    mdReader.StringComparer.Equals(attributeTypeNamespace, "System.Runtime.CompilerServices"))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetAssemblyReferencesCommands()
        {
            StringBuilder result = new StringBuilder();

            // Add all runtime libraries to the list of references passed to crossgen
            foreach (var runtimeAssembly in ImplementationAssemblies)
            {
                // When generating PDBs, we must not add a reference to the IL version of the R2R image for which we're trying to generate a PDB
                if (IsPdbCompilation && String.Equals(Path.GetFileName(runtimeAssembly.ItemSpec), Path.GetFileName(_outputR2RImage), StringComparison.OrdinalIgnoreCase))
                    continue;

                // TODO: Delete check when fixing https://github.com/dotnet/sdk/issues/3110
                if (!IsManagedAssemblyToUseAsCrossgenReference(runtimeAssembly))
                    continue;

                result.AppendLine($"/r \"{runtimeAssembly}\"");
            }

            return result.ToString();
        }

        protected override string GenerateResponseFileCommands()
        {
            StringBuilder result = new StringBuilder();

            result.AppendLine("/nologo");

            if (IsPdbCompilation)
            {
                result.Append(GetAssemblyReferencesCommands());

                if (!String.IsNullOrEmpty(_diasymreaderPath))
                {
                    result.AppendLine($"/DiasymreaderPath \"{_diasymreaderPath}\"");
                }

                result.AppendLine(_createPDBCommand);
                result.AppendLine($"\"{_outputR2RImage}\"");
            }
            else
            {
                result.AppendLine("/MissingDependenciesOK");
                result.AppendLine($"/JITPath \"{_clrjitPath}\"");
                result.Append(GetAssemblyReferencesCommands());
                result.AppendLine($"/out \"{_outputR2RImage}\"");
                result.AppendLine($"\"{_inputAssembly}\"");
            }

            return result.ToString();
        }

        protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            // Ensure output sub-directories exists - Crossgen does not create directories for output files. Any relative path used with the 
            // '/out' parameter has to have an existing directory.
            Directory.CreateDirectory(Path.GetDirectoryName(_outputR2RImage));

            return base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);
        }
    }
}
