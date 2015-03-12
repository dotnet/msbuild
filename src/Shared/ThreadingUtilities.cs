//-----------------------------------------------------------------------
// <copyright file="ThreadingUtilities.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Utilities relating to threading.</summary>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Threading related utility methods.
    /// </summary>
    internal static class ThreadingUtilities
    {
        /// <summary>
        /// Waits for a signal on a handle and guarantees no COM STA pumping.
        /// </summary>
        internal static bool WaitOneNoMessagePump(this WaitHandle handle)
        {
            int index = WaitAnyNoMessagePump(new WaitHandle[] { handle }, Timeout.Infinite);

            return (index != WaitHandle.WaitTimeout);
        }

        /// <summary>
        /// Waits for a signal on a handle and guarantees no COM STA pumping.
        /// </summary>
        internal static bool WaitOneNoMessagePump(this WaitHandle handle, TimeSpan timeout)
        {
            int index = WaitNoMessagePump(new WaitHandle[] { handle }, timeout);

            return (index != WaitHandle.WaitTimeout);
        }

        /// <summary>
        /// Waits for a signal on a handle and guarantees no COM STA pumping.
        /// </summary>
        internal static bool WaitOneNoMessagePump(this WaitHandle handle, int milliseconds)
        {
            int index = WaitNoMessagePump(new WaitHandle[] { handle }, new TimeSpan(0, 0, 0, 0, milliseconds));

            return (index != WaitHandle.WaitTimeout);
        }

        /// <summary>
        /// Waits for a signal on a set of handles and guarantees no COM STA pumping.
        /// Same semantics as WaitHandle.WaitAny: returns the index of the handle, or WaitHandle.