using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Internal;

namespace Microsoft.Build.BackEnd
{
    internal class EntryNodeHandshake : IHandshake
    {
        readonly int _options;
        readonly int _salt;
        readonly int _fileVersionMajor;
        readonly int _fileVersionMinor;
        readonly int _fileVersionBuild;
        readonly int _fileVersionRevision;

        internal EntryNodeHandshake(HandshakeOptions nodeType, string msBuildLocation)
        {
            // We currently use 6 bits of this 32-bit integer. Very old builds will instantly reject any handshake that does not start with F5 or 06; slightly old builds always lead with 00.
            // This indicates in the first byte that we are a modern build.
            _options = (int)nodeType | (CommunicationsUtilities.handshakeVersion << 24);
            string? handshakeSalt = Environment.GetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT");
            var msBuildFile = new FileInfo(msBuildLocation);
            var msBuildDirectory = msBuildFile.DirectoryName;
            _salt = ComputeHandshakeHash(handshakeSalt + msBuildDirectory);
            Version fileVersion = new Version(FileVersionInfo.GetVersionInfo(msBuildLocation).FileVersion ?? string.Empty);
            _fileVersionMajor = fileVersion.Major;
            _fileVersionMinor = fileVersion.Minor;
            _fileVersionBuild = fileVersion.Build;
            _fileVersionRevision = fileVersion.Revision;
        }

        internal const int EndOfHandshakeSignal = -0x2a2a2a2a;

        /// <summary>
        /// Compute stable hash as integer
        /// </summary>
        private static int ComputeHandshakeHash(string fromString)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(fromString));

            return BitConverter.ToInt32(bytes, 0);
        }

        internal static int AvoidEndOfHandshakeSignal(int x)
        {
            return x == EndOfHandshakeSignal ? ~x : x;
        }

        public int[] RetrieveHandshakeComponents()
        {
            return new int[]
            {
                AvoidEndOfHandshakeSignal(_options),
                AvoidEndOfHandshakeSignal(_salt),
                AvoidEndOfHandshakeSignal(_fileVersionMajor),
                AvoidEndOfHandshakeSignal(_fileVersionMinor),
                AvoidEndOfHandshakeSignal(_fileVersionBuild),
                AvoidEndOfHandshakeSignal(_fileVersionRevision),
            };
        }

        public string GetKey()
        {
            return $"{_options} {_salt} {_fileVersionMajor} {_fileVersionMinor} {_fileVersionBuild} {_fileVersionRevision}"
                .ToString(CultureInfo.InvariantCulture);
        }

        public byte? ExpectedVersionInFirstByte => null;

        /// <summary>
        /// Computes Handshake stable hash string representing whole state of handshake.
        /// </summary>
        public string ComputeHash()
        {
            var input = GetKey();
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes)
                .Replace("/", "_")
                .Replace("=", string.Empty);
        }
    }
}
