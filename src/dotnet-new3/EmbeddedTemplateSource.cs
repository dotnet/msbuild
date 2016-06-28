using System;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;

namespace dotnet_new3
{
    internal class EmbeddedTemplateSource : IConfiguredTemplateSource
    {
        private readonly ITemplateSourceEntry _entry;
        private readonly IConfiguredTemplateSource _parent;
        private readonly ITemplateSource _source;

        public EmbeddedTemplateSource(IConfiguredTemplateSource parent, ITemplateSourceEntry entry, ITemplateSource source)
        {
            _parent = parent;
            _entry = entry;
            _source = source;
        }

        public string Alias => $"{_parent.Alias}::{_entry.FullPath}({_source.GetType().FullName})";

        public string Location => _entry.FullPath;

        public IConfiguredTemplateSource ParentSource => _parent;

        public IDisposable<ITemplateSourceFolder> Root => _source.RootFor(_parent, _entry.FullPath);

        public ITemplateSource Source => _source;

        public Stream OpenFile(string path)
        {
            int lastSep = path.IndexOfAny(new[] { '/', '\\' });
            IDisposable<ITemplateSourceFolder> rootFolder = Root;

            if (lastSep == -1)
            {
                ITemplateSourceFile sourceFile = (ITemplateSourceFile)rootFolder.Value.Children.FirstOrDefault(x => string.Equals(path, x.Name, StringComparison.OrdinalIgnoreCase));
                return new CoDisposableStream(sourceFile.OpenRead(), rootFolder);
            }

            string part = path.Substring(0, lastSep);
            ITemplateSourceFolder sourceFolder = (ITemplateSourceFolder)rootFolder.Value.Children.FirstOrDefault(x => string.Equals(part, x.Name, StringComparison.OrdinalIgnoreCase));

            while (lastSep > 0)
            {
                int start = lastSep + 1;
                lastSep = path.IndexOfAny(new[] { '/', '\\' }, lastSep + 1);

                if (lastSep < 0)
                {
                    part = path.Substring(start);
                    ITemplateSourceFile sourceFile = (ITemplateSourceFile)sourceFolder.Children.FirstOrDefault(x => string.Equals(part, x.Name, StringComparison.OrdinalIgnoreCase));
                    return new CoDisposableStream(sourceFile.OpenRead(), rootFolder);
                }

                part = path.Substring(start, lastSep - start);
                sourceFolder = (ITemplateSourceFolder)sourceFolder.Children.FirstOrDefault(x => string.Equals(part, x.Name, StringComparison.OrdinalIgnoreCase));
            }

            rootFolder.Dispose();
            throw new FileNotFoundException("Unable to find file", path);
        }
    }
}