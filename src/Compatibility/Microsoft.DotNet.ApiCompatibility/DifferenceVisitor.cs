// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// The visitor that traverses the mappers' tree and gets it's differences in a <see cref="DiagnosticBag{CompatDifference}"/>.
    /// </summary>
    public class DifferenceVisitor : MapperVisitor
    {
        private readonly DiagnosticBag<CompatDifference>[] _diagnosticBags;

        /// <summary>
        /// Instantiates the visitor with the desired settings.
        /// </summary>
        /// <param name="rightCount">Represents the number of elements that the mappers contain on the right hand side.</param>
        /// <param name="noWarn">A comma separated list of diagnostic IDs to ignore.</param>
        /// <param name="ignoredDifferences">A list of tuples to ignore diagnostic IDs by symbol.</param>
        public DifferenceVisitor(int rightCount = 1, string noWarn = null, (string diagnosticId, string symbolId)[] ignoredDifferences = null)
        {
            if (rightCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(rightCount));
            }

            _diagnosticBags = new DiagnosticBag<CompatDifference>[rightCount];

            for (int i = 0; i < rightCount; i++)
            {
                _diagnosticBags[i] = new DiagnosticBag<CompatDifference>(noWarn ?? string.Empty, ignoredDifferences ?? Array.Empty<(string, string)>());
            }
        }

        /// <summary>
        /// Visits an <see cref="AssemblyMapper"/> and adds it's differences to the <see cref="DiagnosticBag{CompatDifference}"/>.
        /// </summary>
        /// <param name="assembly">The mapper to visit.</param>
        public override void Visit(AssemblyMapper assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            AddDifferences(assembly);
            base.Visit(assembly);
        }

        /// <summary>
        /// Visits an <see cref="TypeMapper"/> and adds it's differences to the <see cref="DiagnosticBag{CompatDifference}"/>.
        /// </summary>
        /// <param name="type">The mapper to visit.</param>
        public override void Visit(TypeMapper type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            AddDifferences(type);

            if (type.ShouldDiffMembers)
            {
                base.Visit(type);
            }
        }

        /// <summary>
        /// Visits an <see cref="MemberMapper"/> and adds it's differences to the <see cref="DiagnosticBag{CompatDifference}"/>.
        /// </summary>
        /// <param name="member">The mapper to visit.</param>
        public override void Visit(MemberMapper member)
        {
            if (member == null)
            {
                throw new ArgumentNullException(nameof(member));
            }

            AddDifferences(member);
        }

        /// <summary>
        /// A list of <see cref="DiagnosticBag{CompatDifference}"/>.
        /// One per element compared in the right hand side.
        /// </summary>
        public IEnumerable<DiagnosticBag<CompatDifference>> DiagnosticBags => _diagnosticBags;

        private void AddDifferences<T>(ElementMapper<T> mapper)
        {
            IReadOnlyList<IEnumerable<CompatDifference>> differences = mapper.GetDifferences();

            if (_diagnosticBags.Length != differences.Count)
            {
                throw new InvalidOperationException(Resources.VisitorRightCountShouldMatchMappersSetSize);
            }

            for (int i = 0; i < differences.Count; i++)
            {
                _diagnosticBags[i].AddRange(differences[i]);
            }
        }
    }
}
