// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Text;

#if !FEATURE_SPECIAL_FOLDERS

namespace Microsoft.Build.Shared
{
    internal static partial class FileUtilities
    {
        //  .NET Core doesn't currently have APIs to get the "special" folders (ie the ones defined in the Environment.SpecialFolder enum)
        //  The code here is mostly copied out of the .NET reference source for this functionality

        public static string GetFolderPath(SpecialFolder folder)
        {
            if (NativeMethodsShared.IsUnixLike)
            {
                return UnixNative.GetFolder(folder);
            }

            if (!NativeMethodsShared.IsWindows)
            {
                throw new PlatformNotSupportedException();
            }

            SpecialFolderOption option = SpecialFolderOption.None;

            StringBuilder sb = new StringBuilder(NativeMethodsShared.MAX_PATH);
            int hresult = Win32Native.SHGetFolderPath(IntPtr.Zero,                    /* hwndOwner: [in] Reserved */
                                                      ((int)folder | (int)option),    /* nFolder:   [in] CSIDL    */
                                                      IntPtr.Zero,                    /* hToken:    [in] access token */
                                                      Win32Native.SHGFP_TYPE_CURRENT, /* dwFlags:   [in] retrieve current path */
                                                      sb);                            /* pszPath:   [out]resultant path */

            String s;
            if (hresult < 0)
            {
                switch (hresult)
                {
                    default:
                        // The previous incarnation threw away all errors. In order to limit
                        // breaking changes, we will be permissive about these errors
                        // instead of calling ThowExceptionForHR.
                        //Runtime.InteropServices.Marshal.ThrowExceptionForHR(hresult);
                        break;
                    case __HResults.COR_E_PLATFORMNOTSUPPORTED:
                        // This one error is the one we do want to throw.
                        // <STRIP>

                        throw new PlatformNotSupportedException();
                }

                // SHGetFolderPath does not initialize the output buffer on error
                s = String.Empty;
            }
            else
            {
                s = sb.ToString();
            }

            return s;
        }

        internal const String SHELL32 = "shell32.dll";

        internal static class __HResults
        {
            internal const int COR_E_PLATFORMNOTSUPPORTED = unchecked((int)0x80131539);
        }

        private static class Win32Native
        {
            [DllImport(SHELL32, CharSet = CharSet.Unicode, BestFitMapping = false)]
            internal static extern int SHGetFolderPath(IntPtr hwndOwner, int nFolder, IntPtr hToken, int dwFlags, [Out]StringBuilder lpszPath);

            internal const int SHGFP_TYPE_CURRENT               = 0;      // the current (user) folder path setting

            internal const int CSIDL_APPDATA = 0x001a;
            internal const int CSIDL_COMMON_APPDATA = 0x0023;
            internal const int CSIDL_LOCAL_APPDATA = 0x001c;
            internal const int CSIDL_COOKIES = 0x0021;
            internal const int CSIDL_FAVORITES = 0x0006;
            internal const int CSIDL_HISTORY = 0x0022;
            internal const int CSIDL_INTERNET_CACHE = 0x0020;
            internal const int CSIDL_PROGRAMS = 0x0002;
            internal const int CSIDL_RECENT = 0x0008;
            internal const int CSIDL_SENDTO = 0x0009;
            internal const int CSIDL_STARTMENU = 0x000b;
            internal const int CSIDL_STARTUP = 0x0007;
            internal const int CSIDL_SYSTEM = 0x0025;
            internal const int CSIDL_TEMPLATES = 0x0015;
            internal const int CSIDL_DESKTOPDIRECTORY = 0x0010;
            internal const int CSIDL_PERSONAL = 0x0005;
            internal const int CSIDL_PROGRAM_FILES = 0x0026;
            internal const int CSIDL_PROGRAM_FILES_COMMON = 0x002b;
            internal const int CSIDL_DESKTOP = 0x0000;
            internal const int CSIDL_DRIVES = 0x0011;
            internal const int CSIDL_MYMUSIC = 0x000d;
            internal const int CSIDL_MYPICTURES = 0x0027;

            internal const int CSIDL_PROFILE                    = 0x0028; // %USERPROFILE% (%SystemDrive%\Users\%USERNAME%)
            internal const int CSIDL_PROGRAM_FILESX86           = 0x002a; // x86 C:\Program Files on RISC

            internal const int CSIDL_WINDOWS                    = 0x0024; // GetWindowsDirectory()

            internal const int CSIDL_FLAG_CREATE = 0x8000; // force folder creation in SHGetFolderPath
            internal const int CSIDL_FLAG_DONT_VERIFY = 0x4000; // return an unverified folder path
        }

        private static class UnixNative
        {
            //     char *getenv(const char *NAME);
            [DllImport("libc", SetLastError = true)]
            internal static extern IntPtr getenv([MarshalAs(UnmanagedType.LPStr)] string name);

