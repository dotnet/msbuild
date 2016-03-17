using System;
using System.IO;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Muxer
    {
        private static readonly string s_muxerFileName = "dotnet" + Constants.ExeSuffix;

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
            var appBase = new DirectoryInfo(PlatformServices.Default.Application.ApplicationBasePath);
            var muxerDir = appBase.Parent.Parent;

            var muxerCandidate = Path.Combine(muxerDir.FullName, s_muxerFileName);

            if (File.Exists(muxerCandidate))
            {
                _muxerPath = muxerCandidate;
            }
        }
    }
}
