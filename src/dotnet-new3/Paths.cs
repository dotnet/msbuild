using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace dotnet_new3
{
    public static class Paths
    {
        private static string _appDir;
        private static string _componentsDir;
        private static string _scratchDir;
        private static string _templateCacheDir;
        private static string _componentCacheFile;
        private static string _templateSourcesFile;
        private static string _aliasesFile;
        private static string _userDir;

        public static string UserDir
        {
            get
            {
                if (_userDir == null)
                {
                    string profileDir;
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        profileDir = Environment.GetEnvironmentVariable("USERPROFILE");
                    }
                    else
                    {
                        profileDir = Environment.GetEnvironmentVariable("HOME");
                    }

                    _userDir = Path.Combine(profileDir, ".netnew");
                }

                return _userDir;
            }
        }

        public static string AppDir
        {
            get
            {
                if (_appDir == null)
                {
                    Assembly asm = Assembly.GetEntryAssembly();
                    Uri codebase = new Uri(asm.CodeBase, UriKind.Absolute);
                    string localPath = codebase.LocalPath;
                    _appDir = Path.GetDirectoryName(localPath);
                }

                return _appDir;
            }
        }

        public static string ComponentsDir => _componentsDir ?? (_componentsDir = Path.Combine(UserDir, "Components"));

        public static string ComponentCacheFile => _componentCacheFile ?? (_componentCacheFile = Path.Combine(UserDir, "components.json"));

        public static string TemplateSourcesFile => _templateSourcesFile ?? (_templateSourcesFile = Path.Combine(UserDir, "template_sources.json"));

        public static string AliasesFile => _aliasesFile ?? (_aliasesFile = Path.Combine(UserDir, "aliases.json"));

        public static string ScratchDir => _scratchDir ?? (_scratchDir = Path.Combine(UserDir, "scratch"));

        public static string TemplateCacheDir => _templateCacheDir ?? (_templateCacheDir = Path.Combine(UserDir, "Templates"));

        public static void DeleteDirectory(this string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        public static void DeleteFile(this string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public static void Delete(this string path)
        {
            path.DeleteDirectory();
            path.DeleteFile();
        }

        public static bool FileExists(this string path)
        {
            return File.Exists(path);
        }

        public static bool DirectoryExists(this string path)
        {
            return Directory.Exists(path);
        }

        public static bool Exists(this string path)
        {
            return path.FileExists() || path.DirectoryExists();
        }

        public static void CreateDirectory(this string path)
        {
            Directory.CreateDirectory(path);
        }

        public static string ReadAllText(this string path, string defaultValue = "")
        {
            return path.Exists() ? File.ReadAllText(path) : defaultValue;
        }

        public static string ToPath(this string codebase)
        {
            Uri cb = new Uri(codebase, UriKind.Absolute);
            string localPath = cb.LocalPath;
            return localPath;
        }
    }
}
