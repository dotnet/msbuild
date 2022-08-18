// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Runner.Tests
{
    public class ApiCompatRunnerTests
    {
        private static ApiCompatRunner MockApiCompatRunner(MetadataInformation left = default,
            MetadataInformation right = default)
        {
            // Mock the api comparer's GetDifferences method so that it returns items.
            Mock<IApiComparer> apiComparerMock = new();
            apiComparerMock
                .Setup(y => y.GetDifferences(It.IsAny<ElementContainer<IAssemblySymbol>>(), It.IsAny<IReadOnlyList<ElementContainer<IAssemblySymbol>>>()))
                .Returns(new CompatDifference[]
                {
                    new CompatDifference(left, right, "CP0001", "Invalid", DifferenceType.Removed, "X01")
                });
            Mock<IApiComparerFactory> apiComparerFactoryMock = new();
            apiComparerFactoryMock
                .Setup(x => x.Create(null))
                .Returns(apiComparerMock.Object);

            // Mock the suppression engine
            Mock<ISuppressionEngine> suppressionEngineMock = new();
            suppressionEngineMock
                .Setup(m => m.IsErrorSuppressed(It.IsAny<Suppression>()))
                .Returns(false);

            // Mock the assembly symbol loader factory to return a default assembly symbol loader.
            Mock<IAssemblySymbolLoader> assemblySymbolLoaderMock = new();
            assemblySymbolLoaderMock
                .Setup(y => y.LoadAssemblies(It.IsAny<string[]>()))
                .Returns(new IAssemblySymbol[]
                {
                    null
                });

            Mock<IAssemblySymbolLoaderFactory> assemblyLoaderFactoryMock = new();
            assemblyLoaderFactoryMock
                .Setup(m => m.Create(It.IsAny<bool>()))
                .Returns(assemblySymbolLoaderMock.Object);

            return new(Mock.Of<ICompatibilityLogger>(),
                suppressionEngineMock.Object,
                apiComparerFactoryMock.Object,
                assemblyLoaderFactoryMock.Object);
        }

        [Fact]
        public void EnqueueWorkItem_NoDuplicateLeftsForDifferentRights_SingleLeftWithMultipleRights()
        {
            ApiCompatRunner apiCompatRunner = MockApiCompatRunner();

            MetadataInformation left = new("A.dll", @"lib\netstandard2.0\A.dll");
            MetadataInformation right1 = new("A.dll", @"lib\netstandard2.0\A.dll");
            MetadataInformation right2 = new("A.dll", @"lib\net462\A.dll");

            apiCompatRunner.EnqueueWorkItem(new ApiCompatRunnerWorkItem(left, new ApiCompatRunnerOptions(), right1, right2));

            Assert.Single(apiCompatRunner.WorkItems);
            Assert.Equal(2, apiCompatRunner.WorkItems.First().Rights.Count);
        }

        [Fact]
        public void EnqueueWorkItem_NoDuplicateRightsForSpecificLeft_SingleRight()
        {
            ApiCompatRunner apiCompatRunner = MockApiCompatRunner();

            MetadataInformation left = new("A.dll", @"lib\netstandard2.0\A.dll");
            MetadataInformation right = new("A.dll", @"lib\net462\A.dll");

            apiCompatRunner.EnqueueWorkItem(new ApiCompatRunnerWorkItem(left, new ApiCompatRunnerOptions(), right, right));

            Assert.Single(apiCompatRunner.WorkItems);
            Assert.Single(apiCompatRunner.WorkItems.First().Rights);
        }

        [Fact]
        public void EnqueueWorkItem_DifferentAssemblies_EqualNumberOfWorkItems()
        {
            ApiCompatRunner apiCompatRunner = MockApiCompatRunner();

            MetadataInformation left1 = new("A.dll", @"lib\netstandard2.0\A.dll");
            MetadataInformation left2 = new("B.dll", @"lib\netstandard2.0\A.dll");
            MetadataInformation right = new("A.dll", @"lib\net462\A.dll");

            apiCompatRunner.EnqueueWorkItem(new ApiCompatRunnerWorkItem(left1, new ApiCompatRunnerOptions(), right));
            apiCompatRunner.EnqueueWorkItem(new ApiCompatRunnerWorkItem(left2, new ApiCompatRunnerOptions(), right));

            Assert.Equal(2, apiCompatRunner.WorkItems.Count());
        }

        [Fact]
        public void ExecuteWorkItems_NoWorkItemsEnqueued_EmptyWorkItems()
        {
            ApiCompatRunner apiCompatRunner = MockApiCompatRunner();

            Assert.Empty(apiCompatRunner.WorkItems);
            apiCompatRunner.ExecuteWorkItems();
            Assert.Empty(apiCompatRunner.WorkItems);
        }

        [Fact]
        public void ExecuteWorkItems_WorkItemsEnqueued_EmptyWorkItems()
        {
            MetadataInformation left = new("A.dll", @"lib\netstandard2.0\A.dll", references: new string[] { @"ref\net6.0\System.Runtime.dll", @"ref\net6.0\System.Collections.dll" });
            MetadataInformation right = new("A.dll", @"lib\net462\A.dll");
            ApiCompatRunnerOptions options = new(enableStrictMode: true, isBaselineComparison: false);

            ApiCompatRunner apiCompatRunner = MockApiCompatRunner(left, right);
            apiCompatRunner.EnqueueWorkItem(new ApiCompatRunnerWorkItem(left, options, right));
            apiCompatRunner.ExecuteWorkItems();

            Assert.Empty(apiCompatRunner.WorkItems);
        }
    }
}
