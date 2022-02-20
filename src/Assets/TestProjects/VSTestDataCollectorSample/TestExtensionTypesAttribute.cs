// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AttachmentProcessorDataCollector;

using Microsoft.VisualStudio.TestPlatform;

[assembly: TestExtensionTypes(typeof(SampleDataCollectorV1))]
[assembly: TestExtensionTypesV2(ExtensionInfo.ExtensionType, ExtensionInfo.ExtensionIdentifier, typeof(SampleDataCollectorV1), 1, "futureUnused")]
[assembly: TestExtensionTypesV2(ExtensionInfo.ExtensionType, ExtensionInfo.ExtensionIdentifier, typeof(SampleDataCollectorV2), 2)]

namespace Microsoft.VisualStudio.TestPlatform
{
    using System;

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
    internal sealed class TestExtensionTypesAttribute : Attribute
    {
        public TestExtensionTypesAttribute(params Type[] types)
        {
            Types = types;
        }

        public Type[] Types { get; }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
    internal sealed class TestExtensionTypesV2Attribute : Attribute
    {
        public string ExtensionType { get; }
        public string ExtensionIdentifier { get; }
        public Type ExtensionImplementation { get; }
        public int Version { get; }

        public TestExtensionTypesV2Attribute(string extensionType, string extensionIdentifier, Type extensionImplementation, int version, string _ = null)
        {
            ExtensionType = extensionType;
            ExtensionIdentifier = extensionIdentifier;
            ExtensionImplementation = extensionImplementation;
            Version = version;
        }
    }
}
