using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Microsoft.DotNet.Archive
{
    // wraps ThreadLocal<ZipArchive> and exposes Dispose semantics that dispose all archives
    class ThreadLocalZipArchive : IDisposable
    {
        private ThreadLocal<ZipArchive> _archive;
        private bool disposed = false;

        public ThreadLocalZipArchive(string archivePath, ZipArchive local = null)
        {
            _archive = new ThreadLocal<ZipArchive>(() =>
                         new ZipArchive(File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete), ZipArchiveMode.Read),
                         trackAllValues:true);

            if (local != null)
            {
                // reuse provided one for current thread
                _archive.Value = local;
            }
        }

        public ZipArchive Archive { get { return _archive.Value; } }

        public void Dispose()
        {
            if (!disposed)
            {
                if (_archive != null)
                {
                    // dispose all archives
                    if (_archive.Values != null)
                    {
                        foreach (var value in _archive.Values)
                        {
                            if (value != null)
                            {
                                value.Dispose();
                            }
                        }
                    }

                    // dispose ThreadLocal
                    _archive.Dispose();
                    _archive = null;
                }
            }
        }
    }
}
