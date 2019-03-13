// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices.ComTypes;

using Marshal = System.Runtime.InteropServices.Marshal;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Helper class for managing native allocations and tracking possible memory leaks.
    /// </summary>
    public class MockUnmanagedMemoryHelper
    {
        private List<IntPtr> _allocatedHandles;

        // Zero if we're allocating independent chunks of memory; 
        // Something else if we're allocating connected chunks of memory that we'll want to release with one ReleaseHandle
        private IntPtr _mainAllocationHandle = IntPtr.Zero;

        // List of linked allocations that we want to release when releasing the <KEY> IntPtr
        private Dictionary<IntPtr, List<IntPtr>> _dependentAllocations = new Dictionary<IntPtr, List<IntPtr>>();

        /// <summary>
        /// Public constructor
        /// </summary>
        public MockUnmanagedMemoryHelper()
        {
            _allocatedHandles = new List<IntPtr>();
        }

        /// <summary>
        /// Allocate a native handle for a buffer of cb bytes
        /// </summary>
        /// <param name="cb"></param>
        /// <returns></returns>
        public IntPtr AllocateHandle(int cb)
        {
            IntPtr handle = Marshal.AllocHGlobal(cb);

            // If this is a dependent allocation, add it to the list of dependencies
            if (_mainAllocationHandle != IntPtr.Zero)
            {
                if (!_dependentAllocations.ContainsKey(_mainAllocationHandle))
                {
                    _dependentAllocations.Add(_mainAllocationHandle, new List<IntPtr>());
                }

                _dependentAllocations[_mainAllocationHandle].Add(handle);
            }
            else
            {
                _allocatedHandles.Add(handle);
            }

            return handle;
        }

        /// <summary>
        /// Release a handle and its dependent handles if any
        /// </summary>
        /// <param name="handle"></param>
        public void FreeHandle(IntPtr handle)
        {
            Assert.True(_allocatedHandles.Exists(new Predicate<IntPtr>(
                delegate (IntPtr ptr) { return ptr == handle; }
            )));
            Marshal.FreeHGlobal(handle);
            _allocatedHandles.Remove(handle);

            // Any dependencies? Free them as well
            if (_dependentAllocations.ContainsKey(handle))
            {
                while (_dependentAllocations[handle].Count > 0)
                {
                    Marshal.FreeHGlobal(_dependentAllocations[handle][0]);
                    _dependentAllocations[handle].RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Tells us that we're going to allocate dependent handles for the given handle
        /// </summary>
        /// <param name="mainAllocation"></param>
        public void EnterSubAllocationScope(IntPtr mainAllocation)
        {
            Assert.Equal(IntPtr.Zero, _mainAllocationHandle);

            _mainAllocationHandle = mainAllocation;
        }

        /// <summary>
        /// Tells us we're no longer allocating dependent handles.
        /// </summary>
        public void ExitSubAllocationScope()
        {
            Assert.NotEqual(IntPtr.Zero, _mainAllocationHandle);

            _mainAllocationHandle = IntPtr.Zero;
        }

        /// <summary>
        /// Helper method for making sure this object has no unreleased memory handles
        /// </summary>
        public void AssertAllHandlesReleased()
        {
            Assert.Empty(_allocatedHandles);
        }
    }
}
