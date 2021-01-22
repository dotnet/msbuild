// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.DotNet.Cli.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Threading.Tasks;

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
                    var constructor = metadataReader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                    var type = metadataReader.GetTypeReference((TypeReferenceHandle)constructor.Parent);

                    return metadataReader.GetString(type.Namespace) + "." + metadataReader.GetString(type.Name);
                }).ToArray();
            }
        }
    }
}
