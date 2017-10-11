// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateUpdateTests
{
    public class MockInstallUnitDescriptor : IInstallUnitDescriptor
    {
        public string Identifier { get; set; }

        public Guid FactoryId { get; set; }

        public Guid MountPointId { get; set; }

        public IReadOnlyDictionary<string, string> Details { get; set; }

        public string UserReadableIdentifier { get; set; }
    }
}
