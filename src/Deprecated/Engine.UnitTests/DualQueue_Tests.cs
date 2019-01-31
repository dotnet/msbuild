// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
  [TestFixture]
  public  class DualQueue_Tests
    {
      /// <summary>
      /// Test the dual queue with multiple writers and only one reader
      /// </summary>
      /// <owner>cmann</owner>
      [Test]
      public void TestQueueEnqueueMultipleWriterOneReader()
        {
            // Queue which will contain elements added using a single Enqueue per item
            DualQueue<string> stringQueue = new DualQueue<string>();
            // Queue which will contain elements added using an EnqueueArray for a group of Items
            DualQueue<string> stringQueueTwo = new DualQueue<string>();
            // List of strings which are supposed to be in the queue
            List<string> stringsSupposedToBeInQueue  = new List<string>();
            // List of strings which are supposed to be in the queue which uses EnQueueArray
            List<string> stringsSupposedToBeInQueueTwo = new List<string>();  
            
            // Array containing our set of ManualResetEvents which is the number of threads we are going to use
            ManualResetEvent[] waitHandles = new ManualResetEvent[50];
            for (int i = 0; i < waitHandles.Length; i++)
            {
                waitHandles[i] = new ManualResetEvent(false);
                ThreadPool.QueueUserWorkItem(
                              delegate(object state)
                              {
                                  // Create three non repeating strings to put in the the different queues
                                  string string1 = System.Guid.NewGuid().ToString();
                                  string string2 = System.Guid.NewGuid().ToString();
                                  string string3 = System.Guid.NewGuid().ToString();
                                  
                                  stringQueue.Enqueue(string1);
                                  lock (stringsSupposedToBeInQueue)
                                  {
                                      stringsSupposedToBeInQueue.Add(string1);
                                  }

                                  stringQueueTwo.EnqueueArray(new string[] { string2, string3 });
                                  lock (stringsSupposedToBeInQueueTwo)
                                  {
                                      stringsSupposedToBeInQueueTwo.Add(string2);
                                      stringsSupposedToBeInQueueTwo.Add(string3);
                                  }
                                  
                                  // Say we are done the thread
                                  ((ManualResetEvent)state).Set();
                              }, waitHandles[i]);
            }
            
          // Wait for all of the threads to complete
            foreach (ManualResetEvent resetEvent in waitHandles)
            {
                resetEvent.WaitOne();
            }
          
            // Pop off items from the queue and make sure that we got all of out items back out
            int numberOfItemsInQueue = 0;
            string result = null;
            while ((result = stringQueue.Dequeue()) != null)
            {
                Assert.IsTrue(stringsSupposedToBeInQueue.Contains(result),string.Format("Expected {0} to be in the queue but it was not",result));
                stringsSupposedToBeInQueue.Remove(result);
                numberOfItemsInQueue++;
            }
            Assert.IsTrue(stringsSupposedToBeInQueue.Count == 0, "Expected all strings to be removed but they were not");
            // The number of items we processed should be the same as the number of EnQueues we did
            Assert.IsTrue(numberOfItemsInQueue == waitHandles.Length,"Expected the number of items in the queue to be the same as the number of Enqueues but it was not");

            // Pop off items from the queue and make sure that we got all of out items back out
            int numberOfItemsInQueueTwo = 0;
            string result2 = null;
            while ((result2 = stringQueueTwo.Dequeue()) != null)
            {
                Assert.IsTrue(stringsSupposedToBeInQueueTwo.Contains(result2), string.Format("Expected {0} to be in the queue number 2 but it was not", result2));
                stringsSupposedToBeInQueueTwo.Remove(result2);
                numberOfItemsInQueueTwo++;
            }
            Assert.IsTrue(stringsSupposedToBeInQueueTwo.Count == 0, "Expected all strings to be removed in queue 2 but they were not");
            // The number of items we processed should be the same as the number of EnQueues we did
            Assert.IsTrue(numberOfItemsInQueueTwo == waitHandles.Length*2, "Expected the number of items in the queue 2 to be the same as the number of Enqueues but it was not");

            // Clear the queue
            stringQueue.Clear();
            Assert.IsTrue(stringQueue.Count == 0, "The count should be zero after clearing the queue");
        }
    }
}
