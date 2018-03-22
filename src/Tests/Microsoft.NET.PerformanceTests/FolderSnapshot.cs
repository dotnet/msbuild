using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.NET.Perf.Tests
{
    class FolderSnapshot : IDisposable
    {
        string OriginalPath { get; set; }
        string BackupPath { get; set; }

        public static FolderSnapshot Create(string path)
        {
            FolderSnapshot folderSnapshot = new FolderSnapshot();
            folderSnapshot.OriginalPath = path;
            folderSnapshot.BackupPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            MirrorFiles(folderSnapshot.OriginalPath, folderSnapshot.BackupPath);
            return folderSnapshot;
        }

        public void Restore()
        {
            MirrorFiles(BackupPath, OriginalPath);
        }

        public static void MirrorFiles(string source, string dest)
        {
            if (Directory.Exists(dest))
            {
                Directory.Delete(dest, true);
            }
            CopyDirectory(source, dest);
        }

        static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);

            DirectoryInfo sourceInfo = new DirectoryInfo(source);
            foreach (var fileInfo in sourceInfo.GetFiles())
            {
                string destFile = Path.Combine(dest, fileInfo.Name);
                fileInfo.CopyTo(destFile);
            }
            foreach (var subdirInfo in sourceInfo.GetDirectories())
            {
                //  Don't mirror .git folder
                if (subdirInfo.Name == ".git")
                {
                    continue;
                }
                string destDir = Path.Combine(dest, subdirInfo.Name);
                CopyDirectory(subdirInfo.FullName, destDir);
            }
        }

        public void Dispose()
        {
            Directory.Delete(BackupPath, true);
        }
    }
}
