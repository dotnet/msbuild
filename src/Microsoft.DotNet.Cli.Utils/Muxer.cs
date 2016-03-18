using System;
using System.IO;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Muxer
    {
            private static readonly string s_muxerName = "dotnet";
        private static readonly string s_muxerFileName = s_muxerName + Constants.ExeSuffix;

        private string _muxerPath;

        public string MuxerPath
        {
            get
            {
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
            var appBase = new DirectoryInfo(PlatformServices.Default.Application.ApplicationBasePath);
            var muxerDir = appBase.Parent?.Parent;

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
            var muxerPath = Env.GetCommandPath(s_muxerName, Constants.ExeSuffix);

            if (muxerPath == null || !File.Exists(muxerPath))
            {
                return false;
            }

            _muxerPath = muxerPath;

            return true;
        }
    }
}
