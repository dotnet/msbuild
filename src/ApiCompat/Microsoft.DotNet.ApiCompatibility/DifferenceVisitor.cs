// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// The visitor that traverses the mappers' tree and gets it's differences in a <see cref="DiagnosticBag{CompatDifference}"/>.
    /// </summary>
    public class DifferenceVisitor : IDifferenceVisitor
    {
        private readonly HashSet<CompatDifference>[] _diagnostics;

        /// <inheritdoc />
        public IReadOnlyList<IReadOnlyCollection<CompatDifference>> DiagnosticCollections => _diagnostics;

        /// <summary>
        /// Instantiates the visitor with the desired settings.
        /// </summary>
        /// <param name="rightCount">Represents the number of elements that the mappers contain on the right hand side.</param>
        public DifferenceVisitor(int rightCount = 1)
        {
            if (rightCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(rightCount));
            }

            _diagnostics = new HashSet<CompatDifference>[rightCount];
            for (int i = 0; i < rightCount; i++)
            {
                _diagnostics[i] = new HashSet<CompatDifference>();
            }
        }

        /// <inheritdoc />
        public void Visit<T>(ElementMapper<T> mapper)
        {
            if (mapper is AssemblySetMapper assemblySetMapper)
            {
                Visit(assemblySetMapper);
            }
            else if (mapper is AssemblyMapper assemblyMapper)
            {
                Visit(assemblyMapper);
            }
            else if (mapper is NamespaceMapper nsMapper)
            {
                Visit(nsMapper);
            }
            else if (mapper is TypeMapper typeMapper)
            {
                Visit(typeMapper);
            }
            else if (mapper is MemberMapper memberMapper)
            {
                Visit(memberMapper);
            }
        }

        /// <inheritdoc />
        public void Visit(AssemblySetMapper mapper)
        {
            foreach (AssemblyMapper assembly in mapper.GetAssemblies())
            {
                Visit(assembly);
            }
        }

        /// <inheritdoc />
        public void Visit(AssemblyMapper assembly)
        {
            AddDifferences(assembly);

            foreach (NamespaceMapper @namespace in assembly.GetNamespaces())
            {
                Visit(@namespace);
            }

            // After visiting the assembly, the assembly mapper will contain any assembly load errors that happened
            // when trying to resolve typeforwarded types. If there were any, we add them to the diagnostic bag next.
            AddToDiagnosticCollections(assembly.AssemblyLoadErrors);
        }

        /// <inheritdoc />
        public void Visit(NamespaceMapper @namespace)
        {
            foreach (TypeMapper type in @namespace.GetTypes())
            {
                Visit(type);
            }
        }

        /// <inheritdoc />
        public void Visit(TypeMapper type)
        {
            AddDifferences(type);

            if (type.ShouldDiffMembers)
            {
                foreach (TypeMapper nestedType in type.GetNestedTypes())
                {
                    Visit(nestedType);
                }

                foreach (MemberMapper member in type.GetMembers())
                {
                    Visit(member);
                }
            }
        }

        /// <inheritdoc />
        public void Visit(MemberMapper member)
        {
            AddDifferences(member);
        }

        private void AddDifferences<T>(ElementMapper<T> mapper)
        {
            IReadOnlyList<IEnumerable<CompatDifference>> differences = mapper.GetDifferences();
            AddToDiagnosticCollections(differences);
        }

        private void AddToDiagnosticCollections(IReadOnlyList<IEnumerable<CompatDifference>> diagnosticsToAdd)
        {
            if (_diagnostics.Length != diagnosticsToAdd.Count)
            {
                throw new InvalidOperationException(Resources.VisitorRightCountShouldMatchMappersSetSize);
            }

            for (int i = 0; i < diagnosticsToAdd.Count; i++)
            {
                foreach (CompatDifference item in diagnosticsToAdd[i])
                {
                    _diagnostics[i].Add(item);
                }
            }
        }
    }
}
