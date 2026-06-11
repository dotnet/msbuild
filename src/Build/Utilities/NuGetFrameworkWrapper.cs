// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Wraps the NuGet.Frameworks assembly, which is referenced by reflection and loaded into a separate AppDomain for performance.
    /// </summary>
    internal sealed partial class NuGetFrameworkWrapper : MarshalByRefObject
    {
        private const string NuGetFrameworksAssemblyName = "NuGet.Frameworks";
        private const string NuGetFrameworksFileName = NuGetFrameworksAssemblyName + ".dll";

        /// <summary>
        /// Methods, properties, and objects used from the NuGet.Frameworks assembly.
        /// </summary>
        private MethodInfo ParseMethod;
        private MethodInfo IsCompatibleMethod;
        private object DefaultCompatibilityProvider;
        private PropertyInfo FrameworkProperty;
        private PropertyInfo VersionProperty;
        private PropertyInfo PlatformProperty;
        private PropertyInfo PlatformVersionProperty;
        private PropertyInfo AllFrameworkVersionsProperty;

        /// <summary>
        /// Public constructor for cross-domain activation only. Use <see cref="CreateInstance"/> to instantiate.
        /// </summary>
        public NuGetFrameworkWrapper()
        { }

        /// <summary>
        /// Initialized this instance. May run in a separate AppDomain.
        /// </summary>
        /// <param name="assemblyName">The NuGet.Frameworks to be loaded or null to load by path.</param>
        /// <param name="assemblyFilePath">The file path from which NuGet.Frameworks should be loaded of <paramref name="assemblyName"/> is null.</param>
        public void Initialize(AssemblyName assemblyName, string assemblyFilePath)
        {
            Assembly NuGetAssembly;
            if (assemblyName != null)
            {
                // This will load the assembly into the default load context if possible, and fall back to LoadFrom context.
                NuGetAssembly = Assembly.Load(assemblyName);
            }
            else
            {
                NuGetAssembly = Assembly.LoadFile(assemblyFilePath);
            }

            var NuGetFramework = NuGetAssembly.GetType("NuGet.Frameworks.NuGetFramework");
            var NuGetFrameworkCompatibilityProvider = NuGetAssembly.GetType("NuGet.Frameworks.CompatibilityProvider");
            var NuGetFrameworkDefaultCompatibilityProvider = NuGetAssembly.GetType("NuGet.Frameworks.DefaultCompatibilityProvider");
            ParseMethod = NuGetFramework.GetMethod("Parse", [typeof(string)]);
            IsCompatibleMethod = NuGetFrameworkCompatibilityProvider.GetMethod("IsCompatible");
            DefaultCompatibilityProvider = NuGetFrameworkDefaultCompatibilityProvider.GetMethod("get_Instance").Invoke(null, Array.Empty<object>());
            FrameworkProperty = NuGetFramework.GetProperty("Framework");
            VersionProperty = NuGetFramework.GetProperty("Version");
            PlatformProperty = NuGetFramework.GetProperty("Platform");
            PlatformVersionProperty = NuGetFramework.GetProperty("PlatformVersion");
            AllFrameworkVersionsProperty = NuGetFramework.GetProperty("AllFrameworkVersions");
        }

        private object Parse(string tfm)
        {
            return ParseMethod.Invoke(null, [tfm]);
        }

        public string GetTargetFrameworkIdentifier(string tfm)
        {
            return FrameworkProperty.GetValue(Parse(tfm)) as string;
        }

        public string GetTargetFrameworkVersion(string tfm, int minVersionPartCount)
        {
            var version = VersionProperty.GetValue(Parse(tfm)) as Version;
            return GetNonZeroVersionParts(version, minVersionPartCount);
        }

        public string GetTargetPlatformIdentifier(string tfm)
        {
            return PlatformProperty.GetValue(Parse(tfm)) as string;
        }

        public string GetTargetPlatformVersion(string tfm, int minVersionPartCount)
        {
            var version = PlatformVersionProperty.GetValue(Parse(tfm)) as Version;
            return GetNonZeroVersionParts(version, minVersionPartCount);
        }

        public bool IsCompatible(string target, string candidate)
        {
            return Convert.ToBoolean(IsCompatibleMethod.Invoke(DefaultCompatibilityProvider, [Parse(target), Parse(candidate)]));
        }

        public string FilterTargetFrameworks(string incoming, string filter)
        {
            IEnumerable<(string originalTfm, object parsedTfm)> incomingFrameworks = ParseTfms(incoming);
            IEnumerable<(string originalTfm, object parsedTfm)> filterFrameworks = ParseTfms(filter);
            StringBuilder tfmList = new StringBuilder();

            // An incoming target framework from 'incoming' is kept if it is compatible with any of the desired target frameworks on 'filter'
            foreach (var l in incomingFrameworks)
            {
                if (filterFrameworks.Any(r =>
                        (FrameworkProperty.GetValue(l.parsedTfm) as string).Equals(FrameworkProperty.GetValue(r.parsedTfm) as string, StringComparison.OrdinalIgnoreCase) &&
                        (((Convert.ToBoolean(AllFrameworkVersionsProperty.GetValue(l.parsedTfm))) && (Convert.ToBoolean(AllFrameworkVersionsProperty.GetValue(r.parsedTfm)))) ||
                         ((VersionProperty.GetValue(l.parsedTfm) as Version) == (VersionProperty.GetValue(r.parsedTfm) as Version)))))
                {
                    if (tfmList.Length == 0)
                    {
                        tfmList.Append(l.originalTfm);
                    }
                    else
                    {
                        tfmList.Append($";{l.originalTfm}");
                    }
                }
            }

            return tfmList.ToString();

            IEnumerable<(string originalTfm, object parsedTfm)> ParseTfms(string desiredTargetFrameworks)
            {
                return desiredTargetFrameworks.Split([';'], StringSplitOptions.RemoveEmptyEntries).Select(tfm =>
                {
                    (string originalTfm, object parsedTfm) parsed = (tfm, Parse(tfm));
                    return parsed;
                });
            }
        }

        /// <summary>
        /// A null-returning InitializeLifetimeService to give the proxy an infinite lease time.
        /// </summary>
        public override object InitializeLifetimeService() => null;

        /// <summary>
        /// Creates <see cref="AppDomainSetup"/> suitable for loading Microsoft.Build, NuGet.Frameworks, and dependencies.
        /// See https://github.com/dotnet/msbuild/blob/main/documentation/NETFramework-NGEN.md#nugetframeworks for the motivation
        /// to use a separate AppDomain.
        /// </summary>
        private static AppDomainSetup CreateAppDomainSetup(AssemblyName assemblyName, string assemblyPath)
        {
            byte[] publicKeyToken = assemblyName.GetPublicKeyToken();
            StringBuilder publicKeyTokenString = new(publicKeyToken.Length * 2);
            for (int i = 0; i < publicKeyToken.Length; i++)
            {
                publicKeyTokenString.Append(publicKeyToken[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            // Create an app.config for the AppDomain. We expect the AD to host the currently executing assembly Microsoft.Build,
            // NuGet.Frameworks, and Framework assemblies. It is important to use the same binding redirects that were used when
            // NGENing MSBuild for the native images to be used.
            string configuration = $"""
<?xml version="1.0" encoding="utf-8"?>
  <configuration>
    <runtime>
      <DisableFXClosureWalk enabled="true" />
      <DeferFXClosureWalk enabled="true" />
      <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
        {(Environment.Is64BitProcess ? _bindingRedirects64 : _bindingRedirects32)}
        <dependentAssembly>
          <assemblyIdentity name="{NuGetFrameworksAssemblyName}" publicKeyToken="{publicKeyTokenString}" culture="{assemblyName.CultureName}" />
          <codeBase version="{assemblyName.Version}" href="{assemblyPath}" />
        </dependentAssembly>
      </assemblyBinding>
    </runtime>
  </configuration>
""";

            AppDomainSetup appDomainSetup = AppDomain.CurrentDomain.SetupInformation;
            appDomainSetup.SetConfigurationBytes(Encoding.UTF8.GetBytes(configuration));
            return appDomainSetup;
        }

        public static NuGetFrameworkWrapper CreateInstance()
        {
            // Resolve the location of the NuGet.Frameworks assembly
            string assemblyDirectory = BuildEnvironmentHelper.Instance.Mode == BuildEnvironmentMode.VisualStudio ?
                Path.Combine(BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory, "Common7", "IDE", "CommonExtensions", "Microsoft", "NuGet") :
                BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory;

            string assemblyPath = Path.Combine(assemblyDirectory, NuGetFrameworksFileName);

            NuGetFrameworkWrapper instance = null;
            AssemblyName assemblyName = null;
            if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_10) &&
                (BuildEnvironmentHelper.Instance.RunningInMSBuildExe || BuildEnvironmentHelper.Instance.RunningInVisualStudio))
            {
                // If we are running in MSBuild.exe or VS, we can load the assembly with Assembly.Load, which enables
                // the runtime to bind to the native image, eliminating some non-trivial JITting cost. Devenv.exe knows how to
                // load the assembly by name. In MSBuild.exe, however, we don't know the version of the assembly statically so
                // we create a separate AppDomain with the right binding redirects.
                try
                {
                    assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                    if (assemblyName != null && BuildEnvironmentHelper.Instance.RunningInMSBuildExe)
                    {
                        AppDomainSetup appDomainSetup = CreateAppDomainSetup(assemblyName, assemblyPath);
                        if (appDomainSetup != null)
                        {
                            AppDomain appDomain = AppDomain.CreateDomain(nameof(NuGetFrameworkWrapper), null, appDomainSetup);
                            instance = (NuGetFrameworkWrapper)appDomain.CreateInstanceAndUnwrap(Assembly.GetExecutingAssembly().FullName, typeof(NuGetFrameworkWrapper).FullName);
                        }
                    }
                }
                catch
                {
                    // If anything goes wrong just fall back to loading into current AD by path.
                    instance = null;
                    assemblyName = null;
                }
            }
            try
            {
                instance ??= new NuGetFrameworkWrapper();
                instance.Initialize(assemblyName, assemblyPath);

                return instance;
            }
            catch (Exception ex)
            {
                throw new InternalErrorException(string.Format(AssemblyResources.GetString("NuGetAssemblyNotFound"), assemblyDirectory), ex);
            }
        }
    }
}

