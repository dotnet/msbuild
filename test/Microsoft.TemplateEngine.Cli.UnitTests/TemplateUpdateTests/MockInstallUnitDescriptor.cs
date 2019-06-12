// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateUpdateTests
{
    public class MockInstallUnitDescriptor : IInstallUnitDescriptor
    {
        public MockInstallUnitDescriptor()
        {
            DetailKeysDisplayOrder = new List<string>();
        }

        public Guid DescriptorId { get; set; }

        public string Identifier { get; set; }

        public Guid FactoryId { get; set; }

        public Guid MountPointId { get; set; }

        public IReadOnlyDictionary<string, string> Details { get; set; }

        public string UninstallString => Identifier;

        public IReadOnlyList<string> DetailKeysDisplayOrder { get; }
    }
}
