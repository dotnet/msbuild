// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
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
            Settings = settings ?? new ApiComparerSettings();

            ruleRunner.InitializeRules(Settings.ToRuleSettings());
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
            var mapper = _elementMapperFactory.CreateAssemblyMapper(Settings.ToMapperSettings());
            mapper.AddElement(left, ElementSide.Left);
            mapper.AddElement(right, ElementSide.Right);

            IDifferenceVisitor visitor = _differenceVisitorFactory.Create();
            visitor.Visit(mapper);

            return visitor.DiagnosticCollections[0];
        }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> GetDifferences(IEnumerable<ElementContainer<IAssemblySymbol>> left,
            IEnumerable<ElementContainer<IAssemblySymbol>> right)
        {
            var mapper = _elementMapperFactory.CreateAssemblySetMapper(Settings.ToMapperSettings());
            mapper.AddElement(left, ElementSide.Left);
            mapper.AddElement(right, ElementSide.Right);

            IDifferenceVisitor visitor = _differenceVisitorFactory.Create();
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
            var mapper = _elementMapperFactory.CreateAssemblyMapper(Settings.ToMapperSettings(), rightCount);
            mapper.AddElement(left, ElementSide.Left);
            for (int i = 0; i < rightCount; i++)
            {
                mapper.AddElement(right[i], ElementSide.Right, i);
            }

            IDifferenceVisitor visitor = _differenceVisitorFactory.Create(rightCount);
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
