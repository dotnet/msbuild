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
    public class DiferenceVisitor : MapperVisitor
    {
        private readonly DiagnosticBag<CompatDifference> _differenceBag;

        /// <summary>
        /// Instantiates the visitor with the desired settings.
        /// </summary>
        /// <param name="noWarn">A comma separated list of diagnostic IDs to ignore.</param>
        /// <param name="ignoredDifferences">A list of tuples to ignore diagnostic IDs by symbol.</param>
        public DiferenceVisitor(string noWarn = null, (string diagnosticId, string symbolId)[] ignoredDifferences = null)
        {
            _differenceBag = new DiagnosticBag<CompatDifference>(noWarn ?? string.Empty, ignoredDifferences ?? Array.Empty<(string, string)>());
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

            _differenceBag.AddRange(assembly.GetDifferences());
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

            _differenceBag.AddRange(type.GetDifferences());

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

            _differenceBag.AddRange(member.GetDifferences());
        }

        /// <summary>
        /// The differences that the <see cref="DiagnosticBag{CompatDifference}"/> has at this point.
        /// </summary>
        public IEnumerable<CompatDifference> Differences => _differenceBag.Differences;
    }
}
