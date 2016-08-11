// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;

using Microsoft.Build.BuildEngine.Shared;
using Microsoft.Win32;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Thin wrapper around Microsoft.Win32.RegistryKey that can be 
    /// subclassed for testing purposes
    /// </summary>
    internal class RegistryKeyWrapper
    {
        // Path to the key this instance wraps
        private string registryKeyPath;
        // The key this instance wraps
        private RegistryKey wrappedKey;
        // The hive this registry key lives under
        private RegistryKey registryHive;
        // This field will be set to true when we try to open the registry key
        private bool attemptedToOpenRegistryKey = false;

        /// <summary>
        /// Initializes this RegistryKeyWrapper to wrap the specified key.
        /// Does not check for a null key.
        /// </summary>
        /// <param name="wrappedKey"></param>
        protected RegistryKeyWrapper(RegistryKey wrappedKey, RegistryKey registryHive)
        {
            this.wrappedKey = wrappedKey;
            this.registryHive = registryHive;
        }

        /// <summary>
        /// Initializes this RegistryKeyWrapper to wrap the key at the specified path
        /// and assumes the key is underneath HKLM
        /// Note that registryKeyPath should be relative to HKLM.
        /// </summary>
        /// <param name="registryKey"></param>
        internal RegistryKeyWrapper(string registryKeyPath)
            : this(registryKeyPath, Registry.LocalMachine)
        {
        }

        /// <summary>
        /// Initializes this RegistryKeyWrapper to wrap the key at the specified path
        /// </summary>
        /// <param name="registryKey"></param>
        /// <param name="registryHive"></param>
        internal RegistryKeyWrapper(string registryKeyPath, RegistryKey registryHive)
        {
            ErrorUtilities.VerifyThrowArgumentNull(registryKeyPath, "registryKeyPath");
            ErrorUtilities.VerifyThrowArgumentNull(registryHive, "registryHive");

            this.registryKeyPath = registryKeyPath;
            this.registryHive = registryHive;
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
                    if (NotExpectedException(ex))
                        throw;

                    throw new RegistryException(ex.Message, ex);
                }
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
                if (NotExpectedException(ex))
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
                return Exists() ? WrappedKey.GetValueNames() : new string[] { };
            }
            catch (Exception ex)
            {
                if (NotExpectedException(ex))
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
                return Exists() ? WrappedKey.GetSubKeyNames() : new string[] { };
            }
            catch (Exception ex)
            {
                if (NotExpectedException(ex))
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
            ErrorUtilities.VerifyThrowArgumentLength(name, "name");
            
            RegistryKeyWrapper wrapper = this;
            string[] keyNames = name.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < keyNames.Length && wrapper.Exists(); ++i)
            {
                try
                {
                    wrapper = new RegistryKeyWrapper(wrapper.WrappedKey.OpenSubKey(keyNames[i], false /* not writeable */), registryHive);
                }
                catch (Exception ex)
                {
                    if (NotExpectedException(ex))
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
            return (null != WrappedKey);
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
                if (wrappedKey == null && registryKeyPath != null && !attemptedToOpenRegistryKey)
                {
                    try
                    {
                        wrappedKey = registryHive.OpenSubKey(registryKeyPath);
                    }
                    catch (Exception ex)
                    {
                        if (NotExpectedException(ex))
                            throw;

                        throw new RegistryException(ex.Message, Name, ex);
                    }
                    finally
                    {
                        attemptedToOpenRegistryKey = true;
                    }
                }

                return wrappedKey;
            }
        }
    
        /// <summary>
        /// Returns false if this is a known exception thrown by the registry API.
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private bool NotExpectedException(Exception e)
        {
            if (e is SecurityException
             || e is UnauthorizedAccessException
             || e is IOException
             || e is ObjectDisposedException)
            {
                return false;
            }

            return true;
        }
    }
}
