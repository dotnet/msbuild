using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.TemplateEngine.Core;

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
        private static string _userProfileDir;
        private static string _packageCache;
        private static string _globalComponentCacheFile;
        private static string _globalComponentsDir;
        private static string _globalTemplateCacheDir;
        private static string _builtInsFeed;
        private static string _userNuGetConfig;
        private static string _defaultInstallTemplateList;
        private static string _defaultInstallPackageList;
        private static string _firstRunCookie;

        public static string PackageCache => _packageCache ?? (_packageCache = Path.Combine(UserProfileDir, ".nuget", "packages"));

        public static string UserProfileDir
        {
            get
            {
                if(_userProfileDir == null)
                {
                    string profileDir =
                        Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                            ? "USERPROFILE"
                            : "HOME");

                    _userProfileDir = profileDir;
                }

                return _userProfileDir;
            }
        }

        public static string UserDir => _userDir ?? (_userDir = Path.Combine(UserProfileDir, ".netnew"));

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

        public static string UserNuGetConfig => _userNuGetConfig ?? (_userNuGetConfig = Path.Combine(UserDir, "NuGet.config"));

        public static string BuiltInsFeed => _builtInsFeed ?? (_builtInsFeed = Path.Combine(AppDir, "BuiltIns"));

        public static string DefaultInstallTemplateList => _defaultInstallTemplateList ?? (_defaultInstallTemplateList = Path.Combine(AppDir, "defaultinstall.template.list"));

        public static string DefaultInstallPackageList => _defaultInstallPackageList ?? (_defaultInstallPackageList = Path.Combine(AppDir, "defaultinstall.package.list"));

        public static string GlobalComponentsDir => _globalComponentsDir ?? (_globalComponentsDir = Path.Combine(AppDir, "Components"));

        public static string GlobalComponentCacheFile => _globalComponentCacheFile ?? (_globalComponentCacheFile = Path.Combine(AppDir, "components.json"));

        public static string ComponentsDir => _componentsDir ?? (_componentsDir = Path.Combine(UserDir, "Components"));

        public static string ComponentCacheFile => _componentCacheFile ?? (_componentCacheFile = Path.Combine(UserDir, "components.json"));

        public static string TemplateSourcesFile => _templateSourcesFile ?? (_templateSourcesFile = Path.Combine(UserDir, "template_sources.json"));

        public static string AliasesFile => _aliasesFile ?? (_aliasesFile = Path.Combine(UserDir, "aliases.json"));

        public static string ScratchDir => _scratchDir ?? (_scratchDir = Path.Combine(UserDir, "scratch"));

        public static string TemplateCacheDir => _templateCacheDir ?? (_templateCacheDir = Path.Combine(UserDir, "Templates"));

        public static string GlobalTemplateCacheDir => _globalTemplateCacheDir ?? (_globalTemplateCacheDir = Path.Combine(AppDir, "Templates"));

        public static string FirstRunCookie => _firstRunCookie ?? (_firstRunCookie = Path.Combine(AppDir, ".firstrun"));

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

        public static void Copy(this string path, string targetPath)
        {
            if (File.Exists(path))
            {
                File.Copy(path, targetPath, true);
                return;
            }

            foreach(string p in path.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(x => x.Length))
            {
                string localPath = p.Substring(path.Length).TrimStart('\\', '/');

                if (Directory.Exists(p))
                {
                    localPath.CreateDirectory(targetPath);
                }
                else
                {
                    int parentDirEndIndex = localPath.LastIndexOfAny(new[] {'/', '\\'});
                    string wholeTargetPath = Path.Combine(targetPath, localPath);

                    if (parentDirEndIndex > -1)
                    {
                        string parentDir = localPath.Substring(0, parentDirEndIndex);
                        parentDir.CreateDirectory(targetPath);
                    }

                    File.Copy(p, wholeTargetPath, true);
                }
            }
        }

        public static void CreateDirectory(this string path, string parent)
        {
            string[] parts = path.Split(new[] {'/', '\\'}, StringSplitOptions.RemoveEmptyEntries);
            string current = parent;

            for(int i = 0; i < parts.Length; ++i)
            {
                current = Path.Combine(current, parts[i]);
                Directory.CreateDirectory(current);
            }
        }

        public static IEnumerable<string> EnumerateDirectories(this string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (Directory.Exists(path))
            {
                return Directory.EnumerateDirectories(path, pattern, searchOption);
            }

            return Enumerable.Empty<string>();
        }

        public static IEnumerable<string> EnumerateFileSystemEntries(this string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (File.Exists(path))
            {
                return new[] { path };
            }

            if (Directory.Exists(path))
            {
                return Directory.EnumerateFileSystemEntries(path.ProcessPath(), pattern, searchOption);
            }

            return Enumerable.Empty<string>();
        }

        public static IEnumerable<string> EnumerateFiles(this string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (File.Exists(path))
            {
                return new[] { path };
            }

            if (Directory.Exists(path))
            {
                return Directory.EnumerateFiles(path.ProcessPath(), pattern, searchOption);
            }

            return Enumerable.Empty<string>();
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

        public static void WriteAllText(this string path, string value)
        {
            string parentDir = Path.GetDirectoryName(path);

            if (!parentDir.Exists())
            {
                Directory.CreateDirectory(parentDir);
            }

            File.WriteAllText(path, value);
        }

        public static string ToPath(this string codebase)
        {
            Uri cb = new Uri(codebase, UriKind.Absolute);
            string localPath = cb.LocalPath;
            return localPath;
        }
    }
}
