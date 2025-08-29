﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// A dummy element location.
    /// </summary>
    public class MockElementLocation : ElementLocation
    {
        /// <summary>
        /// Single instance
        /// </summary>
        private static MockElementLocation s_instance = new MockElementLocation();

        private string _file = "mock.targets";


        /// <summary>
        /// Initializes a new instance of the MockElementLocation class.
        /// </summary>
        /// <param name="file">The path of the file to use.</param>
        public MockElementLocation(string file)
        {
            _file = file;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        private MockElementLocation()
        {
        }

        /// <summary>
        /// File of element, eg a targets file
        /// </summary>
        public override string File
        {
            get { return _file; }
        }

        /// <summary>
        /// Line number
        /// </summary>
        public override int Line
        {
            get { return 0; }
        }

        /// <summary>
        /// Column number
        /// </summary>
        public override int Column
        {
            get { return 1; }
        }

        /// <summary>
        /// Get single instance
        /// </summary>
        internal static MockElementLocation Instance
        {
            get { return s_instance; }
        }
    }
}
