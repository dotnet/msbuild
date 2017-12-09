// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.NET.Build.Tasks.ConflictResolution;

namespace Microsoft.NET.Build.Tasks.UnitTests.Mocks
{
    class MockConflictItem : IConflictItem
    {
        public MockConflictItem(string name = "System.Ben")
        {
            Key = name + ".dll";
            AssemblyVersion = new Version("1.0.0.0");
            ItemType = ConflictItemType.Reference;
            Exists = true;
            FileName = name + ".dll";
            FileVersion = new Version("1.0.0.0");
            PackageId = name;
            PackageVersion = new Version("1.0.0");
            DisplayName = name;
        }
        public string Key { get; set; }

        public Version AssemblyVersion { get; set; }

        public ConflictItemType ItemType { get; set; }

        public bool Exists { get; set; }

        public string FileName { get; set; }

        public Version FileVersion { get; set; }

        public string PackageId { get; set; }

        public Version PackageVersion { get; set; }

        public string DisplayName { get; set; }
    }
}
