using System;
using System.IO;
using Microsoft.Extensions.PlatformAbstractions;
using System.Diagnostics;
using System.Reflection;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Muxer
    {
        public static readonly string MuxerName = "dotnet";
        private static readonly string s_muxerFileName = MuxerName + Constants.ExeSuffix;

        private string _muxerPath;

        public string MuxerPath
        {
            get
            {
                if (_muxerPath == null)
                {
                    throw new InvalidOperationException("Unable to locate dotnet multiplexer");
                }
                return _muxerPath;
            }
        }

        public Muxer()
        {
            if (!TryResolveMuxerFromParentDirectories())
            {
                TryResolverMuxerFromPath();
            }
        }

        private bool TryResolveMuxerFromParentDirectories()
        {
            var appBase = new FileInfo(typeof(object).GetTypeInfo().Assembly.Location);
            var muxerDir = appBase.Directory?.Parent?.Parent?.Parent;

            if (muxerDir == null)
            {
                return false;
            }

            var muxerCandidate = Path.Combine(muxerDir.FullName, s_muxerFileName);

            if (!File.Exists(muxerCandidate))
            {
                return false;
            }

            _muxerPath = muxerCandidate;
            return true;
        }

        private bool TryResolverMuxerFromPath()
        {
            var muxerPath = Env.GetCommandPath(MuxerName, Constants.ExeSuffix);

            if (muxerPath == null || !File.Exists(muxerPath))
            {
                return false;
            }

            _muxerPath = muxerPath;

            return true;
        }
    }
}
