using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mutant.Chicken.Abstractions;

namespace dotnet_new3
{
    public class FileSystemTemplateSource : ITemplateSource
    {
        public string Name => "FileSystem";

        public bool CanHandle(string location)
        {
            try
            {
                return System.IO.Directory.Exists(location);
            }
            catch
            {
                return false;
            }
        }

        public IEnumerable<ITemplateSourceEntry> EntriesIn(string location)
        {
            DirectoryInfo root = new DirectoryInfo(location);
            return root.EnumerateFileSystemInfos().Select(EntryHelper.Create);
        }

        private static class EntryHelper
        {
            public static ITemplateSourceEntry Create(FileSystemInfo info)
            {
                DirectoryInfo dir = info as DirectoryInfo;

                if(dir != null)
                {
                    return new Directory(dir);
                }

                return new File((FileInfo)info);
            }
        }

        private class Directory : TemplateSourceFolder
        {
            private readonly DirectoryInfo _dir;

            public Directory(DirectoryInfo dir)
            {
                _dir = dir;
            }

            public override IEnumerable<ITemplateSourceEntry> Children => _dir.EnumerateFileSystemInfos().Select(EntryHelper.Create);

            public override string FullPath => _dir.FullName;

            public override string Name => _dir.Name;
        }

        private class File : TemplateSourceFile
        {
            private readonly FileInfo _file;

            public File(FileInfo file)
            {
                _file = file;
            }

            public override string FullPath => _file.FullName;

            public override string Name => _file.Name;

            public override Stream OpenRead() => _file.OpenRead();
        }
    }
}
