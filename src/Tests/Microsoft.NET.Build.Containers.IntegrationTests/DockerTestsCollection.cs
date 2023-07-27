// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.IntegrationTests;

[CollectionDefinition("Docker tests")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public class DockerTestsCollection : ICollectionFixture<DockerTestsFixture>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
