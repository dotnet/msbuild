// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Performs api comparison based on ISymbol inputs.
    /// </summary>
    public class ApiComparer : IApiComparer
    {
        /// <inheritdoc />
        public bool IncludeInternalSymbols { get; set; }

        /// <inheritdoc />
        public bool StrictMode { get; set; }

        /// <inheritdoc />
        public bool WarnOnMissingReferences { get; set; }

        /// <inheritdoc />
        public Func<string?, string[]?, ComparingSettings>? GetComparingSettings { get; set; }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> GetDifferences(IEnumerable<IAssemblySymbol> left,
            IEnumerable<IAssemblySymbol> right,
            string? leftName = null,
            string? rightName = null)
        {
            AssemblySetMapper mapper = new(GetComparingSettingsCore(leftName, rightName != null ? new[] { rightName } : null));
            mapper.AddElement(left, ElementSide.Left);
            mapper.AddElement(right, ElementSide.Right);

            DifferenceVisitor visitor = new();
            visitor.Visit(mapper);
            return visitor.DiagnosticCollections.First();
        }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> GetDifferences(IAssemblySymbol left,
            IAssemblySymbol right,
            string? leftName = null,
            string? rightName = null)
        {
            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            AssemblyMapper mapper = new(GetComparingSettingsCore(leftName, rightName != null ? new[] { rightName } : null));
            mapper.AddElement(left, ElementSide.Left);
            mapper.AddElement(right, ElementSide.Right);

            DifferenceVisitor visitor = new();
            visitor.Visit(mapper);
            return visitor.DiagnosticCollections.First();
        }

        /// <inheritdoc />
        public IEnumerable<(MetadataInformation left, MetadataInformation right, IEnumerable<CompatDifference> differences)> GetDifferences(ElementContainer<IAssemblySymbol> left,
            IList<ElementContainer<IAssemblySymbol>> right)
        {
            int rightCount = right.Count;
            AssemblyMapper mapper = new(new ComparingSettings(), rightSetSize: rightCount);
            mapper.AddElement(left.Element, ElementSide.Left);

            string[] rightNames = new string[rightCount];
            for (int i = 0; i < rightCount; i++)
            {
                ElementContainer<IAssemblySymbol> element = right[i];
                rightNames[i] = element.MetadataInformation.DisplayString;
                mapper.AddElement(element.Element, ElementSide.Right, i);
            }

            mapper.Settings = GetComparingSettingsCore(left.MetadataInformation.DisplayString, rightNames);

            DifferenceVisitor visitor = new(rightCount: rightCount);
            visitor.Visit(mapper);

            (MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)[] result = new(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)[rightCount];

            int count = 0;
            foreach (IEnumerable<CompatDifference> collection in visitor.DiagnosticCollections)
            {
                result[count] = (left.MetadataInformation, right[count].MetadataInformation, collection);
                count++;
            }
            
            return result;
        }

        private ComparingSettings GetComparingSettingsCore(string? leftName, string[]? rightNames)
        {
            if (GetComparingSettings != null)
                return GetComparingSettings(leftName, rightNames);

            return new ComparingSettings(includeInternalSymbols: IncludeInternalSymbols,
                strictMode: StrictMode,
                leftName: leftName,
                rightNames: rightNames,
                warnOnMissingReferences: WarnOnMissingReferences);
        }
    }
}
