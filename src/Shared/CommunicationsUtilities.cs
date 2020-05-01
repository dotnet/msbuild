// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;

using Microsoft.Build.Shared;
using System.Reflection;

#if !FEATURE_APM
using System.Threading.Tasks;
#endif

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// Enumeration of all possible (currently supported) options for handshakes.
    /// </summary>
    [Flags]
    internal enum HandshakeOptions
    {
        None = 0,

        /// <summary>
        /// Process is a TaskHost
        /// </summary>
        TaskHost = 1,

        /// <summary>
        /// Using the 2.0 CLR
        /// </summary>
        CLR2 = 2,

        /// <summary>
        /// 64-bit Intel process
        /// </summary>
        X64 = 4,

        /// <summary>
        /// Node reuse enabled
        /// </summary>
        NodeReuse = 8,

        /// <summary>
        /// Building with BelowNormal priority
        /// </summary>
        LowPriority = 16
    }

    /// <summary>
    /// This class contains utility methods for the MSBuild engine.
    /// </summary>
    static internal class CommunicationsUtilities
    {
        /// <summary>
        /// The timeout to connect to a node.
        /// </summary>
        private const int DefaultNodeConnectionTimeout = 900 * 1000; // 15 minutes; enough time that a dev will typically do another build in this time

        /// <summary>
        /// Flag if we have already calculated the FileVersion hashcode
        /// </summary>
        private static bool s_fileVersionChecked;

        /// <summary>
        /// A hashcode calculated from the fileversion
        /// </summary>
        private static int s_fileVersionHash;

        /// <summary>
        /// Whether to trace communications
        /// </summary>
        private static bool s_trace = String.Equals(Environment.GetEnvironmentVariable("MSBUILDDEBUGCOMM"), "1", StringComparison.Ordinal);

        /// <summary>
        /// Place to dump trace
        /// </summary>
        private static string s_debugDumpPath;

        /// <summary>
        /// Ticks at last time logged
        /// </summary>
        private static long s_lastLoggedTicks = DateTime.UtcNow.Ticks;

        /// <summary>
        /// Delegate to debug the communication utilities.
        /// </summary>
        internal delegate void LogDebugCommunications(string format, params object[] stuff);

        /// <summary>
        /// Gets or sets the node connection timeout.
        /// </summary>
        static internal int NodeConnectionTimeout
        {
            get { return GetIntegerVariableOrDefault("MSBUILDNODECONNECTIONTIMEOUT", DefaultNodeConnectionTimeout); }
        }

        /// <summary>
        /// Looks up the file version and caches the hashcode
        /// This file version hashcode is used in calculating the handshake
        /// </summary>
        private static int FileVersionHash
        {
            get
            {
                if (!s_fileVersionChecked)
                {
                    // We only hash in any complus_installroot value, not a file version.
                    // This is because in general msbuildtaskhost.exe does not load any assembly that
                    // the parent process loads, so they can't compare the version of a particular assembly.
                    // They can't compare their own versions, because if one of them is serviced, they
                    // won't match any more. The only known incompatibility is between a razzle and non-razzle
                    // parent and child. COMPLUS_Version can (and typically will) differ legitimately between
                    // them, so just check COMPLUS_InstallRoot.
                    string complusInstallRoot = Environment.GetEnvironmentVariable("COMPLUS_INSTALLROOT");

                    // This is easier in .NET 4+:
                    //  var fileIdentity = typeof(CommunicationsUtilities).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
                    // but we need to be 3.5 compatible here to work in MSBuildTaskHost
                    string fileIdentity = null;
                    foreach (var attribute in typeof(CommunicationsUtilities).GetTypeInfo().Assembly.GetCustomAttributes(false))
                    {
                        if (attribute is AssemblyInformationalVersionAttribute informationalVersionAttribute)
                        {
                            fileIdentity = informationalVersionAttribute.InformationalVersion;
                            break;
                        }
                    }

                    ErrorUtilities.VerifyThrow(fileIdentity != null, "Did not successfully retrieve InformationalVersion.");

                    s_fileVersionHash = GetHandshakeHashCode(complusInstallRoot ?? fileIdentity);
                    s_fileVersionChecked = true;
                }

                return s_fileVersionHash;
            }
        }

        /// <summary>
        /// Get environment block
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static unsafe extern char* GetEnvironmentStrings();

        /// <summary>
        /// Free environment block
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static unsafe extern bool FreeEnvironmentStrings(char* pStrings);

        /// <summary>
        /// Copied from the BCL implementation to eliminate some expensive security asserts.
        /// Returns key value pairs of environment variables in a new dictionary
        /// with a case-insensitive key comparer.
        /// </summary>
        internal static Dictionary<string, string> GetEnvironmentVariables()
        {
            Dictionary<string, string> table = new Dictionary<string, string>(200, StringComparer.OrdinalIgnoreCase); // Razzle has 150 environment variables

            if (NativeMethodsShared.IsWindows)
            {
                unsafe
                {
                    char* pEnvironmentBlock = null;

                    try
                    {
                        pEnvironmentBlock = GetEnvironmentStrings();
                        if (pEnvironmentBlock == null)
                        {
                            throw new OutOfMemoryException();
                        }

                        // Search for terminating \0\0 (two unicode \0's).
                        char* pEnvironmentBlockEnd = pEnvironmentBlock;
                        while (!(*pEnvironmentBlockEnd == '\0' && *(pEnvironmentBlockEnd + 1) == '\0'))
                        {
                            pEnvironmentBlockEnd++;
                        }
                        long stringBlockLength = pEnvironmentBlockEnd - pEnvironmentBlock;

                        // Copy strings out, parsing into pairs and inserting into the table.
                        // The first few environment variable entries start with an '='!
                        // The current working directory of every drive (except for those drives
                        // you haven't cd'ed into in your DOS window) are stored in the 
                        // environment block (as =C:=pwd) and the program's exit code is 
                        // as well (=ExitCode=00000000)  Skip all that start with =.
                        // Read docs about Environment Blocks on MSDN's CreateProcess page.

                        // Format for GetEnvironmentStrings is:
                        // (=HiddenVar=value\0 | Variable=value\0)* \0
                        // See the description of Environment Blocks in MSDN's
                        // CreateProcess page (null-terminated array of null-terminated strings).
                        // Note the =HiddenVar's aren't always at the beginning.
                        for (int i = 0; i < stringBlockLength; i++)
                        {
                            int startKey = i;

                            // Skip to key
                            // On some old OS, the environment block can be corrupted. 
                            // Some lines will not have '=', so we need to check for '\0'. 
                            while (*(pEnvironmentBlock + i) != '=' && *(pEnvironmentBlock + i) != '\0')
                            {
                                i++;
                            }

                            if (*(pEnvironmentBlock + i) == '\0')
                            {
                                continue;
                            }

                            // Skip over environment variables starting with '='
                            if (i - startKey == 0)
                            {
                                while (*(pEnvironmentBlock + i) != 0)
                                {
                                    i++;
                                }

                                continue;
                            }

                            string key = new string(pEnvironmentBlock, startKey, i - startKey);
                            i++;

                            // skip over '='
                            int startValue = i;

                            while (*(pEnvironmentBlock + i) != 0)
                            {
                                // Read to end of this entry
                                i++;
                            }

                            string value = new string(pEnvironmentBlock, startValue, i - startValue);

                            // skip over 0 handled by for loop's i++
                            table[key] = value;
                        }
                    }
                    finally
                    {
                        if (pEnvironmentBlock != null)
                        {
                            FreeEnvironmentStrings(pEnvironmentBlock);
                        }
                    }
                }
            }
            else
            {
                var vars = Environment.GetEnvironmentVariables();
                foreach (var key in vars.Keys)
                {
                    table[(string) key] = (string) vars[key];
                }
            }

            return table;
        }

        /// <summary>
        /// Updates the environment to match the provided dictionary.
        /// </summary>
        internal static void SetEnvironment(IDictionary<string, string> newEnvironment)
        {
            if (newEnvironment != null)
            {
                // First, empty out any new variables
                foreach (KeyValuePair<string, string> entry in CommunicationsUtilities.GetEnvironmentVariables())
                {
                    if (!newEnvironment.ContainsKey(entry.Key))
                    {
                        Environment.SetEnvironmentVariable(entry.Key, null);
                    }
                }

                // Then, make sure the old ones have their old values. 
                foreach (KeyValuePair<string, string> entry in newEnvironment)
                {
                    Environment.SetEnvironmentVariable(entry.Key, entry.Value);
                }
            }
        }

        /// <summary>
        /// Given a base handshake, generates the real handshake based on e.g. elevation level.  
        /// </summary>
        private static long GenerateHostHandshakeFromBase(long baseHandshake)
        {
#if FEATURE_SECURITY_PRINCIPAL_WINDOWS
            // If we are running in elevated privs, we will only accept a handshake from an elevated process as well.
            WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());

            // Both the client and the host will calculate this separately, and the idea is that if they come out the same
            // then we can be sufficiently confident that the other side has the same elevation level as us.  This is complementary
            // to the username check which is also done on connection.
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                unchecked
                {
                    baseHandshake = baseHandshake ^ 0x5c5c5c5c5c5c5c5c + Process.GetCurrentProcess().SessionId;
                }
            }
