// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Runtime.CompilerServices;


#if USE_SYSTEM_TEXT_JSON
using System.Text.Json;
#else
using JsonTokenType = Newtonsoft.Json.JsonToken;
#endif

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    partial class WorkloadManifestReader
    {
        private static LocalizationCatalog ReadLocalizationCatalog(ref Utf8JsonStreamReader reader)
            => new(ReadStringDictionary(ref reader));

        public static string? GetLocalizationCatalogFilePath(string manifestFilePath, CultureInfo? culture = null)
            => GetLocalizationCatalogFilePath(manifestFilePath, culture ?? CultureInfo.CurrentUICulture, _fileExists);

        static readonly Func<string, bool> _fileExists = File.Exists;

        // fileExists param is for tests
        internal static string? GetLocalizationCatalogFilePath(string manifestFilePath, CultureInfo culture, Func<string, bool> fileExists)
        {
            string localizationDir = GetLocalizationDirectory(manifestFilePath);
            do
            {
                var catalog = Path.Combine(localizationDir, $"WorkloadManifest.{culture.Name}.json");
                if (fileExists(catalog))
                {
                    return catalog;
                }

                culture = culture.Parent;
            }
            while (culture != CultureInfo.InvariantCulture);
            return null;
        }

        public static Stream? TryOpenLocalizationCatalogForManifest(string manifestFilePath, CultureInfo? culture = null)
        {
            var localizationPath = GetLocalizationCatalogFilePath(manifestFilePath, culture);
            if (localizationPath != null)
            {
                return File.OpenRead(localizationPath);
            }
            return null;
        }

        public static IEnumerable<(CultureInfo culture, string filePath)> EnumerateLocalizations(string manifestFilePath)
        {
            string localizationDir = GetLocalizationDirectory(manifestFilePath);
            if (Directory.Exists(localizationDir))
            {
                foreach (var filePath in Directory.EnumerateFiles(localizationDir, "WorkloadManifest.*.json"))
                {
                    var cultureName = Path.GetFileNameWithoutExtension(filePath).Substring("WorkloadManifest.".Length);
                    var culture = CultureInfo.GetCultureInfo(cultureName);
                    yield return (culture, filePath);
                }
            }
        }

        private static string GetLocalizationDirectory(string manifestFilePath)
        {
            string manifestDir = Path.GetDirectoryName(manifestFilePath) ?? throw new ArgumentException(nameof(manifestFilePath));
            string localizationDir = Path.Combine(manifestDir, "localize");
            return localizationDir;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string? ReadStringLocalized(ref Utf8JsonStreamReader reader, LocalizationCatalog? localizationCatalog, string id)
        {
            ConsumeToken(ref reader, JsonTokenType.String);
            return localizationCatalog?.Localize(id) ?? reader.GetString();
        }

        class LocalizationCatalog
        {
            readonly Dictionary<string, string> _catalog;

            public LocalizationCatalog(Dictionary<string, string> catalog)
            {
                _catalog = catalog;
            }

            public string Localize(string id)
            {
                if (_catalog.TryGetValue(id, out string? localizedMessage))
                {
                    return localizedMessage;
                }
                return id;
            }
        }
    }
}