            internal static string GetFolder(SpecialFolder folder)
            {
                switch (folder)
                {
                    case SpecialFolder.System:
                        return "/bin";
                    case SpecialFolder.ProgramFiles:
                        return "/user/bin";
                    case SpecialFolder.UserProfile:
                    case SpecialFolder.LocalApplicationData:
                    case SpecialFolder.ApplicationData:
                        unsafe
                        {
                            IntPtr value = getenv("HOME");

                            if (value == IntPtr.Zero)
                            {
                                return null;
                            }

                            int size = 0;
                            while (Marshal.ReadByte(value, size) != 0)
                            {
                                size++;
                            }

                            if (size == 0)
                            {
                                return string.Empty;
                            }

                            byte[] buffer = new byte[size];
                            Marshal.Copy(value, buffer, 0, size);
                            return Encoding.UTF8.GetString(buffer);
                        }
                    default:
                        return null;
                }
            }
        }

        [ComVisible(true)]
        public enum SpecialFolder
        {
            //  
            //      Represents the file system directory that serves as a common repository for
            //       application-specific data for the current, roaming user. 
            //     A roaming user works on more than one computer on a network. A roaming user's 
            //       profile is kept on a server on the network and is loaded onto a system when the
            //       user logs on. 
            //  
            ApplicationData = Win32Native.CSIDL_APPDATA,
            //  
            //      Represents the file system directory that serves as a common repository for application-specific data that
            //       is used by all users. 
            //  
            CommonApplicationData = Win32Native.CSIDL_COMMON_APPDATA,
            //  
            //     Represents the file system directory that serves as a common repository for application specific data that
            //       is used by the current, non-roaming user. 
            //  
            LocalApplicationData = Win32Native.CSIDL_LOCAL_APPDATA,
            //  
            //     Represents the file system directory that serves as a common repository for Internet
            //       cookies. 
            //  
            Cookies = Win32Native.CSIDL_COOKIES,
            Desktop = Win32Native.CSIDL_DESKTOP,
            //  
            //     Represents the file system directory that serves as a common repository for the user's
            //       favorite items. 
            //  
            Favorites = Win32Native.CSIDL_FAVORITES,
            //  
            //     Represents the file system directory that serves as a common repository for Internet
            //       history items. 
            //  
            History = Win32Native.CSIDL_HISTORY,
            //  
            //     Represents the file system directory that serves as a common repository for temporary 
            //       Internet files. 
            //  
            InternetCache = Win32Native.CSIDL_INTERNET_CACHE,
            //  
            //      Represents the file system directory that contains
            //       the user's program groups. 
            //  
            Programs = Win32Native.CSIDL_PROGRAMS,
            MyComputer = Win32Native.CSIDL_DRIVES,
            MyMusic = Win32Native.CSIDL_MYMUSIC,
            MyPictures = Win32Native.CSIDL_MYPICTURES,
            ////      "My Videos" folder
            //MyVideos = Win32Native.CSIDL_MYVIDEO,
            //  
            //     Represents the file system directory that contains the user's most recently used
            //       documents. 
            //  
            Recent = Win32Native.CSIDL_RECENT,
            //  
            //     Represents the file system directory that contains Send To menu items. 
            //  
            SendTo = Win32Native.CSIDL_SENDTO,
            //  
            //     Represents the file system directory that contains the Start menu items. 
            //  
            StartMenu = Win32Native.CSIDL_STARTMENU,
            //  
            //     Represents the file system directory that corresponds to the user's Startup program group. The system
            //       starts these programs whenever any user logs on to Windows NT, or
            //       starts Windows 95 or Windows 98. 
            //  
            Startup = Win32Native.CSIDL_STARTUP,
            //  
            //     System directory.
            //  
            System = Win32Native.CSIDL_SYSTEM,
            //  
            //     Represents the file system directory that serves as a common repository for document
            //       templates. 
            //  
            Templates = Win32Native.CSIDL_TEMPLATES,
            //  
            //     Represents the file system directory used to physically store file objects on the desktop.
            //       This should not be confused with the desktop folder itself, which is
            //       a virtual folder. 
            //  
            DesktopDirectory = Win32Native.CSIDL_DESKTOPDIRECTORY,
            //  
            //     Represents the file system directory that serves as a common repository for documents. 
            //  
            Personal = Win32Native.CSIDL_PERSONAL,
            //          
            // "MyDocuments" is a better name than "Personal"
            //
            MyDocuments = Win32Native.CSIDL_PERSONAL,
            //  
            //     Represents the program files folder. 
            //  
            ProgramFiles = Win32Native.CSIDL_PROGRAM_FILES,
            //
            //      USERPROFILE
            //
            UserProfile            = Win32Native.CSIDL_PROFILE,
            //  
            //     Represents the folder for components that are shared across applications. 
            //  
            CommonProgramFiles = Win32Native.CSIDL_PROGRAM_FILES_COMMON,

            //
            //      x86 C:\Program Files on RISC
            //
            ProgramFilesX86        = Win32Native.CSIDL_PROGRAM_FILESX86,

            //
            //      GetWindowsDirectory()
            //
            Windows                = Win32Native.CSIDL_WINDOWS,
        }

        private enum SpecialFolderOption
        {
            None = 0,
            Create = Win32Native.CSIDL_FLAG_CREATE,
            DoNotVerify = Win32Native.CSIDL_FLAG_DONT_VERIFY,
        }
    }
}
#endif