#endif

            // Mask out the first byte. That's because old
            // builds used a single, non zero initial byte,
            // and we don't want to risk communicating with them
            return baseHandshake;
        }

        /// <summary>
        /// Magic number sent by the host to the client during the handshake.
        /// Derived from the binary timestamp to avoid mixing binary versions.
        /// </summary>
        internal static long GetHostHandshake(HandshakeOptions nodeType)
        {
            string salt = Environment.GetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT");
            string toolsDirectory = (nodeType & HandshakeOptions.X64) == HandshakeOptions.X64 ? BuildEnvironmentHelper.Instance.MSBuildToolsDirectory64 : BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32;
            int nodeHandshakeSalt = GetHandshakeHashCode(salt + toolsDirectory);

            Trace("MSBUILDNODEHANDSHAKESALT=\"{0}\", msbuildDirectory=\"{1}\", nodeType={2}, FileVersionHash={3}", salt, toolsDirectory, nodeType, FileVersionHash);

            //FileVersionHash (32 bits) is shifted 8 bits to avoid session ID collision
            //nodeType (4 bits) is shifted just after the FileVersionHash
            //nodeHandshakeSalt (32 bits) is shifted just after hostContext
            //the most significant byte (leftmost 8 bits) will get zero'd out to avoid connecting to older builds.
            //| masked out | nodeHandshakeSalt | hostContext |              fileVersionHash             | SessionID
            //  0000 0000     0000 0000 0000        0000        0000 0000 0000 0000 0000 0000 0000 0000   0000 0000
            long baseHandshake = ((long)nodeHandshakeSalt << 44) | ((long)nodeType << 40) | ((long)FileVersionHash << 8);
            return GenerateHostHandshakeFromBase(baseHandshake);
        }

        /// <summary>
        /// Magic number sent by the client to the host during the handshake.
        /// Munged version of the host handshake.
        /// </summary>
        internal static long GetClientHandshake(HandshakeOptions hostContext)
        {
            return ~GetHostHandshake(hostContext);
        }

        /// <summary>
        /// Extension method to write a series of bytes to a stream
        /// </summary>
        internal static void WriteLongForHandshake(this PipeStream stream, long value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            // We want to read the long and send it from left to right (this means big endian)
            // if we are little endian we need to reverse the array to keep the left to right reading
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            ErrorUtilities.VerifyThrow(bytes.Length == 8, "Long should be 8 bytes");

            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Extension method to read a series of bytes from a stream
        /// </summary>
        internal static long ReadLongForHandshake(this PipeStream stream
#if NETCOREAPP2_1 || MONO
            , int handshakeReadTimeout
#endif
            )
        {
            return stream.ReadLongForHandshake((byte[])null, 0
#if NETCOREAPP2_1 || MONO
                , handshakeReadTimeout
#endif
                );
        }

        /// <summary>
        /// Extension method to read a series of bytes from a stream.
        /// If specified, leading byte matches one in the supplied array if any, returns rejection byte and throws IOException.
        /// </summary>
        internal static long ReadLongForHandshake(this PipeStream stream, byte[] leadingBytesToReject,
            byte rejectionByteToReturn
#if NETCOREAPP2_1 || MONO
            , int timeout
#endif
            )
        {
            byte[] bytes = new byte[8];

#if NETCOREAPP2_1 || MONO
            if (!NativeMethodsShared.IsWindows)
            {
                // Enforce a minimum timeout because the Windows code can pass
                // a timeout of 0 for the connection, but that doesn't work for
                // the actual timeout here.
                timeout = Math.Max(timeout, 50);

                // A legacy MSBuild.exe won't try to connect to MSBuild running
                // in a dotnet host process, so we can read the bytes simply.
                var readTask = stream.ReadAsync(bytes, 0, bytes.Length);

                // Manual timeout here because the timeout passed to Connect() just before
                // calling this method does not apply on UNIX domain socket-based
                // implementations of PipeStream.
                // https://github.com/dotnet/corefx/issues/28791
                if (!readTask.Wait(timeout))
                {
                    throw new IOException(string.Format(CultureInfo.InvariantCulture, "Did not receive return handshake in {0}ms", timeout));
                }

                readTask.GetAwaiter().GetResult();
            }
            else
#endif
            {
                // Legacy approach with an early-abort for connection attempts from ancient MSBuild.exes
                for (int i = 0; i < bytes.Length; i++)
                {
                    int read = stream.ReadByte();

                    if (read == -1)
                    {
                        // We've unexpectly reached end of stream.
                        // We are now in a bad state, disconnect on our end
                        throw new IOException(String.Format(CultureInfo.InvariantCulture, "Unexpected end of stream while reading for handshake"));
                    }

                    if (i == 0 && leadingBytesToReject != null)
                    {
                        foreach (byte reject in leadingBytesToReject)
                        {
                            if (read == reject)
                            {
                                stream.WriteByte(rejectionByteToReturn); // disconnect the host

                                throw new IOException(String.Format(CultureInfo.InvariantCulture, "Client: rejected old host. Received byte {0} but this matched a byte to reject.", bytes[i]));  // disconnect and quit
                            }
                        }
                    }

                    bytes[i] = Convert.ToByte(read);
                }
            }

            long result;

            try
            {
                // We want to read the long and send it from left to right (this means big endian)
                // If we are little endian the stream has already been reversed by the sender, we need to reverse it again to get the original number
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                result = BitConverter.ToInt64(bytes, 0 /* start index */);
            }
            catch (ArgumentException ex)
            {
                throw new IOException(String.Format(CultureInfo.InvariantCulture, "Failed to convert the handshake to big-endian. {0}", ex.Message));
            }

            return result;
        }

#if !FEATURE_APM
        internal static async Task<int> ReadAsync(Stream stream, byte[] buffer, int bytesToRead)
        {
            int totalBytesRead = 0;
            while (totalBytesRead < bytesToRead)
            {
                int bytesRead = await stream.ReadAsync(buffer, totalBytesRead, bytesToRead - totalBytesRead);
                if (bytesRead == 0)
                {
                    return totalBytesRead;
                }
                totalBytesRead += bytesRead;
            }
            return totalBytesRead;
        }
#endif

        /// <summary>
        /// Given the appropriate information, return the equivalent HandshakeOptions.
        /// </summary>
        internal static HandshakeOptions GetHandshakeOptions(bool taskHost, bool is64Bit = false, int clrVersion = 0, bool nodeReuse = false, bool lowPriority = false, IDictionary<string, string> taskHostParameters = null)
        {
            HandshakeOptions context = taskHost ? HandshakeOptions.TaskHost : HandshakeOptions.None;

            // We don't know about the TaskHost. Figure it out.
            if (taskHost && clrVersion == 0)
            {
                // Take the current TaskHost context
                if (taskHostParameters == null)
                {
                    clrVersion = typeof(bool).GetTypeInfo().Assembly.GetName().Version.Major;
                    is64Bit = XMakeAttributes.GetCurrentMSBuildArchitecture().Equals(XMakeAttributes.MSBuildArchitectureValues.x64);
                }
                else
                {
                    ErrorUtilities.VerifyThrow(taskHostParameters.ContainsKey(XMakeAttributes.runtime), "Should always have an explicit runtime when we call this method.");
                    ErrorUtilities.VerifyThrow(taskHostParameters.ContainsKey(XMakeAttributes.architecture), "Should always have an explicit architecture when we call this method.");

                    clrVersion = taskHostParameters[XMakeAttributes.runtime].Equals(XMakeAttributes.MSBuildRuntimeValues.clr4, StringComparison.OrdinalIgnoreCase) ? 4 : 2;
                    is64Bit = taskHostParameters[XMakeAttributes.architecture].Equals(XMakeAttributes.MSBuildArchitectureValues.x64);
                }
            }
            if (is64Bit)
            {
                context |= HandshakeOptions.X64;
            }
            if (clrVersion == 2)
            {
                context |= HandshakeOptions.CLR2;
            }
            if (nodeReuse)
            {
                context |= HandshakeOptions.NodeReuse;
            }
            if (lowPriority)
            {
                context |= HandshakeOptions.LowPriority;
            }
            return context;
        }

        /// <summary>
        /// Gets the value of an integer environment variable, or returns the default if none is set or it cannot be converted.
        /// </summary>
        internal static int GetIntegerVariableOrDefault(string environmentVariable, int defaultValue)
        {
            string environmentValue = Environment.GetEnvironmentVariable(environmentVariable);
            if (String.IsNullOrEmpty(environmentValue))
            {
                return defaultValue;
            }

            int localDefaultValue;
            if (Int32.TryParse(environmentValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out localDefaultValue))
            {
                defaultValue = localDefaultValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Writes trace information to a log file
        /// </summary>
        internal static void Trace(string format, params object[] args)
        {
            Trace(/* nodeId */ -1, format, args);
        }

        /// <summary>
        /// Writes trace information to a log file
        /// </summary>
        internal static void Trace(int nodeId, string format, params object[] args)
        {
            if (s_trace)
            {
                if (s_debugDumpPath == null)
                {
                    s_debugDumpPath = Environment.GetEnvironmentVariable("MSBUILDDEBUGPATH");

                    if (String.IsNullOrEmpty(s_debugDumpPath))
                    {
                        s_debugDumpPath = Path.GetTempPath();
                    }
                    else
                    {
                        Directory.CreateDirectory(s_debugDumpPath);
                    }
                }

                try
                {
                    string fileName = @"MSBuild_CommTrace_PID_{0}";
                    if (nodeId != -1)
                    {
                        fileName += "_node_" + nodeId;
                    }

                    fileName += ".txt";

                    using (StreamWriter file = FileUtilities.OpenWrite(String.Format(CultureInfo.CurrentCulture, Path.Combine(s_debugDumpPath, fileName), Process.GetCurrentProcess().Id, nodeId), append: true))
                    {
                        string message = String.Format(CultureInfo.CurrentCulture, format, args);
                        long now = DateTime.UtcNow.Ticks;
                        float millisecondsSinceLastLog = (float)(now - s_lastLoggedTicks) / 10000L;
                        s_lastLoggedTicks = now;
                        file.WriteLine("{0} (TID {1}) {2,15} +{3,10}ms: {4}", Thread.CurrentThread.Name, Thread.CurrentThread.ManagedThreadId, now, millisecondsSinceLastLog, message);
                    }
                }
                catch (IOException)
                {
                    // Ignore
                }
            }
        }

        /// <summary>
        /// Add the task host context to this handshake, to make sure that task hosts with different contexts 
        /// will have different handshakes. Shift it into the upper 32-bits to avoid running into the 
        /// session ID. The connection may be salted to allow MSBuild to only connect to nodes that come from the same
        /// test environment.
        /// </summary>
        /// <param name="hostContext">TaskHostContext</param>
        /// <returns>Base Handshake</returns>
        private static long GetBaseHandshakeForContext(HandshakeOptions hostContext)
        {
            string salt = Environment.GetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT") + BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32;
            long nodeHandshakeSalt = GetHandshakeHashCode(salt);

            Trace("MSBUILDNODEHANDSHAKESALT=\"{0}\", msbuildDirectory=\"{1}\", hostContext={2}, FileVersionHash={3}", Environment.GetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT"), BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32, hostContext, FileVersionHash);

            //FileVersionHash (32 bits) is shifted 8 bits to avoid session ID collision
            //hostContext (4 bits) is shifted just after the FileVersionHash
            //nodeHandshakeSalt (32 bits) is shifted just after hostContext
            //the most significant byte (leftmost 8 bits) will get zero'd out to avoid connecting to older builds.
            //| masked out | nodeHandshakeSalt | hostContext |              fileVersionHash             | SessionID
            //  0000 0000     0000 0000 0000        0000        0000 0000 0000 0000 0000 0000 0000 0000   0000 0000
            long baseHandshake = (nodeHandshakeSalt << 44) | ((long)hostContext << 40) | ((long)FileVersionHash << 8);
            return baseHandshake;
        }

        /// <summary>
        /// Gets a hash code for this string.  If strings A and B are such that A.Equals(B), then
        /// they will return the same hash code.
        /// This is as implemented in CLR String.GetHashCode() [ndp\clr\src\BCL\system\String.cs]
        /// but stripped out architecture specific defines
        /// that causes the hashcode to be different and this causes problem in cross-architecture handshaking
        /// </summary>
        internal static int GetHandshakeHashCode(string fileVersion)
        {
            unsafe
            {
                fixed (char* src = fileVersion)
                {
                    int hash1 = (5381 << 16) + 5381;
                    int hash2 = hash1;

                    int* pint = (int*)src;
                    int len = fileVersion.Length;
                    while (len > 0)
                    {
                        hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ pint[0];
                        if (len <= 2)
                        {
                            break;
                        }

                        hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ pint[1];
                        pint += 2;
                        len -= 4;
                    }

                    return hash1 + (hash2 * 1566083941);
                }
            }
        }
    }
}
