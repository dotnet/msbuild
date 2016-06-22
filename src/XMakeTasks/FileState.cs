// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Cache file state over file name.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// CopyFile delegate
    /// 
    /// returns  Success = true, Failure = false; Retry = null
    /// </summary>
    /// <param name="source">Source file</param>
    /// <param name="destination">Destination file</param>
    internal delegate bool? CopyFileWithState(FileState source, FileState destination);

    /// <summary>
    /// Short-term cache saves the result of IO operations on a filename. Should only be
    /// used in cases where it is know there will be no exogenous changes to the filesystem
    /// for this file.
    /// </summary>
    /// <remarks>
    /// Uses PInvoke rather than FileInfo because the latter does all kinds of expensive checks.
    /// 
    /// Deficiency: some of the properties eat some or all exceptions. If they are called first, they will
    /// trigger the population and eat. Subsequent calls will then not throw, but instead eg return zero.
    /// This could be fixed by storing the exception from the population, and throwing no matter who does
    /// the population and whether it's been done before.
    /// </remarks>
    internal class FileState
    {
        /// <summary>
        /// The name of the file.
        /// </summary>
        private string _filename;

        /// <summary>
        /// The info about the file.
        /// </summary>
        private NativeMethodsShared.WIN32_FILE_ATTRIBUTE_DATA _data;

        /// <summary>
        /// Whether data is reliable.
        /// False means that we tried to get it, but failed. Only Reset will get it again.
        /// Null means we didn't try yet.
        /// </summary>
        private bool? _dataIsGood;

        /// <summary>
        /// Whether the file or directory exists.
        /// Used instead of an exception, for perf.
        /// </summary>
        private bool _fileOrDirectoryExists;

        /// <summary>
        /// Constructor.
        /// Only stores file name: does not grab the file state until first request.
        /// </summary>
        internal FileState(string filename)
        {
            ErrorUtilities.VerifyThrowArgumentLength(filename, "filename");
            _filename = filename;
        }

        /// <summary>
        /// Whether the file is readonly.
        /// Returns false for directories.
        /// Throws if file does not exist.
        /// </summary>
        internal bool IsReadOnly
        {
            get
            {
                EnsurePopulated();

                if (DirectoryExists)
                {
                    return false;
                }

                if (!FileExists)
                {
                    // Provoke exception
                    var length = (new FileInfo(_filename)).Length;
                }

                return ((_data.fileAttributes & NativeMethodsShared.FILE_ATTRIBUTE_READONLY) != 0);
            }
        }

        /// <summary>
        /// Whether the file exists.
        /// Returns false if it is a directory, even if it exists.
        /// Returns false instead of IO related exceptions.
        /// </summary>
        internal bool FileExists
        {
            get
            {
                try
                {
                    EnsurePopulated();
                }
                catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
                {
                    return false;
                }

                return _fileOrDirectoryExists && !IsDirectory;
            }
        }

        /// <summary>
        /// Whether the directory exists.
        /// Returns false for files.
        /// Returns false instead of IO related exceptions.
        /// </summary>
        internal bool DirectoryExists
        {
            get
            {
                try
                {
                    EnsurePopulated();
                }
                catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
                {
                    return false;
                }

                return _fileOrDirectoryExists && IsDirectory;
            }
        }

        /// <summary>
        /// Last time the file was written.
        /// If file does not exist, returns 12 midnight 1/1/1601.
        /// Works for directories.
        /// </summary>
        internal DateTime LastWriteTime
        {
            get
            {
                // Could cache this as conversion can be expensive
                return LastWriteTimeUtcFast.ToLocalTime();
            }
        }

        /// <summary>
        /// Last time the file was written, in UTC. Avoids translation for daylight savings, time zone etc which isn't needed for just comparisons.
        /// If file does not exist, returns 12 midnight 1/1/1601.
        /// Works for directories.
        /// </summary>
        internal DateTime LastWriteTimeUtcFast
        {
            get
            {
                EnsurePopulated();

                if (!_fileOrDirectoryExists)
                {
                    // Same as the FileInfo class
                    return new DateTime(1601, 1, 1);
                }

                return DateTime.FromFileTimeUtc(((long)_data.ftLastWriteTimeHigh << 0x20) | _data.ftLastWriteTimeLow);
            }
        }

        /// <summary>
        /// Length of the file in bytes.
        /// Throws if it is a directory.
        /// Throws if it does not exist.
        /// </summary>
        internal long Length
        {
            get
            {
                EnsurePopulated();

                if (DirectoryExists)
                {
                    // Produce a nice file not found exception message
                    var info = new FileInfo(_filename).Length;
                }

                if (!FileExists)
                {
                    // Provoke exception
                    var length = (new FileInfo(_filename)).Length;
                }

                return (((long)_data.fileSizeHigh << 0x20) | _data.fileSizeLow);
            }
        }

        /// <summary>
        /// Name of the file as it was passed in.
        /// Not normalized.
        /// </summary>
        internal string Name
        {
            get
            {
                return _filename;
            }
        }

        /// <summary>
        /// Whether this is a directory.
        /// Throws if it does not exist.
        /// </summary>
        internal bool IsDirectory
        {
            get
            {
                EnsurePopulated();

                if (!_fileOrDirectoryExists)
                {
                    // Provoke exception
                    var length = (new FileInfo(_filename)).Length;
                }

                return ((_data.fileAttributes & NativeMethodsShared.FILE_ATTRIBUTE_DIRECTORY) != 0);
            }
        }

        /// <summary>
        /// Use in case the state is known to have changed exogenously.
        /// </summary>
        internal void Reset()
        {
            _data = new NativeMethodsShared.WIN32_FILE_ATTRIBUTE_DATA();
            _dataIsGood = null;
        }

        /// <summary>
        /// Ensure we have the data.
        /// Does not throw for nonexistence.
        /// </summary>
        private void EnsurePopulated()
        {
            if (_dataIsGood == null)
            {
                _dataIsGood = false;
                _filename = FileUtilities.AttemptToShortenPath(_filename); // This is no-op unless the path actually is too long
                _data = new NativeMethodsShared.WIN32_FILE_ATTRIBUTE_DATA();

                // THIS COPIED FROM THE BCL:
                //
                // For floppy drives, normally the OS will pop up a dialog saying
                // there is no disk in drive A:, please insert one.  We don't want that. 
                // SetErrorMode will let us disable this, but we should set the error
                // mode back, since this may have wide-ranging effects.
                int oldMode = NativeMethodsShared.SetErrorMode(1 /* ErrorModes.SEM_FAILCRITICALERRORS */);

                bool success = false;
                _fileOrDirectoryExists = true;

                try
                {
                    success = NativeMethodsShared.GetFileAttributesEx(_filename, 0, ref _data);

                    if (!success)
                    {
                        int error = Marshal.GetLastWin32Error();

                        // File not found is the most common case, for example we're copying
                        // somewhere without a file yet. Don't do something like FileInfo.Exists to
                        // get a nice error, or we're doing IO again! Don't even format our own string:
                        // that turns out to be unacceptably expensive here as well. Set a flag for this particular case.
                        //
                        // Also, when not under debugger (!) it will give error == 3 for path too long. Make that consistently throw instead.
                        if ((error == 2 /* ERROR_FILE_NOT_FOUND */ || error == 3 /* ERROR_PATH_NOT_FOUND */) &&
                            _filename.Length <= NativeMethodsShared.MAX_PATH)
                        {
                            _fileOrDirectoryExists = false;
                            return;
                        }

                        // Throw nice message as far as we can. At this point IO is OK.
                        var length = new FileInfo(_filename).Length;

                        // Otherwise this will give at least something
                        NativeMethodsShared.ThrowExceptionForErrorCode(error);
                        ErrorUtilities.ThrowInternalErrorUnreachable();
                    }
                }
                finally
                {
                    NativeMethodsShared.SetErrorMode(oldMode);
                }

                _dataIsGood = true;
            }
        }
    }
}
