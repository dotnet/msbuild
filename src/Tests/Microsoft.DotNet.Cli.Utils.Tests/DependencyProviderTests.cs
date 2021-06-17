// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.Win32;
using Xunit;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
#pragma warning disable CA1416
    public class DependencyProviderTests
    {
        [WindowsOnlyTheory]
        [InlineData(false, "NET.CORE.SDK,v6.0", @"SOFTWARE\Classes\Installer\Dependencies\NET.CORE.SDK,v6.0\Dependents", "HKEY_CURRENT_USER")]
        [InlineData(true, "NET.CORE.SDK,v6.0", @"SOFTWARE\Classes\Installer\Dependencies\NET.CORE.SDK,v6.0\Dependents", "HKEY_LOCAL_MACHINE")]
        public void ProviderProperties(bool allUsers, string providerKeyName, string expectedDependentsKeyPath, string expectedBaseKeyName)
        {
            DependencyProvider dep = new DependencyProvider(providerKeyName, allUsers);

            Assert.Equal(expectedDependentsKeyPath, dep.DependentsKeyPath);
            Assert.Equal(expectedBaseKeyName, dep.BaseKey.Name);
            Assert.Equal(providerKeyName, dep.ProviderKeyName);
        }

        [WindowsOnlyFact]
        public void ItCanAddDependents()
        {
            // We cannot create per-machine entries unless the tests run elevated. The results are the
            // the same, it's only the base key that's different
            DependencyProvider dep = new DependencyProvider(".NET_SDK_TEST_PROVIDER_KEY", allUsers: false);

            try
            {
                // We should not have any dependents
                Assert.Empty(dep.Dependents);

                dep.AddDependent("Microsoft.NET.SDK,v6.0.100");

                Assert.Single(dep.Dependents);
                Assert.Equal("Microsoft.NET.SDK,v6.0.100", dep.Dependents.First());
            }
            finally
            {
                // Clean up and delete everything
                using RegistryKey providerKey = dep.BaseKey.OpenSubKey(DependencyProvider.DependenciesKeyRelativePath, writable: true);
                providerKey?.DeleteSubKeyTree(dep.ProviderKeyName);
            }
        }

        [WindowsOnlyFact]
        public void ItCanFindVisualStudioDependents()
        {
            DependencyProvider dep = new DependencyProvider(".NET_SDK_TEST_PROVIDER_KEY", allUsers: false);

            try
            {
                // We should not have any dependents
                Assert.Empty(dep.Dependents);

                // Write the VS dependents key
                dep.AddDependent(DependencyProvider.VisualStudioDependentKeyName);

                Assert.True(dep.HasVisualStudioDependency);
            }
            finally
            {
                // Clean up and delete everything
                using RegistryKey providerKey = dep.BaseKey.OpenSubKey(DependencyProvider.DependenciesKeyRelativePath, writable: true);
                providerKey?.DeleteSubKeyTree(dep.ProviderKeyName);
            }
        }

        [WindowsOnlyFact]
        public void ItWillNotRemoveTheProviderIfOtherDependentsExist()
        {
            DependencyProvider dep = new DependencyProvider(".NET_SDK_TEST_PROVIDER_KEY", allUsers: false);

            try
            {
                // Write multiple dependents
                dep.AddDependent(DependencyProvider.VisualStudioDependentKeyName);
                dep.AddDependent("Microsoft.NET.SDK,v6.0.100");

                Assert.Equal(2, dep.Dependents.Count());

                dep.RemoveDependent("Microsoft.NET.SDK,v6.0.100", removeProvider: true);

                Assert.True(dep.HasVisualStudioDependency);
            }
            finally
            {
                // Clean up and delete everything
                using RegistryKey providerKey = dep.BaseKey.OpenSubKey(DependencyProvider.DependenciesKeyRelativePath, writable: true);
                providerKey?.DeleteSubKeyTree(dep.ProviderKeyName);
            }
        }
    }
#pragma warning restore CA1416
}
