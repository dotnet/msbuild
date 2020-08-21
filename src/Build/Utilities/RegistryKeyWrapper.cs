// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if FEATURE_WIN32_REGISTRY

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;

using Microsoft.Build.Shared;
using Microsoft.Win32;
using RegistryException = Microsoft.Build.Exceptions.RegistryException;

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// Thin wrapper around Microsoft.Win32.RegistryKey that can be 
    /// subclassed for testing purposes
    /// </summary>
    internal class RegistryKeyWrapper : IDisposable
    {
        // Path to the key this instance wraps
        private string _registryKeyPath;
        // The key this instance wraps
        private RegistryKey _wrappedKey;
        // The hive this registry key lives under
        private RegistryKey _registryHive;
        // This field will be set to true when we try to open the registry key
        private bool _attemptedToOpenRegistryKey = false;

        /// <summary>
        /// Has the object been disposed yet.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes this RegistryKeyWrapper to wrap the specified key.
        /// Does not check for a null key.
        /// </summary>
        protected RegistryKeyWrapper(RegistryKey wrappedKey, RegistryKey registryHive)
        {
            _wrappedKey = wrappedKey;
            _registryHive = registryHive;
        }

        /// <summary>
        /// Initializes this RegistryKeyWrapper to wrap the key at the specified path
        /// and assumes the key is underneath HKLM
        /// Note that registryKeyPath should be relative to HKLM.
        /// </summary>
        internal RegistryKeyWrapper(string registryKeyPath)
            : this(registryKeyPath, Registry.LocalMachine)
        {
        }

        /// <summary>
        /// Initializes this RegistryKeyWrapper to wrap the key at the specified path
        /// </summary>
        internal RegistryKeyWrapper(string registryKeyPath, RegistryHive registryHive, RegistryView registryView)
            : this(registryKeyPath, RegistryKey.OpenBaseKey(registryHive, registryView))
        {
        }

        /// <summary>
        /// Initializes this RegistryKeyWrapper to wrap the key at the specified path
        /// </summary>
        internal RegistryKeyWrapper(string registryKeyPath, RegistryKey registryHive)
        {
            ErrorUtilities.VerifyThrowArgumentNull(registryKeyPath, nameof(registryKeyPath));
            ErrorUtilities.VerifyThrowArgumentNull(registryHive, nameof(registryHive));

            _registryKeyPath = registryKeyPath;
            _registryHive = registryHive;
        }

        /// <summary>
        /// Name of the registry key
        /// </summary>
        public virtual string Name
        {
            get
            {
                try
                {
                    return Exists() ? WrappedKey.Name : string.Empty;
                }
                catch (Exception ex)
                {
                    if (ExceptionHandling.NotExpectedRegistryException(ex))
                        throw;

                    throw new RegistryException(ex.Message, ex);
                }
            }
        }

        /// <summary>
        /// Convenient static helper method on RegistryKeyWrapper, for when someone is only intersted in knowing 
        /// whether a particular registry key exists or not.
        /// </summary>
        public static bool KeyExists(string registryKeyPath, RegistryHive registryHive, RegistryView registryView)
        {
            using (RegistryKeyWrapper wrapper = new RegistryKeyWrapper(registryKeyPath, registryHive, registryView))
            {
                return wrapper.Exists();
            }
        }

        /// <summary>
        /// Gets the value with name "name" stored under this registry key
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public virtual object GetValue(string name)
        {
            try
            {
                return Exists() ? WrappedKey.GetValue(name) : null;
            }
            catch (Exception ex)
            {
                if (ExceptionHandling.NotExpectedRegistryException(ex))
                    throw;

                throw new RegistryException(ex.Message, Name + "@" + name, ex);
            }
        }

        /// <summary>
        /// Gets the names of all values underneath this registry key
        /// </summary>
        /// <returns></returns>
        public virtual string[] GetValueNames()
        {
            try
            {
                return Exists() ? WrappedKey.GetValueNames() : Array.Empty<string>();
            }
            catch (Exception ex)
            {
                if (ExceptionHandling.NotExpectedRegistryException(ex))
                    throw;

                throw new RegistryException(ex.Message, Name, ex);
            }
        }

        /// <summary>
        /// Gets the names of all sub keys immediately below this registry key
        /// </summary>
        /// <returns></returns>
        public virtual string[] GetSubKeyNames()
        {
            try
            {
                return Exists() ? WrappedKey.GetSubKeyNames() : Array.Empty<string>();
            }
            catch (Exception ex)
            {
                if (ExceptionHandling.NotExpectedRegistryException(ex))
                    throw;

                throw new RegistryException(ex.Message, Name, ex);
            }
        }

        /// <summary>
        /// Returns the RegistryKeyWrapper around the sub key with name "name". If that does
        /// not exist, returns a RegistryKeyWrapper around null.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public virtual RegistryKeyWrapper OpenSubKey(string name)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, nameof(name));

            RegistryKeyWrapper wrapper = this;
            string[] keyNames = name.Split(MSBuildConstants.BackslashChar, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < keyNames.Length && wrapper.Exists(); ++i)
            {
                try
                {
                    wrapper = new RegistryKeyWrapper(wrapper.WrappedKey.OpenSubKey(keyNames[i], false /* not writeable */), _registryHive);
                }
                catch (Exception ex)
                {
                    if (ExceptionHandling.NotExpectedRegistryException(ex))
                        throw;

                    throw new RegistryException(ex.Message, wrapper.Name + "\\" + keyNames[i], ex);
                }
            }

            return wrapper;
        }

        /// <summary>
        /// Returns true if the wrapped registry key exists.
        /// </summary>
        /// <returns></returns>
        public virtual bool Exists()
        {
            return WrappedKey != null;
        }

        /// <summary>
        /// Lazy getter for the root tools version registry key: means that this class
        /// will never throw registry exceptions from the constructor
        /// </summary>
        private RegistryKey WrappedKey
        {
            get
            {
                // If we haven't wrapped a key yet, and we got a path to look at,
                // and we haven't tried to look there yet
                if (_wrappedKey == null && _registryKeyPath != null && !_attemptedToOpenRegistryKey)
                {
                    try
                    {
                        _wrappedKey = _registryHive.OpenSubKey(_registryKeyPath);
                    }
                    catch (Exception ex)
                    {
                        if (ExceptionHandling.NotExpectedRegistryException(ex))
                            throw;

                        throw new RegistryException(ex.Message, _wrappedKey == null ? string.Empty : Name, ex);
                    }
                    finally
                    {
                        _attemptedToOpenRegistryKey = true;
                    }
                }

                return _wrappedKey;
            }
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_wrappedKey != null)
                    {
                        _wrappedKey.Dispose();
                        _wrappedKey = null;
                    }

                    if (_registryHive != null)
                    {
                        _registryHive.Dispose();
                        _registryHive = null;
                    }
                }

                // Note disposing has been done.
                _disposed = true;
            }
        }
    }
}
#endif
