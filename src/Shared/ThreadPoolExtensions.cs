// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Make an extension to the threadpool class to allow the setting of culture on queued work item.</summary>
//-----------------------------------------------------------------------

using System;
using System.Globalization;
using System.Threading;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Class to wrap the saving and restoring of the culture of a threadpool thread
    /// </summary>
    internal static class ThreadPoolExtensions
    {
        /// <summary>
        /// Queue a threadpool thread and set it to a certain culture.
        /// </summary>
        internal static bool QueueThreadPoolWorkItemWithCulture(WaitCallback callback, CultureInfo culture, CultureInfo uiCulture)
        {
            bool success = ThreadPool.QueueUserWorkItem(
              delegate (Object state)
              {
                  // Save the culture so at the end of the threadproc if something else reuses this thread then it will not have a culture which it was not expecting.
                  CultureInfo originalThreadCulture = Thread.CurrentThread.CurrentCulture;
                  CultureInfo originalThreadUICulture = Thread.CurrentThread.CurrentUICulture;
                  try
                  {
                      if (Thread.CurrentThread.CurrentCulture != culture)
                      {
                          Thread.CurrentThread.CurrentCulture = culture;
                      }

                      if (Thread.CurrentThread.CurrentUICulture != uiCulture)
                      {
                          Thread.CurrentThread.CurrentUICulture = uiCulture;
                      }

                      callback(state);
                  }
                  finally
                  {
                      // Set the culture back to the original one so that if something else reuses this thread then it will not have a culture which it was not expecting.
                      if (Thread.CurrentThread.CurrentCulture != originalThreadCulture)
                      {
                          Thread.CurrentThread.CurrentCulture = originalThreadCulture;
                      }

                      if (Thread.CurrentThread.CurrentUICulture != originalThreadUICulture)
                      {
                          Thread.CurrentThread.CurrentUICulture = originalThreadUICulture;
                      }
                  }
              });

            return success;
        }
    }
}
