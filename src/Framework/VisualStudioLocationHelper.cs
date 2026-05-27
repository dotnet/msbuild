// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
#if FEATURE_WINDOWSINTEROP && FEATURE_VISUALSTUDIOSETUP
using Microsoft.Build.Shared.VisualStudio;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
#endif

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Helper class that queries the Visual Studio Setup Configuration COM API for instances
    /// of Visual Studio installed on the machine. Will not include anything before VS "15".
    /// </summary>
    /// <remarks>
    /// Uses manually-defined struct-based COM declarations under <c>Shared/VisualStudio/</c>
    /// (matching the CsWin32 struct-based COM pattern used elsewhere in the repo) so this
    /// project does not need a reference to the legacy
    /// <c>Microsoft.VisualStudio.Setup.Configuration.Interop</c> RCW package. All COM pointer
    /// lifetimes are managed via <c>ComScope&lt;T&gt;</c>.
    /// </remarks>
    internal static unsafe class VisualStudioLocationHelper
    {
        /// <summary>
        /// Query the Visual Studio setup API to get instances of Visual Studio installed
        /// on the machine. Will not include anything before Visual Studio "15".
        /// </summary>
        /// <returns>Enumerable list of Visual Studio instances</returns>
        internal static IList<VisualStudioInstance> GetInstances()
        {
            var validInstances = new List<VisualStudioInstance>();

#if FEATURE_WINDOWSINTEROP && FEATURE_VISUALSTUDIOSETUP
            // No try/catch around the COM call chain by design: AcquireSetupConfiguration2
            // and PopulateInstances check HRESULTs inline and return gracefully on failure.
            // If a future maintainer introduces a ThrowOnFailure() in this path, add the
            // matching catch — "empty list" is the documented behaviour for absent VS.
            try
            {
                using ComScope<ISetupConfiguration2> config = AcquireSetupConfiguration2();
                if (!config.IsNull)
                {
                    PopulateInstances(config.Pointer, validInstances);
                }
            }
            catch (DllNotFoundException)
            {
                // The Setup Configuration native helper isn't on disk — VS "15" or newer
                // is likely not installed. Fall through with whatever has been collected.
            }
#endif

            return validInstances;
        }

#if FEATURE_WINDOWSINTEROP && FEATURE_VISUALSTUDIOSETUP
        /// <summary>
        /// Try to obtain a top-level <see cref="ISetupConfiguration2"/> pointer. First attempts
        /// <c>CoCreateInstance</c> on the registered coclass; on
        /// <see cref="HRESULT.REGDB_E_CLASSNOTREG"/> falls back to the app-local
        /// <c>GetSetupConfiguration</c> entry point exported by
        /// <c>Microsoft.VisualStudio.Setup.Configuration.Native.dll</c> (which requires that
        /// helper DLL to be loadable but doesn't require COM registration). Any other COM
        /// failure is returned as a null scope — the caller treats absent VS Setup as an
        /// empty result, not an error.
        /// </summary>
        private static ComScope<ISetupConfiguration2> AcquireSetupConfiguration2()
        {
            Guid clsid = SetupConfiguration.CLSID_SetupConfiguration;
            Guid iidConfig2 = ISetupConfiguration2.IID_ISetupConfiguration2;

            ComScope<ISetupConfiguration2> config2 = new();
            HRESULT hr = PInvoke.CoCreateInstance(&clsid, pUnkOuter: null, CLSCTX.CLSCTX_INPROC_SERVER, &iidConfig2, config2);
            if (hr.Succeeded)
            {
                return config2;
            }

            if (hr != HRESULT.REGDB_E_CLASSNOTREG)
            {
                // Some other COM failure. The caller swallows errors, so signal "no result"
                // by returning an empty scope rather than constructing a COMException only
                // to have it discarded. CoCreateInstance must null the out-parameter on
                // failure per the COM spec, but Dispose defensively so the ownership model
                // is explicit even against a buggy COM implementation.
                config2.Dispose();
                return default;
            }

            // App-local fallback: the helper DLL is present but not COM-registered.
            using ComScope<ISetupConfiguration> config1 = new();
            int rawHr = SetupConfiguration.GetSetupConfiguration(config1, IntPtr.Zero);
            if (rawHr < 0 || config1.IsNull)
            {
                return default;
            }

            HRESULT qiHr = config1.Pointer->QueryInterface(&iidConfig2, config2);
            if (qiHr.Failed)
            {
                config2.Dispose();
                return default;
            }

            return config2;
        }

        /// <summary>
        /// Enumerate every instance the configuration exposes and append the complete ones
        /// to <paramref name="results"/>. Per-instance failures are silent — a partial/broken
        /// install simply does not appear in the output.
        /// </summary>
        private static void PopulateInstances(ISetupConfiguration2* config, List<VisualStudioInstance> results)
        {
            using ComScope<IEnumSetupInstances> enumInstances = new();
            if (config->EnumAllInstances(enumInstances).Failed || enumInstances.IsNull)
            {
                return;
            }

            while (true)
            {
                using ComScope<ISetupInstance> instance = new();
                uint fetched = 0;
                if (enumInstances.Pointer->Next(1, instance, &fetched).Failed
                    || fetched == 0
                    || instance.IsNull)
                {
                    break;
                }

                if (TryReadInstance(instance.Pointer, out VisualStudioInstance vs))
                {
                    results.Add(vs);
                }
            }
        }

        /// <summary>
        /// Read one <see cref="ISetupInstance"/>: filter by <see cref="InstanceState.Complete"/>,
        /// parse the version string, and capture the display name and installation path. Returns
        /// <see langword="false"/> when the instance should be skipped (incomplete install,
        /// missing v2 interface, or unparseable version).
        /// </summary>
        private static bool TryReadInstance(ISetupInstance* instance, out VisualStudioInstance result)
        {
            result = null!;

            // QI to ISetupInstance2 so we can read the install state. Instances that lack v2 are
            // ignored — only VS 15 and newer ship the v2 interface, which matches what the old
            // RCW-based code required for `GetState`.
            using ComScope<ISetupInstance2> instance2 = new();
            Guid iidInstance2 = ISetupInstance2.IID_ISetupInstance2;
            if (instance->QueryInterface(&iidInstance2, instance2).Failed || instance2.IsNull)
            {
                return false;
            }

            InstanceState state = default;
            if (instance2.Pointer->GetState(&state).Failed || state != InstanceState.Complete)
            {
                return false;
            }

            // BSTR is IDisposable: `using` calls SysFreeString in place at scope exit, so we
            // don't need a try/finally around each of the three out-params.
            using BSTR versionBstr = default;
            using BSTR nameBstr = default;
            using BSTR pathBstr = default;

            if (instance->GetInstallationVersion(&versionBstr).Failed
                || !Version.TryParse(versionBstr.ToString(), out Version version))
            {
                return false;
            }

            if (instance->GetDisplayName(0, &nameBstr).Failed
                || instance->GetInstallationPath(&pathBstr).Failed)
            {
                return false;
            }

            result = new VisualStudioInstance(nameBstr.ToString(), pathBstr.ToString(), version);
            return true;
        }
#endif
    }

    /// <summary>
    /// Wrapper class to represent an installed instance of Visual Studio.
    /// </summary>
    internal class VisualStudioInstance
    {
        /// <summary>
        /// Version of the Visual Studio Instance
        /// </summary>
        internal Version Version { get; }

        /// <summary>
        /// Path to the Visual Studio installation
        /// </summary>
        internal string Path { get; }

        /// <summary>
        /// Full name of the Visual Studio instance with SKU name
        /// </summary>
        internal string Name { get; }

        internal VisualStudioInstance(string name, string path, Version version)
        {
            Name = name;
            Path = path;
            Version = version;
        }
    }
}
