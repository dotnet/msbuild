// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Threading;

#nullable disable

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
                  CultureInfo originalThreadCulture = CultureInfo.CurrentCulture;
                  CultureInfo originalThreadUICulture = CultureInfo.CurrentUICulture;
                  try
                  {
                      if (CultureInfo.CurrentCulture != culture)
                      {
                          Thread.CurrentThread.CurrentCulture = culture;
                      }

                      if (CultureInfo.CurrentUICulture != uiCulture)
                      {
                          Thread.CurrentThread.CurrentUICulture = uiCulture;
                      }

                      callback(state);
                  }
                  finally
                  {
                      // Set the culture back to the original one so that if something else reuses this thread then it will not have a culture which it was not expecting.
                      if (CultureInfo.CurrentCulture != originalThreadCulture)
                      {
                          Thread.CurrentThread.CurrentCulture = originalThreadCulture;
                      }

                      if (CultureInfo.CurrentUICulture != originalThreadUICulture)
                      {
                          Thread.CurrentThread.CurrentUICulture = originalThreadUICulture;
                      }
                  }
              });

            return success;
        }
    }
}
