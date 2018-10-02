// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
                  CultureInfo originalThreadCulture = CultureInfo.CurrentCulture;
                  CultureInfo originalThreadUICulture = CultureInfo.CurrentUICulture;
                  try
                  {
                      if (CultureInfo.CurrentCulture != culture)
                      {
#if FEATURE_THREAD_CULTURE
                          Thread.CurrentThread.CurrentCulture = culture;
#else
                          CultureInfo.CurrentCulture = culture;
#endif
                      }

                      if (CultureInfo.CurrentUICulture != uiCulture)
                      {
#if FEATURE_THREAD_CULTURE
                          Thread.CurrentThread.CurrentUICulture = uiCulture;
#else
                          CultureInfo.CurrentCulture = uiCulture;
#endif
                      }

                      callback(state);
                  }
                  finally
                  {
                      // Set the culture back to the original one so that if something else reuses this thread then it will not have a culture which it was not expecting.
                      if (CultureInfo.CurrentCulture != originalThreadCulture)
                      {
#if FEATURE_THREAD_CULTURE
                          Thread.CurrentThread.CurrentCulture = originalThreadCulture;
#else
                          CultureInfo.CurrentCulture = originalThreadCulture;
#endif
                      }

                      if (CultureInfo.CurrentUICulture != originalThreadUICulture)
                      {
#if FEATURE_THREAD_CULTURE
                          Thread.CurrentThread.CurrentUICulture = originalThreadUICulture;
#else
                          CultureInfo.CurrentUICulture = originalThreadUICulture;
#endif
                      }
                  }
              });

            return success;
        }
    }
}
