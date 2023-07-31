// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.New
{
    /// <summary>
    /// Returns list of *.nupkg files from C:\Program Files\dotnet\templates\x.x.x.x\ (on Windows) to be installed.
    /// </summary>
    internal sealed class BuiltInTemplatePackageProvider : ITemplatePackageProvider
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;

        public BuiltInTemplatePackageProvider(BuiltInTemplatePackageProviderFactory factory, IEngineEnvironmentSettings settings)
        {
            Factory = factory;
            _environmentSettings = settings;
        }

        public ITemplatePackageProviderFactory Factory { get; }

#pragma warning disable CS0067
        /// <summary>
        /// We don't trigger this event, we could complicate our life with FileSystemWatcher.
        /// But since "dotnet new" is short lived process is not worth it, plus it would cause
        /// some perf hit...
        /// </summary>
        public event Action TemplatePackagesChanged;
#pragma warning restore CS0067

        public Task<IReadOnlyList<ITemplatePackage>> GetAllTemplatePackagesAsync(CancellationToken cancellationToken)
        {
            var packages = new List<ITemplatePackage>();
            foreach (string templateFolder in GetTemplateFolders(_environmentSettings))
            {
                foreach (string nupkgPath in Directory.EnumerateFiles(templateFolder, "*.nupkg", SearchOption.TopDirectoryOnly))
                {
                    packages.Add(new TemplatePackage(this, nupkgPath, File.GetLastWriteTime(nupkgPath)));
                }
            }
            return Task.FromResult<IReadOnlyList<ITemplatePackage>>(packages);
        }

        private static IEnumerable<string> GetTemplateFolders(IEngineEnvironmentSettings environmentSettings)
        {
            var templateFoldersToInstall = new List<string>();

            var sdkDirectory = Path.GetDirectoryName(typeof(Microsoft.DotNet.Cli.DotnetFiles).Assembly.Location);
            var dotnetRootPath = Path.GetDirectoryName(Path.GetDirectoryName(sdkDirectory));

            // First grab templates from dotnet\templates\M.m folders, in ascending order, up to our version
            string templatesRootFolder = Path.GetFullPath(Path.Combine(dotnetRootPath, "templates"));
            if (Directory.Exists(templatesRootFolder))
            {
                IReadOnlyDictionary<string, SemanticVersion> parsedNames = GetVersionDirectoriesInDirectory(templatesRootFolder);
                IList<string> versionedFolders = GetBestVersionsByMajorMinor(parsedNames);

                templateFoldersToInstall.AddRange(versionedFolders
                    .Select(versionedFolder => Path.Combine(templatesRootFolder, versionedFolder)));
            }

            // Now grab templates from our base folder, if present.
            string templatesDir = Path.Combine(sdkDirectory, "Templates");
            if (Directory.Exists(templatesDir))
            {
                templateFoldersToInstall.Add(templatesDir);
            }

            return templateFoldersToInstall;
        }

        // Returns a dictionary of fileName -> Parsed version info
        // including all the directories in the input directory whose names are parse-able as versions.
        private static IReadOnlyDictionary<string, SemanticVersion> GetVersionDirectoriesInDirectory(string fullPath)
        {
            var versionFileInfo = new Dictionary<string, SemanticVersion>();

            foreach (string directory in Directory.EnumerateDirectories(fullPath, "*.*", SearchOption.TopDirectoryOnly))
            {
                if (SemanticVersion.TryParse(Path.GetFileName(directory), out SemanticVersion versionInfo))
                {
                    versionFileInfo.Add(directory, versionInfo);
                }
            }

            return versionFileInfo;
        }

        private static IList<string> GetBestVersionsByMajorMinor(IReadOnlyDictionary<string, SemanticVersion> versionDirInfo)
        {
            IDictionary<string, (string path, SemanticVersion version)> bestVersionsByBucket = new Dictionary<string, (string path, SemanticVersion version)>();

            Version sdkVersion = typeof(Microsoft.DotNet.Cli.NewCommandParser).Assembly.GetName().Version;
            foreach (KeyValuePair<string, SemanticVersion> dirInfo in versionDirInfo)
            {
                var majorMinorDirVersion = new Version(dirInfo.Value.Major, dirInfo.Value.Minor);
                // restrict the results to not include from higher versions of the runtime/templates then the SDK
                if (majorMinorDirVersion <= sdkVersion)
                {
                    string coreAppVersion = $"{dirInfo.Value.Major}.{dirInfo.Value.Minor}";
                    if (!bestVersionsByBucket.TryGetValue(coreAppVersion, out (string path, SemanticVersion version) currentHighest)
                        || dirInfo.Value.CompareTo(currentHighest.version) > 0)
                    {
                        bestVersionsByBucket[coreAppVersion] = (dirInfo.Key, dirInfo.Value);
                    }
                }
            }

            return bestVersionsByBucket.OrderBy(x => x.Key).Select(x => x.Value.path).ToList();
        }
    }
}
