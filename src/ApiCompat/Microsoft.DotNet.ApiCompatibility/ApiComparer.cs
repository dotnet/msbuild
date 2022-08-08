// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

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
            return visitor.DiagnosticCollections[0];
        }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> GetDifferences(IAssemblySymbol left,
            IAssemblySymbol right,
            string? leftName = null,
            string? rightName = null)
        {
            AssemblyMapper mapper = new(GetComparingSettingsCore(leftName, rightName != null ? new[] { rightName } : null));
            mapper.AddElement(left, ElementSide.Left);
            mapper.AddElement(right, ElementSide.Right);

            DifferenceVisitor visitor = new();
            visitor.Visit(mapper);
            return visitor.DiagnosticCollections[0];
        }

        /// <inheritdoc />
        public IEnumerable<(MetadataInformation left, MetadataInformation right, IEnumerable<CompatDifference> differences)> GetDifferences(ElementContainer<IAssemblySymbol> left,
            IList<ElementContainer<IAssemblySymbol>> right)
        {
            int rightCount = right.Count;

            // Retrieve the right names
            string[] rightNames = new string[rightCount];
            for (int i = 0; i < rightCount; i++)
            {
                rightNames[i] = right[i].MetadataInformation.DisplayString;
            }

            AssemblyMapper mapper = new(GetComparingSettingsCore(left.MetadataInformation.DisplayString, rightNames), rightSetSize: rightCount);
            mapper.AddElement(left.Element, ElementSide.Left);
            for (int i = 0; i < rightCount; i++)
            {
                mapper.AddElement(right[i].Element, ElementSide.Right, i);
            }

            DifferenceVisitor visitor = new(rightCount: rightCount);
            visitor.Visit(mapper);

            (MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)[] result = new(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)[rightCount];
            for (int i = 0; i < visitor.DiagnosticCollections.Count; i++)
            {
                result[i] = (left.MetadataInformation, right[i].MetadataInformation, visitor.DiagnosticCollections[i]);
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
