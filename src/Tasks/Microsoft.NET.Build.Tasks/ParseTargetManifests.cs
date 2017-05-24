// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Parses the target manifest files into MSBuild Items.
    /// </summary>
    public sealed class ParseTargetManifests : TaskBase
    {
        public string TargetManifestFiles { get; set; }

        [Output]
        public ITaskItem[] RuntimeStorePackages { get; private set; }

        protected override void ExecuteCore()
        {
            string[] targetManifestFileList = TargetManifestFiles?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            if (targetManifestFileList != null && targetManifestFileList.Length > 0)
            {
                var runtimeStorePackages = new Dictionary<PackageIdentity, StringBuilder>();
                foreach (var manifestFile in targetManifestFileList)
                {
                    Log.LogMessage(MessageImportance.Low, string.Format(CultureInfo.CurrentCulture, Strings.ParsingFiles, manifestFile));
                    var packagesSpecified = StoreArtifactParser.Parse(manifestFile);
                    var targetManifestFileName = Path.GetFileName(manifestFile);

                    foreach (var pkg in packagesSpecified)
                    {
                        Log.LogMessage(MessageImportance.Low, string.Format(CultureInfo.CurrentCulture, Strings.PackageInfoLog, pkg.Id, pkg.Version));
                        StringBuilder fileList;
                        if (runtimeStorePackages.TryGetValue(pkg, out fileList))
                        {
                            fileList.Append($";{targetManifestFileName}");
                        }
                        else
                        {
                            runtimeStorePackages.Add(pkg, new StringBuilder(targetManifestFileName));
                        }
                    }
                }

                var resultPackages = new List<ITaskItem>();
                foreach (var storeEntry in runtimeStorePackages)
                {
                    string packageName = storeEntry.Key.Id;
                    string packageVersion = storeEntry.Key.Version.ToNormalizedString();

                    TaskItem item = new TaskItem($"{packageName}/{packageVersion}");
                    item.SetMetadata(MetadataKeys.PackageName, packageName);
                    item.SetMetadata(MetadataKeys.PackageVersion, packageVersion);
                    item.SetMetadata(MetadataKeys.RuntimeStoreManifestNames, storeEntry.Value.ToString());

                    resultPackages.Add(item);
                }

                RuntimeStorePackages = resultPackages.ToArray();
            }
        }
    }
}
