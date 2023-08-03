// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Microsoft.DotNet.Cli.Utils
{
    /// <summary>
    /// <para>
    /// Represents an installer dependency provider used to manage reference counts against an installer (MSI). 
    /// A dependency provider is an artificial construct introduced in WiX v3.6 to support reference counting installation
    /// packages. MSIs that support this include a custom table, WixDependencyProvider, and a registry entry that writes the 
    /// provider key to SOFTWARE\Classes\Installer\Dependencies\{provider key name} under HKLM or HKCU.
    /// </para>
    /// <para>
    /// Installers like chainers register a reference by writing a value under the Dependents subkey of the provider key. When the
    /// MSI is removed, a custom action is executed to determine if there are any remaining dependents and block the uninstall.
    /// The check can be bypassed by setting IGNOREDEPENDENCIES=ALL. When a chainer removes the MSI, it first removes its
    /// dependent entry. If there are no other dependents, it can proceed to remove the MSI, otherwise it should do nothing.
    /// </para>
    /// </summary>
#if NET
    [SupportedOSPlatform("windows")]
#endif
    public sealed class DependencyProvider
    {
        /// <summary>
        /// The key name used by Visual Studio 2015 and later to register a dependency.
        /// </summary>
        internal const string VisualStudioDependentKeyName = "VS.{AEF703B8-D2CC-4343-915C-F54A30B90937}";

        /// <summary>
        /// The relative path from the <see cref="BaseKey"/> to the Dependencies key.
        /// </summary>
        internal const string DependenciesKeyRelativePath = @"SOFTWARE\Classes\Installer\Dependencies";

        /// <summary>
        /// <see langword="true"/> if the dependency provider is associated with a per-machine 
        /// installation; <see langword="false"/> otherwise.
        /// </summary>
        public readonly bool AllUsers;

        /// <summary>
        /// Returns the root key to use: <see cref="Registry.LocalMachine"/> for per-machine installations or 
        /// <see cref="Registry.CurrentUser"/> for per-user installations.
        /// </summary>
        public readonly RegistryKey BaseKey;

        /// <summary>
        /// Gets all dependents associated with the provider key. The property always enumerates the
        /// provider's dependent entries in the registry.
        /// </summary>
        public IEnumerable<string> Dependents => GetDependents();

        /// <summary>
        /// The path of the key where the provider's dependents are stored, relative to the <see cref="BaseKey"/>.
        /// </summary>
        public readonly string DependentsKeyPath;

        /// <summary>
        /// <see langword="true"/> if Visual Studio 2015 or later is registered as a dependent. Visual Studio only
        /// writes a single entry, regardless of how many instances have taken dependencies.
        /// </summary>
        public bool HasVisualStudioDependency => Dependents.Contains(VisualStudioDependentKeyName);

        /// <summary>
        /// The name of the dependency provider key used for tracking reference counts.
        /// </summary>
        public readonly string ProviderKeyName;

        /// <summary>
        /// The product code of the MSI associated with the dependency provider.
        /// </summary>
        public string ProductCode => GetProductCode();

        /// <summary>
        /// The path of the provider key, relative to the <see cref="BaseKey"/>.
        /// </summary>
        public readonly string ProviderKeyPath;

        /// <summary>
        /// Creates a new <see cref="DependencyProvider"/> instance.
        /// </summary>
        /// <param name="providerKeyName">The name of the dependency provider key.</param>
        /// <param name="allUsers"><see langword="true" /> if the provider belongs to a per-machine installation; 
        /// <see langword="false"/> otherwise.</param>
        public DependencyProvider(string providerKeyName, bool allUsers = true)
        {
            if (providerKeyName is null)
            {
                throw new ArgumentNullException(nameof(providerKeyName));
            }

            if (string.IsNullOrWhiteSpace(providerKeyName))
            {
                throw new ArgumentException($"{nameof(providerKeyName)} cannot be empty.");
            }

            ProviderKeyName = providerKeyName;
            AllUsers = allUsers;
            BaseKey = AllUsers ? Registry.LocalMachine : Registry.CurrentUser;
            ProviderKeyPath = $@"{DependenciesKeyRelativePath}\{ProviderKeyName}";
            DependentsKeyPath = $@"{ProviderKeyPath}\Dependents";
        }

        /// <summary>
        /// Adds the specified dependent to the provider key. The dependent is stored as a subkey under the Dependents key of
        /// the provider."/>
        /// </summary>
        /// <param name="dependent">The dependent to add.</param>
        public void AddDependent(string dependent)
        {
            if (dependent is null)
            {
                throw new ArgumentNullException(nameof(dependent));
            }

            if (string.IsNullOrWhiteSpace(dependent))
            {
                throw new ArgumentException($"{nameof(dependent)} cannot be empty.");
            }

            using RegistryKey dependentsKey = BaseKey.CreateSubKey(Path.Combine(DependentsKeyPath, dependent), writable: true);
        }

        /// <summary>
        /// Remove the specified dependent from the provider key. Optionally, if this is the final dependent,
        /// the provider key can also be removed. This is typically done during an uninstall.
        /// </summary>
        /// <param name="dependent">The dependent to remove.</param>
        /// <param name="removeProvider">When <see langword="true"/>, delete the provider key if the dependent being
        /// removed is the last dependent.</param>
        public void RemoveDependent(string dependent, bool removeProvider)
        {
            if (dependent is null)
            {
                throw new ArgumentNullException(nameof(dependent));
            }

            if (string.IsNullOrWhiteSpace(dependent))
            {
                throw new ArgumentException($"{nameof(dependent)} cannot be empty.");
            }

            using RegistryKey dependentsKey = BaseKey.OpenSubKey(DependentsKeyPath, writable: true);
            dependentsKey?.DeleteSubKeyTree(dependent);

            if ((removeProvider) && (Dependents.Count() == 0))
            {
                using RegistryKey providerKey = BaseKey.OpenSubKey(DependenciesKeyRelativePath, writable: true);
                providerKey?.DeleteSubKeyTree(ProviderKeyName);
            }
        }

        /// <summary>
        /// Gets all the dependents associated with the provider key.
        /// </summary>
        /// <returns>All dependents of the provider key.</returns>
        private IEnumerable<string> GetDependents()
        {
            using RegistryKey dependentsKey = BaseKey.OpenSubKey(DependentsKeyPath);

            return dependentsKey?.GetSubKeyNames() ?? Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets the ProductCode associated with this dependency provider. The ProductCode is stored in the default
        /// value.
        /// </summary>
        /// <returns>The ProductCode associated with this dependency provider or <see langword="null"/> if it does not exist.</returns>
        private string GetProductCode()
        {
            using RegistryKey providerKey = BaseKey.OpenSubKey(ProviderKeyPath);
            return providerKey?.GetValue(null) as string ?? null;
        }
    }
}
