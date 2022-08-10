// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        private ComparingSettings? _comparingSettings;

        /// <inheritdoc />
        public bool IncludeInternalSymbols { get; set; }

        /// <inheritdoc />
        public bool StrictMode { get; set; }

        /// <inheritdoc />
        public bool WarnOnMissingReferences { get; set; }

        /// <inheritdoc />
        public ComparingSettings ComparingSettings
        {
            get => _comparingSettings ?? new ComparingSettings(includeInternalSymbols: IncludeInternalSymbols,
                strictMode: StrictMode,
                warnOnMissingReferences: WarnOnMissingReferences);
            set => _comparingSettings = value;
        }

        public ApiComparer(bool includeInternalSymbols = false,
            bool strictMode = false,
            bool warnOnMissingReferences = false,
            ComparingSettings? comparingSettings = null)
        {
            IncludeInternalSymbols = includeInternalSymbols;
            StrictMode = strictMode;
            WarnOnMissingReferences = warnOnMissingReferences;
            _comparingSettings = comparingSettings;
        }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> GetDifferences(IAssemblySymbol left,
            IAssemblySymbol right)
        {
            return GetDifferences(new ElementContainer<IAssemblySymbol>(left, new MetadataInformation()),
                new ElementContainer<IAssemblySymbol>(right, new MetadataInformation()));
        }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> GetDifferences(ElementContainer<IAssemblySymbol> left,
            ElementContainer<IAssemblySymbol> right)
        {
            AssemblyMapper mapper = new(ComparingSettings);
            mapper.AddElement(left, ElementSide.Left);
            mapper.AddElement(right, ElementSide.Right);

            DifferenceVisitor visitor = new();
            visitor.Visit(mapper);
            return visitor.DiagnosticCollections[0];
        }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> GetDifferences(IEnumerable<ElementContainer<IAssemblySymbol>> left,
            IEnumerable<ElementContainer<IAssemblySymbol>> right)
        {
            AssemblySetMapper mapper = new(ComparingSettings);
            mapper.AddElement(left, ElementSide.Left);
            mapper.AddElement(right, ElementSide.Right);

            DifferenceVisitor visitor = new();
            visitor.Visit(mapper);
            return visitor.DiagnosticCollections[0];
        }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> GetDifferences(IEnumerable<IAssemblySymbol> left,
            IEnumerable<IAssemblySymbol> right)
        {
            List<ElementContainer<IAssemblySymbol>> transformedLeft = new();
            foreach (IAssemblySymbol assemblySymbol in left)
            {
                transformedLeft.Add(new ElementContainer<IAssemblySymbol>(assemblySymbol, new MetadataInformation()));
            }

            List<ElementContainer<IAssemblySymbol>> transformedRight = new();
            foreach (IAssemblySymbol assemblySymbol in right)
            {
                transformedRight.Add(new ElementContainer<IAssemblySymbol>(assemblySymbol, new MetadataInformation()));
            }

            return GetDifferences(transformedLeft, transformedRight);
        }

        /// <inheritdoc />
        public IEnumerable<(MetadataInformation left, MetadataInformation right, IEnumerable<CompatDifference> differences)> GetDifferences(ElementContainer<IAssemblySymbol> left,
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right)
        {
            int rightCount = right.Count;

            AssemblyMapper mapper = new(ComparingSettings, rightCount);
            mapper.AddElement(left, ElementSide.Left);

            for (int i = 0; i < rightCount; i++)
            {
                mapper.AddElement(right[i], ElementSide.Right, i);
            }

            DifferenceVisitor visitor = new(rightCount);
            visitor.Visit(mapper);

            var result = new(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)[rightCount];
            for (int i = 0; i < visitor.DiagnosticCollections.Count; i++)
            {
                result[i] = (left.MetadataInformation, right[i].MetadataInformation, visitor.DiagnosticCollections[i]);
            }
            
            return result;
        }
    }
}
