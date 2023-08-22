// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using FluentAssertions.Execution;

namespace Microsoft.NET.TestFramework.Assertions
{
    public class AssemblyAssertions
    {
        private FileInfo _assemblyPath;

        public AssemblyAssertions(FileInfo assemblyPath)
        {
            _assemblyPath = assemblyPath;
        }

        public FileInfo AssemblyPath => _assemblyPath;

        public AndConstraint<AssemblyAssertions> ContainType(string expectedType)
        {
            var types = GetDeclaredTypeNames();

            Execute.Assertion
                .ForCondition(types.Contains(expectedType))
                .FailWith($"Expected type {expectedType} to be in assembly, but it is not.");
            return new AndConstraint<AssemblyAssertions>(this);
        }

        public AndConstraint<AssemblyAssertions> NotContainType(string expectedType)
        {
            var types = GetDeclaredTypeNames();

            Execute.Assertion
                .ForCondition(!types.Contains(expectedType))
                .FailWith($"Expected type {expectedType} to not be in assembly, but it is.");
            return new AndConstraint<AssemblyAssertions>(this);
        }

        public AndConstraint<AssemblyAssertions> HaveAttribute(string expectedAttribute)
        {
            var attributes = GetAssemblyAttributes();

            Execute.Assertion
                .ForCondition(attributes.Contains(expectedAttribute))
                .FailWith($"Expected attribute {expectedAttribute} to be in assembly, but it is not.");
            return new AndConstraint<AssemblyAssertions>(this);
        }

        private IEnumerable<string> GetDeclaredTypeNames()
        {
            using (var file = File.OpenRead(AssemblyPath.ToString()))
            {
                using var peReader = new PEReader(file);
                var metadataReader = peReader.GetMetadataReader();
                return metadataReader.TypeDefinitions.Where(t => !t.IsNil).Select(t =>
                {
                    var type = metadataReader.GetTypeDefinition(t);
                    return metadataReader.GetString(type.Namespace) + "." + metadataReader.GetString(type.Name);
                }).ToArray();
            }
        }

        private IEnumerable<string> GetAssemblyAttributes()
        {
            using (var file = File.OpenRead(AssemblyPath.ToString()))
            {
                var peReader = new PEReader(file);
                var metadataReader = peReader.GetMetadataReader();
                return metadataReader.CustomAttributes.Where(t => !t.IsNil).Select(t =>
                {
                    var attribute = metadataReader.GetCustomAttribute(t);
                    switch (attribute.Constructor.Kind)
                    {
                        case HandleKind.MethodDefinition:
                            {
                                var methodDef = metadataReader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                                var declaringTypeHandle = methodDef.GetDeclaringType();
                                var typeDefinition = metadataReader.GetTypeDefinition(declaringTypeHandle);
                                var @namespace = metadataReader.GetString(typeDefinition.Namespace);
                                var name = metadataReader.GetString(typeDefinition.Name);
                                return $"{@namespace}.{name}";
                            }
                        case HandleKind.MemberReference:
                            {
                                var memberRef = metadataReader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                                var typeRef = metadataReader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
                                var @namespace = metadataReader.GetString(typeRef.Namespace);
                                var name = metadataReader.GetString(typeRef.Name);
                                return $"{@namespace}.{name}";
                            }
                        default:
                            throw new InvalidOperationException();
                    }
                }).ToArray();
            }
        }
    }
}
