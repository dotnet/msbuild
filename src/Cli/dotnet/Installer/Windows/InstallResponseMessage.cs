// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json;
using static Microsoft.Win32.Msi.Error;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Response message used by the elevated server when replying to a request from
    /// the client process.
    /// </summary>
    internal class InstallResponseMessage : InstallMessageBase
    {
        public string Message
        {
            get;
            set;
        }

        /// <summary>
        /// The error code of the requested operation that failed. May be <see cref="SUCCESS"/> if
        /// an HRESULT was set.
        /// </summary>
        public uint Error
        {
            get;
            set;
        }

        /// <summary>
        /// The HRESULT of the requested operaiton that failed.
        /// </summary>
        public int HResult
        {
            get;
            set;
        }

        /// <summary>
        /// <see langword="true"/> if both <see cref="Error"/> and <see cref="HResult"/> indicates
        /// a success result.
        /// </summary>
        public bool Succeeded => HResult == S_OK && Success(Error);

        /// <summary>
        /// Creates a new <see cref="InstallResponseMessage"/> from a sequence of bytes.
        /// </summary>
        /// <param name="bytes">The raw bytes to be converted.</param>
        /// <returns>A new <see cref="InstallResponseMessage"/>.</returns>
        public static InstallResponseMessage Create(byte[] bytes)
        {
            string json = Encoding.UTF8.GetString(bytes);

            return JsonConvert.DeserializeObject<InstallResponseMessage>(json, DefaultSerializerSettings);
        }

        public static InstallResponseMessage Create(Exception e)
        {
            return new InstallResponseMessage
            {
                HResult = e.HResult,
                Message = e.Message + Environment.NewLine + e.StackTrace,
            };
        }
    }
}
