using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;

namespace dotnet_new3
{
    public class FileSystemTemplateSource : ITemplateSource
    {
        public bool IsEmbeddable => false;

        public bool CanHostEmbeddedSources => true;

        public string Name => "FileSystem";

        public bool CanHandle(string location)
        {
            try
            {
                return location.DirectoryExists();
            }
            catch
            {
                return false;
            }
        }

        public bool CanHandle(IConfiguredTemplateSource source, string location)
        {
            return false;
        }

        public IDisposable<ITemplateSourceFolder> RootFor(string location)
        {
            DirectoryInfo root = new DirectoryInfo(location);
            int len = root.FullName.Length;

            if (root.FullName.LastIndexOfAny(new[] { '\\', '/' }) != root.FullName.Length - 1)
            {
                ++len;
            }

            return new Directory(len, null, root).NoDispose();
        }

        public IDisposable<ITemplateSourceFolder> RootFor(IConfiguredTemplateSource source, string location)
        {
            return null;
        }

        public Task<bool> CheckForUpdatesAsync(string location)
        {
            return Task.FromResult(false);
        }

        public Task<bool> CheckForUpdatesAsync(IConfiguredTemplateSource source, string location)
        {
            return Task.FromResult(false);
        }

        public string GetInstallPackageId(string location)
        {
            return null;
        }

        public string GetInstallPackageId(IConfiguredTemplateSource source, string location)
        {
            return null;
        }

        private static class EntryHelper
        {
            public static ITemplateSourceEntry Create(int rootLength, ITemplateSourceFolder parent, FileSystemInfo info)
            {
                DirectoryInfo dir = info as DirectoryInfo;

                if(dir != null)
                {
                    return new Directory(rootLength, parent, dir);
                }

                return new File(rootLength, parent, (FileInfo)info);
            }
        }

        private class Directory : TemplateSourceFolder
        {
            private readonly DirectoryInfo _dir;
            private readonly int _rootLength;

            public Directory(int rootLength, ITemplateSourceFolder parent, DirectoryInfo dir)
                : base(parent)
            {
                _rootLength = rootLength;
                _dir = dir;
            }

            public override IEnumerable<ITemplateSourceEntry> Children
            {
                get
                {
                    _dir.Refresh();
                    if (_dir.Exists)
                    {
                        return _dir.EnumerateFileSystemInfos().Select(x => EntryHelper.Create(_rootLength, this, x));
                    }

                    return Enumerable.Empty<ITemplateSourceEntry>();
                }
            }

            public override string FullPath => _dir.FullName.Substring(_rootLength);

            public override string Name => _dir.Name;
        }

        private class File : TemplateSourceFile
        {
            private readonly FileInfo _file;
            private readonly int _rootLength;

            public File(int rootLength, ITemplateSourceFolder parent, FileInfo file)
                : base(parent)
            {
                _rootLength = rootLength;
                _file = file;
            }

            public override string FullPath => _file.FullName.Substring(_rootLength);

            public override string Name => _file.Name;

            public override Stream OpenRead() => _file.OpenRead();
        }
    }
}
