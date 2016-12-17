using System;
using System.IO;
using System.Reflection;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Muxer
    {
        public static readonly string MuxerName = "dotnet";
        private static readonly string s_muxerFileName = MuxerName + Constants.ExeSuffix;

        private string _muxerPath;

        internal string SharedFxVersion
        {
            get
            {
                var depsFile = new FileInfo(GetDataFromAppDomain("FX_DEPS_FILE"));
                return depsFile.Directory.Name;
            }
        }

        public string MuxerPath
        {
            get
            {
                if (_muxerPath == null)
                {
                    throw new InvalidOperationException(LocalizableStrings.UnableToLocateDotnetMultiplexer);
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

        public static string GetDataFromAppDomain(string propertyName)
        {
            var appDomainType = typeof(object).GetTypeInfo().Assembly?.GetType("System.AppDomain");
            var currentDomain = appDomainType?.GetProperty("CurrentDomain")?.GetValue(null);
            var deps = appDomainType?.GetMethod("GetData")?.Invoke(currentDomain, new[] { propertyName });

            return deps as string;
        }

        private bool TryResolveMuxerFromParentDirectories()
        {
            var fxDepsFile = GetDataFromAppDomain("FX_DEPS_FILE");
            if (string.IsNullOrEmpty(fxDepsFile))
            {
                return false;
            }

            var muxerDir = new FileInfo(fxDepsFile).Directory?.Parent?.Parent?.Parent;
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
