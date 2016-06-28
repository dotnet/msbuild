using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;

namespace dotnet_new3
{
    public static class ConfiguredTemplateSourceHelper
    {
        public static IReadOnlyList<IConfiguredTemplateSource> Scan(IReadOnlyList<IConfiguredTemplateSource> configuredSources, IEnumerable<ITemplateSource> sources)
        {
            List<ITemplateSource> fileEmbeddableSources = new List<ITemplateSource>();

            foreach (ITemplateSource src in sources)
            {
                if (src.IsEmbeddable)
                {
                    fileEmbeddableSources.Add(src);
                }
            }

            Dictionary<string, IConfiguredTemplateSource> result = configuredSources.ToDictionary(x => x.Location, x => x, StringComparer.OrdinalIgnoreCase);

            foreach (IConfiguredTemplateSource configuredSource in configuredSources)
            {
                if (configuredSource.Source.CanHostEmbeddedSources)
                {
                    Scan(configuredSource, fileEmbeddableSources, result);
                }
            }

            return result.Values.ToList();
        }

        private static void Scan(IConfiguredTemplateSource configuredSource, IReadOnlyList<ITemplateSource> fileEmbeddableSources, Dictionary<string, IConfiguredTemplateSource> configuredSources)
        {
            using (IDisposable<ITemplateSourceFolder> rootFolder = configuredSource.Root)
            {
                foreach (ITemplateSourceFile entry in rootFolder.Value.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    if (entry.Kind == TemplateSourceEntryKind.File)
                    {
                        foreach (ITemplateSource source in fileEmbeddableSources)
                        {
                            if (source.CanHandle(configuredSource, entry.FullPath))
                            {
                                IConfiguredTemplateSource embedded = new EmbeddedTemplateSource(configuredSource, entry, source);
                                configuredSources[embedded.Location] = embedded;

                                if (embedded.Source.CanHostEmbeddedSources)
                                {
                                    Scan(embedded, fileEmbeddableSources, configuredSources);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
