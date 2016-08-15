// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Reflection;
using Microsoft.Build.BuildEngine;
using NUnit.Framework;
using System.Collections;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using System.Collections.Generic;

    
namespace Microsoft.Build.UnitTests
{
    /*
     * Class:   ObjectModelHelpers
     * Owner:   jomof
     *
     * Utility methods for unit tests that work through the object model.
     *
     */
    sealed public class EngineHelpers        
    {
        internal static string EnsureNoLeadingSlash(string path)
        {
            if (path.Length > 0 && IsSlash(path[0]))
            {
                path = path.Substring(1);
            }

            return path;
        }

        internal static string EnsureNoTrailingSlash(string path)
        {
            if (EndsWithSlash(path))
            {
                path = path.Substring(0, path.Length - 1);
            }

            return path;
        }

        internal static bool EndsWithSlash(string fileSpec)
        {
            return (fileSpec.Length > 0)
                ? IsSlash(fileSpec[fileSpec.Length - 1])
                : false;
        }

        internal static bool IsSlash(char c)
        {
            return ((c == Path.DirectorySeparatorChar) || (c == Path.AltDirectorySeparatorChar));
        }

        /// <summary>
        /// Amazingly sophisticated :) helper function to determine if the set of ITaskItems returned from 
        /// a task match the expected set of ITaskItems.  It can also check that the ITaskItems have the expected
        /// metadata, and that the ITaskItems are returned in the correct order.
        /// 
        /// The "expectedItemsString" is a formatted way of easily specifying which items you expect to see.
        /// The format is:
        /// 
        ///         itemspec1 :   metadataname1=metadatavalue1 ; metadataname2=metadatavalue2 ; ...
        ///         itemspec2 :   metadataname3=metadatavalue3 ; metadataname4=metadatavalue4 ; ...
        ///         itemspec3 :   metadataname5=metadatavalue5 ; metadataname6=metadatavalue6 ; ...
        /// 
        /// (Each item needs to be on its own line.)
        /// 
        /// </summary>
        /// <param name="expectedItemsString"></param>
        /// <param name="actualItems"></param>
        /// <owner>RGoel</owner>
        static internal void AssertItemsMatch
            (
            string expectedItemsString, 
            BuildItem[] actualItems
            )
        {
            AssertItemsMatch(expectedItemsString, actualItems, true);
        }

        /// <summary>
        /// Amazingly sophisticated :) helper function to determine if the set of ITaskItems returned from 
        /// a task match the expected set of ITaskItems.  It can also check that the ITaskItems have the expected
        /// metadata, and that the ITaskItems are returned in the correct order.
        /// 
        /// The "expectedItemsString" is a formatted way of easily specifying which items you expect to see.
        /// The format is:
        /// 
        ///         itemspec1 :   metadataname1=metadatavalue1 ; metadataname2=metadatavalue2 ; ...
        ///         itemspec2 :   metadataname3=metadatavalue3 ; metadataname4=metadatavalue4 ; ...
        ///         itemspec3 :   metadataname5=metadatavalue5 ; metadataname6=metadatavalue6 ; ...
        /// 
        /// (Each item needs to be on its own line.)
        /// 
        /// </summary>
        /// <param name="expectedItemsString"></param>
        /// <param name="actualItems"></param>
        /// <owner>RGoel</owner>
        static internal void AssertItemsMatch
            (
            string expectedItemsString, 
            BuildItem[] actualItems, 
            bool orderOfItemsShouldMatch
            )
        {
            ITaskItem[] actualTaskItems = new ITaskItem[actualItems.Length];

            int i = 0;
            foreach (BuildItem actualItem in actualItems)
            {
                actualTaskItems[i++] = new Microsoft.Build.BuildEngine.TaskItem(actualItem);
            }

            ObjectModelHelpers.AssertItemsMatch(expectedItemsString, actualTaskItems, orderOfItemsShouldMatch);
        }

        /// <summary>
        /// Amazingly sophisticated :) helper function to determine if the set of ITaskItems returned from 
        /// a task match the expected set of ITaskItems.  It can also check that the ITaskItems have the expected
        /// metadata, and that the ITaskItems are returned in the correct order.
        /// 
        /// The "expectedItemsString" is a formatted way of easily specifying which items you expect to see.
        /// The format is:
        /// 
        ///         itemspec1 :   metadataname1=metadatavalue1 ; metadataname2=metadatavalue2 ; ...
        ///         itemspec2 :   metadataname3=metadatavalue3 ; metadataname4=metadatavalue4 ; ...
        ///         itemspec3 :   metadataname5=metadatavalue5 ; metadataname6=metadatavalue6 ; ...
        /// 
        /// (Each item needs to be on its own line.)
        /// 
        /// </summary>
        /// <param name="expectedItemsString"></param>
        /// <param name="actualItems"></param>
        /// <owner>RGoel</owner>
        static internal void AssertItemsMatch
            (
            string expectedItemsString, 
            BuildItemGroup actualItems
            )
        {
            AssertItemsMatch(expectedItemsString, actualItems, true);
        }

        /// <summary>
        /// Amazingly sophisticated :) helper function to determine if the set of ITaskItems returned from 
        /// a task match the expected set of ITaskItems.  It can also check that the ITaskItems have the expected
        /// metadata, and that the ITaskItems are returned in the correct order.
        /// 
        /// The "expectedItemsString" is a formatted way of easily specifying which items you expect to see.
        /// The format is:
        /// 
        ///         itemspec1 :   metadataname1=metadatavalue1 ; metadataname2=metadatavalue2 ; ...
        ///         itemspec2 :   metadataname3=metadatavalue3 ; metadataname4=metadatavalue4 ; ...
        ///         itemspec3 :   metadataname5=metadatavalue5 ; metadataname6=metadatavalue6 ; ...
        /// 
        /// (Each item needs to be on its own line.)
        /// 
        /// </summary>
        /// <param name="expectedItemsString"></param>
        /// <param name="actualItems"></param>
        /// <owner>RGoel</owner>
        static internal void AssertItemsMatch
            (
            string expectedItemsString, 
            BuildItemGroup actualItems, 
            bool orderOfItemsShouldMatch
            )
        {
            AssertItemsMatch(expectedItemsString, actualItems.ToArray(), orderOfItemsShouldMatch);
        }
    }
}