#else

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.Frameworks;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Wraps the NuGet.Frameworks assembly. On .NET (Core), <see cref="NuGet.Frameworks"/>
    /// is referenced directly at compile time and called without reflection, avoiding the
    /// per-call <c>MethodInfo.Invoke</c> cost incurred by the .NET Framework code path.
    /// </summary>
    internal sealed partial class NuGetFrameworkWrapper
    {
        private NuGetFrameworkWrapper()
        { }

        public static NuGetFrameworkWrapper CreateInstance() => new();

        private static NuGetFramework Parse(string tfm) => NuGetFramework.Parse(tfm);

        public string GetTargetFrameworkIdentifier(string tfm) => Parse(tfm).Framework;

        public string GetTargetFrameworkVersion(string tfm, int minVersionPartCount) =>
            GetNonZeroVersionParts(Parse(tfm).Version, minVersionPartCount);

        public string GetTargetPlatformIdentifier(string tfm) => Parse(tfm).Platform;

        public string GetTargetPlatformVersion(string tfm, int minVersionPartCount) =>
            GetNonZeroVersionParts(Parse(tfm).PlatformVersion, minVersionPartCount);

        public bool IsCompatible(string target, string candidate) =>
            DefaultCompatibilityProvider.Instance.IsCompatible(Parse(target), Parse(candidate));

        public string FilterTargetFrameworks(string incoming, string filter)
        {
            IEnumerable<(string originalTfm, NuGetFramework parsedTfm)> incomingFrameworks = ParseTfms(incoming);
            IEnumerable<(string originalTfm, NuGetFramework parsedTfm)> filterFrameworks = ParseTfms(filter);
            StringBuilder tfmList = new StringBuilder();

            // An incoming target framework from 'incoming' is kept if it is compatible with any of the desired target frameworks on 'filter'
            foreach (var l in incomingFrameworks)
            {
                if (filterFrameworks.Any(r =>
                        l.parsedTfm.Framework.Equals(r.parsedTfm.Framework, StringComparison.OrdinalIgnoreCase) &&
                        ((l.parsedTfm.AllFrameworkVersions && r.parsedTfm.AllFrameworkVersions) ||
                         l.parsedTfm.Version == r.parsedTfm.Version)))
                {
                    if (tfmList.Length == 0)
                    {
                        tfmList.Append(l.originalTfm);
                    }
                    else
                    {
                        tfmList.Append($";{l.originalTfm}");
                    }
                }
            }

            return tfmList.ToString();

            static IEnumerable<(string originalTfm, NuGetFramework parsedTfm)> ParseTfms(string desiredTargetFrameworks)
            {
                return desiredTargetFrameworks.Split([';'], StringSplitOptions.RemoveEmptyEntries).Select(tfm =>
                {
                    (string originalTfm, NuGetFramework parsedTfm) parsed = (tfm, Parse(tfm));
                    return parsed;
                });
            }
        }
    }
}

#endif
