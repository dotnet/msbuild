// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using SdkResolverContextBase = Microsoft.Build.Framework.SdkResolverContext;

namespace NuGet.MSBuildSdkResolver.UnitTests
{
    /// <summary>
    /// A mock implementation of <see cref="SdkResolverContextBase"/> that uses a <see cref="MockSdkLogger"/>.
    /// </summary>
    public sealed class MockSdkResolverContext : SdkResolverContextBase
    {
        /// <summary>
        /// Initializes a new instance of the MockSdkResolverContext class.
        /// </summary>
        /// <param name="projectPath">The path to the project.</param>
        public MockSdkResolverContext(string projectPath)
        {
            Logger = MockSdkLogger;

            ProjectFilePath = projectPath;
        }

        /// <summary>
        /// Gets the <see cref="MockSdkLogger"/> being used by the context.
        /// </summary>
        public MockSdkLogger MockSdkLogger { get; } = new MockSdkLogger();
    }
}
