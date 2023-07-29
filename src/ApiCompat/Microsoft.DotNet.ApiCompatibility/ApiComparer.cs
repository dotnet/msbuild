// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Mapping;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Performs api comparison based on ISymbol inputs.
    /// </summary>
    public class ApiComparer : IApiComparer
    {
        private readonly IDifferenceVisitorFactory _differenceVisitorFactory;
        private readonly IElementMapperFactory _elementMapperFactory;

        /// <inheritdoc />
        public ApiComparerSettings Settings { get; }

        public ApiComparer(IRuleFactory ruleFactory,
            ApiComparerSettings? settings = null,
            IDifferenceVisitorFactory? differenceVisitorFactory = null,
            IRuleContext? ruleContext = null,
            Func<IRuleFactory, IRuleContext, IRuleRunner>? ruleRunnerFactory = null,
            Func<IRuleRunner, IElementMapperFactory>? elementMapperFactory = null)
        {
            ruleContext ??= new RuleContext();
            IRuleRunner ruleRunner = ruleRunnerFactory?.Invoke(ruleFactory, ruleContext) ?? new RuleRunner(ruleFactory, ruleContext);

            _differenceVisitorFactory = differenceVisitorFactory ?? new DifferenceVisitorFactory();
            _elementMapperFactory = elementMapperFactory?.Invoke(ruleRunner) ?? new ElementMapperFactory(ruleRunner);
            Settings = settings ?? new();

            ruleRunner.InitializeRules(Settings);
        }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> GetDifferences(IAssemblySymbol left,
            IAssemblySymbol right)
        {
            return GetDifferences(new ElementContainer<IAssemblySymbol>(left, MetadataInformation.DefaultLeft),
                new ElementContainer<IAssemblySymbol>(right, MetadataInformation.DefaultRight));
        }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> GetDifferences(ElementContainer<IAssemblySymbol> left,
            ElementContainer<IAssemblySymbol> right)
        {
            IAssemblyMapper assemblyMapper = _elementMapperFactory.CreateAssemblyMapper(Settings, rightCount: 1);
            assemblyMapper.AddElement(left, ElementSide.Left);
            assemblyMapper.AddElement(right, ElementSide.Right);

            IDifferenceVisitor visitor = _differenceVisitorFactory.Create();
            visitor.Visit(assemblyMapper);

            return visitor.CompatDifferences;
        }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> GetDifferences(IEnumerable<ElementContainer<IAssemblySymbol>> left,
            IEnumerable<ElementContainer<IAssemblySymbol>> right)
        {
            IAssemblySetMapper assemblySetMapper = _elementMapperFactory.CreateAssemblySetMapper(Settings, rightCount: 1);
            assemblySetMapper.AddElement(left, ElementSide.Left);
            assemblySetMapper.AddElement(right, ElementSide.Right);

            IDifferenceVisitor visitor = _differenceVisitorFactory.Create();
            visitor.Visit(assemblySetMapper);

            return visitor.CompatDifferences;
        }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> GetDifferences(IEnumerable<IAssemblySymbol> left,
            IEnumerable<IAssemblySymbol> right)
        {
            List<ElementContainer<IAssemblySymbol>> transformedLeft = new();
            foreach (IAssemblySymbol assemblySymbol in left)
            {
                transformedLeft.Add(new ElementContainer<IAssemblySymbol>(assemblySymbol, MetadataInformation.DefaultLeft));
            }

            List<ElementContainer<IAssemblySymbol>> transformedRight = new();
            foreach (IAssemblySymbol assemblySymbol in right)
            {
                transformedRight.Add(new ElementContainer<IAssemblySymbol>(assemblySymbol, MetadataInformation.DefaultRight));
            }

            return GetDifferences(transformedLeft, transformedRight);
        }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> GetDifferences(ElementContainer<IAssemblySymbol> left,
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right)
        {
            int rightCount = right.Count;
            IAssemblyMapper assemblyMapper = _elementMapperFactory.CreateAssemblyMapper(Settings, rightCount);
            assemblyMapper.AddElement(left, ElementSide.Left);
            for (int i = 0; i < rightCount; i++)
            {
                assemblyMapper.AddElement(right[i], ElementSide.Right, i);
            }

            IDifferenceVisitor visitor = _differenceVisitorFactory.Create();
            visitor.Visit(assemblyMapper);

            return SortCompatDifferencesByInputMetadata(visitor.CompatDifferences.ToLookup(c => c.Right, t => t), right);
        }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> GetDifferences(IEnumerable<ElementContainer<IAssemblySymbol>> left,
            IReadOnlyList<IEnumerable<ElementContainer<IAssemblySymbol>>> right)
        {
            IAssemblySetMapper assemblySetMapper = _elementMapperFactory.CreateAssemblySetMapper(Settings, right.Count);
            assemblySetMapper.AddElement(left, ElementSide.Left);
            for (int rightIndex = 0; rightIndex < right.Count; rightIndex++)
            {
                assemblySetMapper.AddElement(right[rightIndex], ElementSide.Right, rightIndex);
            }

            IDifferenceVisitor visitor = _differenceVisitorFactory.Create();
            visitor.Visit(assemblySetMapper);

            return SortCompatDifferencesByInputMetadata(visitor.CompatDifferences.ToLookup(c => c.Left, t => t), left);
        }

        /// <summary>
        /// Sort the compat differences by the order of the passed in metadata.
        /// </summary>
        private static IEnumerable<CompatDifference> SortCompatDifferencesByInputMetadata(ILookup<MetadataInformation, CompatDifference> compatDifferencesLookup,
            IEnumerable<ElementContainer<IAssemblySymbol>> inputMetadata)
        {
            HashSet<MetadataInformation> processedMetadata = new();
            List<CompatDifference> sortedCompatDifferences = new();

            foreach (ElementContainer<IAssemblySymbol> elementContainer in inputMetadata)
            {
                sortedCompatDifferences.AddRange(compatDifferencesLookup[elementContainer.MetadataInformation]);
                processedMetadata.Add(elementContainer.MetadataInformation);
            }

            foreach (IGrouping<MetadataInformation, CompatDifference> compatDifferenceGroup in compatDifferencesLookup)
            {
                if (processedMetadata.Contains(compatDifferenceGroup.Key))
                    continue;

                sortedCompatDifferences.AddRange(compatDifferenceGroup);
            }

            return sortedCompatDifferences;
        }
    }
}
