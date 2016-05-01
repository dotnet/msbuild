using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mutant.Chicken.Abstractions;

namespace dotnet_new3
{
    internal class ConfiguredTemplateSource : IConfiguredTemplateSource
    {
        private readonly string _location;

        public ConfiguredTemplateSource(ITemplateSource source, string alias, string location)
        {
            Source = source;
            _location = location;
            Alias = alias;
        }

        public string Alias { get; }

        public IDisposable<ITemplateSourceFolder> Root => Source.RootFor(_location);

        public string Location => _location;

        public ITemplateSource Source { get; }

        public Stream OpenFile(string path)
        {
            int lastSep = path.IndexOfAny(new[] {'/', '\\'});
            IDisposable<ITemplateSourceFolder> rootFolder = Root;

            if (lastSep == -1)
            {
                ITemplateSourceFile sourceFile = (ITemplateSourceFile) rootFolder.Value.Children.FirstOrDefault(x => string.Equals(path, x.Name, StringComparison.OrdinalIgnoreCase));
                return new CoDisposableStream(sourceFile.OpenRead(), rootFolder);
            }

            string part = path.Substring(0, lastSep);
            ITemplateSourceFolder sourceFolder = (ITemplateSourceFolder) rootFolder.Value.Children.FirstOrDefault(x => string.Equals(part, x.Name, StringComparison.OrdinalIgnoreCase));

            while (lastSep > 0)
            {
                int start = lastSep + 1;
                lastSep = path.IndexOfAny(new[] {'/', '\\'}, lastSep + 1);

                if (lastSep < 0)
                {
                    part = path.Substring(start);
                    ITemplateSourceFile sourceFile = (ITemplateSourceFile) sourceFolder.Children.FirstOrDefault(x => string.Equals(part, x.Name, StringComparison.OrdinalIgnoreCase));
                    return new CoDisposableStream(sourceFile.OpenRead(), rootFolder);
                }

                part = path.Substring(start, lastSep - start);
                sourceFolder = (ITemplateSourceFolder) sourceFolder.Children.FirstOrDefault(x => string.Equals(part, x.Name, StringComparison.OrdinalIgnoreCase));
            }

            rootFolder.Dispose();
            throw new FileNotFoundException("Unable to find file", path);
        }
    }
}