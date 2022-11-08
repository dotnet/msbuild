// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// The visitor that traverses the mappers' tree and gets it's differences in a <see cref="DiagnosticBag{CompatDifference}"/>.
    /// </summary>
    public class DifferenceVisitor : IDifferenceVisitor
    {
        private readonly HashSet<CompatDifference> _compatDifferences = new();

        /// <inheritdoc />
        public IEnumerable<CompatDifference> CompatDifferences => _compatDifferences;

        /// <inheritdoc />
        public void Visit<T>(IElementMapper<T> mapper)
        {
            if (mapper is IAssemblySetMapper assemblySetMapper)
            {
                Visit(assemblySetMapper);
            }
            else if (mapper is IAssemblyMapper assemblyMapper)
            {
                Visit(assemblyMapper);
            }
            else if (mapper is INamespaceMapper nsMapper)
            {
                Visit(nsMapper);
            }
            else if (mapper is ITypeMapper typeMapper)
            {
                Visit(typeMapper);
            }
            else if (mapper is IMemberMapper memberMapper)
            {
                Visit(memberMapper);
            }
        }

        /// <inheritdoc />
        public void Visit(IAssemblySetMapper mapper)
        {
            foreach (IAssemblyMapper assembly in mapper.GetAssemblies())
            {
                Visit(assembly);
            }
        }

        /// <inheritdoc />
        public void Visit(IAssemblyMapper assembly)
        {
            AddDifferences(assembly);

            foreach (INamespaceMapper @namespace in assembly.GetNamespaces())
            {
                Visit(@namespace);
            }

            // After visiting the assembly, the assembly mapper will contain any assembly load errors that happened
            // when trying to resolve typeforwarded types. If there were any, we add them to the diagnostic bag next.
            foreach (CompatDifference item in assembly.AssemblyLoadErrors)
            {
                _compatDifferences.Add(item);
            }
        }

        /// <inheritdoc />
        public void Visit(INamespaceMapper @namespace)
        {
            foreach (ITypeMapper type in @namespace.GetTypes())
            {
                Visit(type);
            }
        }

        /// <inheritdoc />
        public void Visit(ITypeMapper type)
        {
            AddDifferences(type);

            if (type.ShouldDiffMembers)
            {
                foreach (ITypeMapper nestedType in type.GetNestedTypes())
                {
                    Visit(nestedType);
                }

                foreach (IMemberMapper member in type.GetMembers())
                {
                    Visit(member);
                }
            }
        }

        /// <inheritdoc />
        public void Visit(IMemberMapper member)
        {
            AddDifferences(member);
        }

        private void AddDifferences<T>(IElementMapper<T> mapper)
        {
            foreach (CompatDifference item in mapper.GetDifferences())
            {
                _compatDifferences.Add(item);
            }
        }
    }
}
