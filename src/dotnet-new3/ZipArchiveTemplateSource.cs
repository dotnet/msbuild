using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Mutant.Chicken.Abstractions;

namespace dotnet_new3
{
    public class ZipArchiveTemplateSource : ITemplateSource
    {
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

        public IDisposable<ITemplateSourceFolder> RootFor(string location)
        {
            return new Directory(null, "", "", () => ZipFile.OpenRead(location)).NoDispose();
        }

        private class Directory : TemplateSourceFolder
        {
            private readonly Func<ZipArchive> _opener;
            private readonly int _lastSlashIndex;
            private IReadOnlyList<ITemplateSourceEntry> _cachedChildren;

            public Directory(ITemplateSourceFolder parent, string fullPath, string name, Func<ZipArchive> opener)
                : base(parent)
            {
                FullPath = fullPath;
                Name = name;
                _opener = opener;
                _lastSlashIndex = string.IsNullOrEmpty(fullPath) ? 0 : (fullPath.Length - 1);
            }

            public override IEnumerable<ITemplateSourceEntry> Children
            {
                get { return _cachedChildren ?? (_cachedChildren = CalculateChildren().ToList()); }
            }

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
                            //If there's only one part beyond the last slash, it's a file since ZIPs don't support dirs
                            //  as independent constructs
                            if (!string.IsNullOrWhiteSpace(entry.Name) && entry.FullName.LastIndexOfAny(new []{ '\\', '/' }) == _lastSlashIndex)
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

                private class CoDisposableStream :Stream
                {
                    private readonly IDisposable[] _alsoDispose;
                    private readonly Stream _source;

                    public CoDisposableStream(Stream source, params IDisposable[] alsoDispose)
                    {
                        _source = source;
                        _alsoDispose = alsoDispose;
                    }

                    public override bool CanRead => _source.CanRead;

                    public override bool CanSeek => _source.CanSeek;

                    public override bool CanWrite => _source.CanWrite;

                    public override long Length => _source.Length;

                    public override long Position
                    {
                        get { return _source.Position; }
                        set { _source.Position = value; }
                    }

                    public override void Flush() => _source.Flush();

                    public override int Read(byte[] buffer, int offset, int count) => _source.Read(buffer, offset, count);

                    public override long Seek(long offset, SeekOrigin origin) => _source.Seek(offset, origin);

                    public override void SetLength(long value) => _source.SetLength(value);

                    public override void Write(byte[] buffer, int offset, int count) => _source.Write(buffer, offset, count);

                    protected override void Dispose(bool disposing)
                    {
                        foreach(IDisposable disposable in _alsoDispose)
                        {
                            disposable.Dispose();
                        }

                        _source.Dispose();
                    }
                }
            }
        }
    }
}