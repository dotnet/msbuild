using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.TemplateEngine.Abstractions;

namespace dotnet_new3
{
    public class ZipArchiveTemplateSource : ITemplateSource
    {
        public bool IsEmbeddable => true;

        public bool CanHostEmbeddedSources => false;

        public string Name => "ZipArchive";

        public bool CanHandle(string location)
        {
            try
            {
                if (!File.Exists(location))
                {
                    return false;
                }

                using (ZipFile.OpenRead(location))
                {
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool CanHandle(IConfiguredTemplateSource source, string location)
        {

            try
            {
                string extension = Path.GetExtension(location).ToUpperInvariant();
                switch (extension)
                {
                    case ".ZIP":
                    case ".VSIX":
                    case ".NUPKG":
                        break;
                    default:
                        return false;
                }

                using (Stream fileData = source.Root.Value.OpenFile(location))
                using (new ZipArchive(fileData, ZipArchiveMode.Read, true))
                {
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public IDisposable<ITemplateSourceFolder> RootFor(string location)
        {
            if (!File.Exists(location))
            {
                return Directory.Empty.NoDispose();
            }

            return new Directory(null, "", "", () => ZipFile.OpenRead(location)).NoDispose();
        }

        public IDisposable<ITemplateSourceFolder> RootFor(IConfiguredTemplateSource source, string location)
        {
            if (!source.Root.Value.Exists(location))
            {
                return Directory.Empty.NoDispose();
            }

            return new Directory(null, "", "", () => new ZipArchive(source.Root.Value.OpenFile(location), ZipArchiveMode.Read, false)).NoDispose();
        }

        public async Task<bool> CheckForUpdatesAsync(string location)
        {
            if(Path.GetExtension(location).ToUpperInvariant() == ".NUPKG")
            {
                using (IDisposable<ITemplateSourceFolder> root = RootFor(location))
                {
                    ITemplateSourceFile nuspec = root.Value.EnumerateFiles("*.nuspec", SearchOption.TopDirectoryOnly).FirstOrDefault();

                    if(nuspec != null)
                    {
                        string id, version;
                        GetPackageIdAndVersion(nuspec, out id, out version);

                        NuGetUtil.Init();
                        string newerVersion = await NuGetUtil.GetCurrentVersionOfPackageAsync(id, version);
                        return newerVersion != null;
                    }
                }
            }

            return false;
        }

        public async Task<bool> CheckForUpdatesAsync(IConfiguredTemplateSource source, string location)
        {
            if (Path.GetExtension(location).ToUpperInvariant() == ".NUPKG")
            {
                using (IDisposable<ITemplateSourceFolder> root = RootFor(source, location))
                {
                    ITemplateSourceFile nuspec = root.Value.EnumerateFiles("*.nuspec", SearchOption.TopDirectoryOnly).FirstOrDefault();

                    if (nuspec != null)
                    {
                        string id, version;
                        GetPackageIdAndVersion(nuspec, out id, out version);

                        NuGetUtil.Init();
                        string newerVersion = await NuGetUtil.GetCurrentVersionOfPackageAsync(id, version);
                        return newerVersion != null;
                    }
                }
            }

            return false;
        }

        private void GetPackageIdAndVersion(ITemplateSourceFile nuspec, out string id, out string version)
        {
            using (Stream s = nuspec.OpenRead())
            using (TextReader reader = new StreamReader(s, Encoding.UTF8, true, 8192, true))
            {
                XDocument doc = XDocument.Load(reader);
                XElement metadata = doc.Descendants().First(x => x.Name.LocalName == "metadata");
                id = metadata.Descendants().First(x => x.Name.LocalName == "id").Value;
                version = metadata.Descendants().First(x => x.Name.LocalName == "version").Value;
            }
        }

        public string GetInstallPackageId(string location)
        {
            if (Path.GetExtension(location).ToUpperInvariant() == ".NUPKG")
            {
                using (IDisposable<ITemplateSourceFolder> root = RootFor(location))
                {
                    ITemplateSourceFile nuspec = root.Value.EnumerateFiles("*.nuspec", SearchOption.TopDirectoryOnly).FirstOrDefault();

                    if (nuspec != null)
                    {
                        string id, version;
                        GetPackageIdAndVersion(nuspec, out id, out version);
                        return id;
                    }
                }
            }

            return null;
        }

        public string GetInstallPackageId(IConfiguredTemplateSource source, string location)
        {
            if (Path.GetExtension(location).ToUpperInvariant() == ".NUPKG")
            {
                using (IDisposable<ITemplateSourceFolder> root = RootFor(source, location))
                {
                    ITemplateSourceFile nuspec = root.Value.EnumerateFiles("*.nuspec", SearchOption.TopDirectoryOnly).FirstOrDefault();

                    if (nuspec != null)
                    {
                        string id, version;
                        GetPackageIdAndVersion(nuspec, out id, out version);
                        return id;
                    }
                }
            }

            return null;
        }

        private class Directory : TemplateSourceFolder
        {
            private readonly Func<ZipArchive> _opener;
            private readonly int _lastSlashIndex;
            private IReadOnlyList<ITemplateSourceEntry> _cachedChildren;

            private Directory()
                : base(null)
            {
                _cachedChildren = new ITemplateSourceEntry[0];
            }

            public Directory(ITemplateSourceFolder parent, string fullPath, string name, Func<ZipArchive> opener)
                : base(parent)
            {
                FullPath = fullPath;
                Name = name;
                _opener = opener;
                _lastSlashIndex = string.IsNullOrEmpty(fullPath) ? 0 : (fullPath.Length - 1);
            }

            public override IEnumerable<ITemplateSourceEntry> Children => _cachedChildren ?? (_cachedChildren = CalculateChildren().ToList());

            private IEnumerable<ITemplateSourceEntry> CalculateChildren()
            {
                HashSet<string> seenDirNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (ZipArchive archive = _opener())
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        //The new entry and this directory must have the same root for it to be a child
                        if (entry.FullName.IndexOf(FullPath, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            if (string.IsNullOrWhiteSpace(entry.Name))
                            {
                                continue;
                            }

                            //If there's only one part beyond the last slash, it's a file since ZIPs don't support dirs
                            //  as independent constructs
                            int lastIndex = entry.FullName.LastIndexOfAny(new[] { '\\', '/' });
                            if (lastIndex == _lastSlashIndex || (_lastSlashIndex == 0 && lastIndex < 0))
                            {
                                yield return new File(this, entry.FullName, entry.Name, _opener);
                            }
                            else
                            {
                                int startAt = string.IsNullOrEmpty(FullPath) ? -1 : _lastSlashIndex;

                                if (startAt < entry.FullName.Length - 1)
                                {
                                    int nextSlash = entry.FullName.IndexOfAny(new[] { '\\', '/' }, startAt + 1);
                                    string name = entry.FullName.Substring(startAt + 1, nextSlash - startAt - 1);

                                    if (seenDirNames.Add(name))
                                    {
                                        string fullPath = FullPath + name + "/";
                                        yield return new Directory(this, fullPath, name, _opener);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            public override string FullPath { get; }

            public override string Name { get; }

            public static Directory Empty { get; } = new Directory();

            private class File : TemplateSourceFile
            {
                private readonly Func<ZipArchive> _opener;

                public File(ITemplateSourceFolder parent, string fullPath, string name, Func<ZipArchive> opener)
                    : base(parent)
                {
                    _opener = opener;
                    FullPath = fullPath;
                    Name = name;
                }

                public override string FullPath { get; }

                public override string Name { get; }

                public override Stream OpenRead()
                {
                    ZipArchive archive = _opener();
                    ZipArchiveEntry file = archive.GetEntry(FullPath);
                    Stream result = file.Open();
                    return new CoDisposableStream(result, archive);
                }
            }
        }
    }
}