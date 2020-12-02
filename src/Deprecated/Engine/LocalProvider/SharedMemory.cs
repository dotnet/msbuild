// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.Win32.SafeHandles;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    internal enum SharedMemoryType
    {
        ReadOnly,
        WriteOnly
    }

    /// <summary>
    /// The shared memory is used to transmit serialized LocalCallDescriptors.
    /// These local call descriptors encapsulate commands and data that needs
    /// to be communicated between the parent and child objects. This enumeration
    /// is used by the shared memory to mark what kind of LocalCallDescriptor
    /// object is in the shared memory so it can be correctly deserialized.
    /// This marker is placed at the front of object in the shared memory.
    /// Enumeration of LocalCallDescriptor Types
    /// </summary>
    internal enum ObjectType
    {
        // Has the object been serialized using .net serialization (binary formatter)
        NetSerialization = 1,
        // Used to mark that the next int read represents how many bytes are in the
        // large object which is about to be sent      
        FrameMarker = 2,
        // Mark the end of the batch in sharedMemory.
        EndMarker = 3,
        // Below are the enumeration values are for messages / commands which are
        // passed between the child and the parent processes
        PostBuildRequests = 4,
        PostBuildResult = 5,
        PostLoggingMessagesToHost = 6,
        UpdateNodeSettings = 7,
        RequestStatus = 8,
        PostStatus = 9,
        InitializeNode = 10,
        InitializationComplete = 11,
        ShutdownNode = 12,
        ShutdownComplete = 13,
        PostIntrospectorCommand = 14,
        GenericSingleObjectReply = 15,
        PostCacheEntriesToHost = 16,
        GetCacheEntriesFromHost = 17
    }

    /// <summary>
    /// This class is responsible for providing a communication channel between
    /// a child process and a parent process. Each process (child or parent) will
    /// have two SharedMemory class instances, one for reading and one for writing.
    /// For example, a parent will have one shared memory class to "read" data
    /// sent from the child and one "write" shared The shared memory communicates
    /// through named shared memory regions.
    /// </summary>
    internal class SharedMemory : IDisposable
    {
        #region Construction

        private SharedMemory()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">
        /// The name the shared memory will be given, this is combination of node,
        /// username, admin status, and some other ones,
        /// see LocalNodeProviderGlobalNames.NodeInputMemoryName for greater detail.
        /// </param>
        /// <param name="type">
        ///  This type determines which lock and stream needs to be instantiated
        ///  within the shared memory class. For example,
        ///  read only means, only create a memory stream,
        ///  a read lock and a backing byte array and a binary reader. A write
        ///  only type means,  create a memory stream, write lock and a binary writer.
        ///  This type however does not set the type of the memory mapped section,
        ///  the memory mapped section itself is created
        ///  with READWRITE access.
        ///</param>
        /// <param name="allowExistingMapping">
        ///  The shared memory is given a parameter to determine whether or not to
        ///  reuse an existing mapped memory secion. When the node is first created
        ///  this is false, however when the shared memory threads are created this
        ///  is true. We do this because we create the shared memory when the node
        ///  is created, at this point the there should be no shared memory with the
        ///  same name. However when we create the reader and writer threads
        ///  (which happens on node reuse) we want to reuse the memory.
        ///</param>
        internal SharedMemory(string name, SharedMemoryType type, bool allowExistingMapping)
        {
            this.type = type;

            InitializeMemoryMapping(name, allowExistingMapping);

            writeBytesRemaining = 0;
            readBytesRemaining = 0;
            readBytesTotal = 0;
            largeObjectsQueue = null;

            // Has the shared memory been properly created and is ready to use
            if (IsUsable)
            {
                // Setup the structures for either a read only or write only stream
                InitializeStreams(type);
                try
                {
                    // This could fail if two different administrator accounts try and 
                    // access each others nodes as events and semaphores are protected
                    // against cross account access
                    InitializeSynchronization();
                }
                catch (System.UnauthorizedAccessException)
                {
                    if (writeStream != null)
                    {
                        // Closes binary writer and the underlying stream
                        binaryWriter.Close();
                    }

                    if (readStream != null)
                    {
                        // Closes binary reader and the underlying stream
                        binaryReader.Close();
                    }

                    NativeMethods.UnmapViewOfFile(pageFileView);
                    pageFileMapping.Dispose();
                }
            }
        }

        /// <summary>
        /// Creates the shared memory region and map a view to it.
        /// </summary>
        private void InitializeMemoryMapping(string memoryMapName, bool allowExistingMapping)
        {
            // Null means use the default security permissions
            IntPtr pointerToSecurityAttributes = NativeMethods.NullPtr;
            IntPtr pSDNative = IntPtr.Zero;
            try
            {
                // Check to see if the user is an administrator, this is done to prevent non 
                // administrator processes from accessing the shared memory. On a vista machine 
                // the check does not differentiate beween the application being elevated to have
                // administrator rights or the application being started with administrator rights.
                // If the user is an administator create a new set of securityAttributes which make
                // the shared memory only accessable to administrators.
                if (NativeMethods.IsUserAdministrator())
                {
                    NativeMethods.SECURITY_ATTRIBUTES saAttr = new NativeMethods.SECURITY_ATTRIBUTES();
                    uint pSDLength = 0;
                    if (!NativeMethods.ConvertStringSecurityDescriptorToSecurityDescriptor(NativeMethods.ADMINONLYSDDL, NativeMethods.SECURITY_DESCRIPTOR_REVISION, ref pSDNative, ref pSDLength))
                    {
                        throw new System.ComponentModel.Win32Exception();
                    }

                    saAttr.bInheritHandle = 0;
                    saAttr.nLength = Marshal.SizeOf(typeof(NativeMethods.SECURITY_ATTRIBUTES));
                    saAttr.lpSecurityDescriptor = pSDNative;
                    pointerToSecurityAttributes = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeMethods.SECURITY_ATTRIBUTES)));
                    Marshal.StructureToPtr(saAttr, pointerToSecurityAttributes, true);
                }

               // The file mapping has either the default (current user) security permissions or 
               // permissions restricted to only administrator users depending on the check above.
               // If pointerToSecurityAttributes is null the default permissions are used.
               this.pageFileMapping =
                    NativeMethods.CreateFileMapping
                    (
                        NativeMethods.InvalidHandle,
                        pointerToSecurityAttributes,
                        NativeMethods.PAGE_READWRITE,
                        0,
                        size + 4,
                        memoryMapName
                    );

                // If only new mappings are allowed and the current one has been created by somebody else
                // delete the mapping. Note that we would like to compare the GetLastError value against
                // ERROR_ALREADY_EXISTS but CLR sometimes overwrites the last error so to be safe we'll
                // not reuse the node for any unsuccessful value.
                if (!allowExistingMapping && Marshal.GetLastWin32Error() != NativeMethods.ERROR_SUCCESS)
                {
                    if (!pageFileMapping.IsInvalid && !pageFileMapping.IsClosed)
                    {
                        NativeMethods.UnmapViewOfFile(pageFileView);
                        pageFileMapping.Close();
                    }
                }
            }
            finally
            {
                NativeMethods.LocalFree(pointerToSecurityAttributes);
                NativeMethods.LocalFree(pSDNative);
            }

            if (!this.pageFileMapping.IsInvalid && !pageFileMapping.IsClosed)
            {
                // Maps a view of a file mapping into the address space of the calling process so that we can use the 
                // view to read and write to the shared memory region.
                this.pageFileView =
                    NativeMethods.MapViewOfFile
                    (
                        this.pageFileMapping,
                        NativeMethods.FILE_MAP_ALL_ACCESS, // Give the map read, write, and copy access
                        0,  // Start mapped view at high order offset 0
                        0,  // Start mapped view at low order offset 0
                         // The size of the shared memory plus some extra space for an int
                         // to write the number of bytes written
                        (IntPtr)(size + 4)
                    );

                // Check to see if the file view has been created on the fileMapping.
                if (this.pageFileView == NativeMethods.NullPtr)
                {
                    // Make the shared memory not usable.
                    this.pageFileMapping.Close();
                }
                else
                {
                    this.name = memoryMapName;
                }
            }
        }

        /// <summary>
        /// Initialize the MemoryStreams which will be used to contain the serialized data from the LocalCallDescriptors.
        /// </summary>
        private void InitializeStreams(SharedMemoryType streamType)
        {
            // Initialize the .net binary formatter in case we need to use .net serialization.
            this.binaryFormatter = new BinaryFormatter();
            this.loggingTypeCache = new Hashtable();

            if (streamType == SharedMemoryType.ReadOnly)
            {
                this.readBuffer = new byte[size];
                this.readStream = new MemoryStream(this.readBuffer);
                this.binaryReader = new BinaryReader(this.readStream);
                readLock = new object();
            }
            else if (streamType == SharedMemoryType.WriteOnly)
            {
                this.writeStream = new MemoryStream();
                writeLock = new object();
                this.binaryWriter = new BinaryWriter(this.writeStream);
            }
            else
            {
                ErrorUtilities.VerifyThrow(false, "Unknown shared memory type.");
            }
        }

        /// <summary>
        /// Initialize the synchronization variables which will be used to communicate the status of the shared memory between processes.
        /// </summary>
        private void InitializeSynchronization()
        {
            this.unreadBatchCounter = new Semaphore(0, size, this.name + "UnreadBatchCounter");
            this.fullFlag = new EventWaitHandle(false, EventResetMode.ManualReset, this.name + "FullFlag");
            this.notFullFlag = new EventWaitHandle(true, EventResetMode.ManualReset, this.name + "NotFullFlag");
            this.readActionCounter = new Semaphore(0, size + /* for full-flag */ 1, this.name + "ReadActionCounter");
            // Make sure the count of unread batches is 0
            while (NumberOfUnreadBatches > 0)
            {
                DecrementUnreadBatchCounter();
            }
        }

        #endregion

        #region Disposal

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (IsUsable)
                {
                    NativeMethods.UnmapViewOfFile(pageFileView);
                    pageFileMapping.Dispose();

                    unreadBatchCounter.Close();
                    fullFlag.Close();
                    notFullFlag.Close();
                    readActionCounter.Close();
                }

                if (writeStream != null)
                {
                    // Closes binary writer and the underlying stream
                    binaryWriter.Close();
                }

                if (readStream != null)
                {
                    // Closes binary reader and the underlying stream
                    binaryReader.Close();
                }

                // Set the sentinel.
                disposed = true;

                // Suppress finalization of this disposed instance.
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~SharedMemory()
        {
            Dispose();
        }

        #endregion

        #region Properties
        /// <summary>
        ///  Indicates the shared memory region been created and initialized properly.
        /// </summary>
        internal bool IsUsable
        {
            get
            {
                return !pageFileMapping.IsInvalid &&
                    !pageFileMapping.IsClosed &&
                    (pageFileView != NativeMethods.NullPtr);
            }
        }

        /// <summary>
        /// Returns the readActionCounter as a WaitHandle. This WaitHandle is used
        /// to notify the SharedMemory reader threads that there is something ready
        /// in the shared memory to be read. The ReadFlag will remain set as long as
        /// the number of times the shared memory has been read is less than the
        /// number of times writer thread has written to the shared memory.
        /// </summary>
        internal WaitHandle ReadFlag
        {
            get
            {
                return readActionCounter;
            }
        }

        /// <summary>
        /// Indicates when the SharedMemory is full
        /// </summary>
        private bool IsFull
        {
            get
            {
                // If the flag is set true is returned
                // A timeout of 0 means the WaitOne will time out 
                // instantly and return false if the flag is not set.
                return fullFlag.WaitOne(0, false);
            }
        }
        /// <summary>
        /// The NumberOfUnreadBatches is the number of "batches" written to shared
        /// memory which have not been read yet by the ReaderThread. A batch
        /// contains one or more serialized objects.
        /// </summary>
        private int NumberOfUnreadBatches
        {
            get
            {
                // Relese the semaphore, this will return the number of times the
                // semaphore was entered into. This value reflects the count before
                // the release is taken into account.
                int numberOfUnreadBatches = unreadBatchCounter.Release();
                // Decrement the semaphore to offset the increment used to get the count.
                unreadBatchCounter.WaitOne();

                return numberOfUnreadBatches;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// The shared memory is now full, set the correct synchronization variables to
        /// inform the reader thread of this situation.
        /// </summary>
        private void MarkAsFull()
        {
            fullFlag.Set();
            notFullFlag.Reset();
            readActionCounter.Release();
        }

        /// <summary>
        /// The shared memory is no longer full. Set the correct synchronization variables
        /// to inform the writer thread of this situation.
        /// </summary>
        private void MarkAsNotFull()
        {
            fullFlag.Reset();
            notFullFlag.Set();
        }

        /// <summary>
        /// A batch is now in the shared memory and is ready to be read by the reader thread.
        /// </summary>
        private void IncrementUnreadBatchCounter()
        {
            // Release increments a semaphore
            // http://msdn2.microsoft.com/en-us/library/system.threading.semaphore.aspx
            unreadBatchCounter.Release();
            readActionCounter.Release();
        }

        /// <summary>
        /// A batch has just been read out of shared memory.
        /// </summary>
        private void DecrementUnreadBatchCounter()
        {
            // WaitOne decrements the semaphore
            unreadBatchCounter.WaitOne();
        }

        /// <summary>
        /// This function write out a set of objects into the shared buffer.
        /// In normal operation all the objects in the queue are serialized into
        /// the buffer followed by an end marker class. If the buffer is not big
        /// enough to contain a single object the object is broken into
        /// multiple buffers as follows - first a frame marker is sent containing
        /// the size of the serialized object + size of end marker. The reader makes
        /// sure upon receiving the frame marker that its buffer is large enough
        /// to contain the object about to be sent. After the frame marker the object
        /// is sent as a series of buffers until all of it is written out.
        /// </summary>
        /// <param name="objectsToWrite"> Queue of objects to be sent (mostly logging messages)</param>
        /// <param name="objectsToWriteHiPriority">Queue of high priority objects (these are commands and statuses) </param>
        /// <param name="blockUntilDone"> If true the function will block until both queues are empty</param>
        internal void Write(DualQueue<LocalCallDescriptor> objectsToWrite, DualQueue<LocalCallDescriptor> objectsToWriteHiPriority, bool blockUntilDone)
        {
            Debug.Assert(type == SharedMemoryType.WriteOnly, "Should only be calling Write from a writeonly shared memory object");

            lock (writeLock)
            {
                // Loop as long as there are objects availiable and room in the shared memory.
                // If blockUntilDone is set continue to loop until all of the objects in both queues are sent.
                while ((objectsToWrite.Count > 0 || objectsToWriteHiPriority.Count > 0) &&
                       ((blockUntilDone && notFullFlag.WaitOne()) || !IsFull))
                {
                    bool isFull = false;
                    long writeStartPosition = writeStream.Position;
                    bool writeEndMarker = false;

                    // Put as many LocalCallDescriptor objects as possible into the shared memory
                    while (!isFull && (objectsToWrite.Count > 0 || objectsToWriteHiPriority.Count > 0))
                    {
                        long writeResetPosition = writeStream.Position;

                        DualQueue<LocalCallDescriptor> currentQueue = objectsToWriteHiPriority.Count > 0 ? objectsToWriteHiPriority : objectsToWrite;

                        // writeBytesRemaining == 0 is when we are currently not sending a multi part object through
                        // the shared memory
                        if (writeBytesRemaining == 0)
                        {
                            // Serialize the object to the memory stream
                            SerializeCallDescriptorToStream(currentQueue);

                            // If the size of the serialized object plus the end marker fits within the shared memory
                            // dequeue the object as it is going to be sent.
                            if ((writeStream.Position + sizeof(byte)) <= size)
                            {
                                currentQueue.Dequeue();
                                writeEndMarker = true;
                            }
                            else
                            {
                                // The serialized object plus the end marker is larger than the shared memory buffer
                                // Check if it necessary break down the object into multiple buffers
                                // If the memoryStream was empty before trying to serialize the object
                                // create a frame marker with the size of the object and send through the shared memory
                                if (writeResetPosition == 0)
                                {
                                    // We don't want to switch from low priority to high priority queue in the middle of sending a large object
                                    // so we make a record of which queue contains the large object
                                    largeObjectsQueue = currentQueue;
                                    // Calculate the total number of bytes that needs to be sent
                                    writeBytesRemaining = (int)(writeStream.Position + sizeof(byte));
                                    // Send a frame marker out to the reader containing the size of the object
                                    writeStream.Position = 0;

                                    // Write the frameMarkerId byte and then the amount of bytes for the large object
                                    writeStream.WriteByte((byte)ObjectType.FrameMarker);
                                    binaryWriter.Write((Int32)writeBytesRemaining);
                                    writeEndMarker = true;
                                }
                                else
                                {
                                    // Some items were placed in the shared Memory buffer, erase the last one which was too large
                                    // and say the buffer is full so it can be sent
                                    writeStream.Position = writeResetPosition;
                                }
                                isFull = true;
                            }
                        }
                        else
                        {
                            if (writeStream.Position == 0)
                            {
                                // Serialize the object which will be split across multiple buffers
                                SerializeCallDescriptorToStream(largeObjectsQueue);
                                writeStream.WriteByte((byte)ObjectType.EndMarker);
                            }
                            break;
                        }
                    }

                    // If a multi-buffer object is being sent and the large object is still larger or equal to the shard memory buffer - send the next chunk of the object
                    if (writeBytesRemaining != 0 && writeStream.Position >= size)
                    {
                        // Set write Length to an entire buffer length  or just the remaining portion
                        int writeLength = writeBytesRemaining > size ? size : writeBytesRemaining;

                        //Write the length of the buffer to the memory file
                        Marshal.WriteInt32((IntPtr)pageFileView, (int)writeLength);
                        Marshal.Copy
                        (
                            writeStream.GetBuffer(), // Source Buffer
                            (int)(writeStream.Position - writeBytesRemaining), // Start index
                            (IntPtr)((int)pageFileView + 4), //Destination (+4 because of the int written to the memory file with the write length)
                            (int)writeLength // Length of bytes to write
                        );

                        writeBytesRemaining -= writeLength;
                        IncrementUnreadBatchCounter();

                        // Once the object is fully sent - remove it from the queue
                        if (writeBytesRemaining == 0)
                        {
                            largeObjectsQueue.Dequeue();
                        }

                        isFull = true;
                    }

                    if (writeEndMarker)
                    {
                        writeStream.WriteByte((byte)ObjectType.EndMarker);
                        // Need to verify the WriteInt32 and ReadInt32 are always atomic operations
                        //writeSizeMutex.WaitOne();
                        // Write the size of the buffer to send to the memory stream
                        Marshal.WriteInt32((IntPtr)pageFileView, (int)writeStream.Position);
                        //writeSizeMutex.ReleaseMutex();

                        Marshal.Copy
                        (
                            writeStream.GetBuffer(), // Buffer
                            (int)writeStartPosition, // Start Position
                            (IntPtr)((int)pageFileView + writeStartPosition + 4), // Destination + 4 for the int indicating the size of the data to be copied to the memory file
                            (int)(writeStream.Position - writeStartPosition) // Length of data to copy to memory file
                        );

                        IncrementUnreadBatchCounter();
                    }

                    if (isFull)
                    {
                        MarkAsFull();
                        writeStream.SetLength(0);
                    }
                }
            }
        }

        /// <summary>
        /// Serialize the first object in the queue to the a memory stream which will be copied into shared memory.
        /// The write stream which is being written to is not the shared memory itself, it is a memory stream from which
        /// bytes will be copied and placed in the shared memory in the write method.
        /// </summary>
        private void SerializeCallDescriptorToStream(DualQueue<LocalCallDescriptor> objectsToWrite)
        {
            // Get the object by peeking at the queue rather than dequeueing the object. This is done
            // because we only want to dequeue the object when it has completely been put in shared memory.
            // This may be done right away if the object is small enough to fit in the shared memory or 
            // may happen after a the object is sent as a number of smaller chunks.
            object objectToWrite = objectsToWrite.Peek();
            Debug.Assert(objectToWrite != null, "Expect to get a non-null object from the queue");
            if (objectToWrite is LocalCallDescriptorForPostBuildResult)
            {
                writeStream.WriteByte((byte)ObjectType.PostBuildResult);
                ((LocalCallDescriptorForPostBuildResult)objectToWrite).WriteToStream(binaryWriter);
            }
            else if (objectToWrite is LocalCallDescriptorForPostBuildRequests)
            {
                writeStream.WriteByte((byte)ObjectType.PostBuildRequests);
                ((LocalCallDescriptorForPostBuildRequests)objectToWrite).WriteToStream(binaryWriter);
            }
            else if (objectToWrite is LocalCallDescriptorForPostLoggingMessagesToHost)
            {
                writeStream.WriteByte((byte)ObjectType.PostLoggingMessagesToHost);
                ((LocalCallDescriptorForPostLoggingMessagesToHost)objectToWrite).WriteToStream(binaryWriter, loggingTypeCache);
            }
            else if (objectToWrite is LocalCallDescriptorForInitializeNode)
            {
                writeStream.WriteByte((byte)ObjectType.InitializeNode);
                ((LocalCallDescriptorForInitializeNode)objectToWrite).WriteToStream(binaryWriter);
            }
            else if (objectToWrite is LocalCallDescriptorForInitializationComplete)
            {
                writeStream.WriteByte((byte)ObjectType.InitializationComplete);
                ((LocalCallDescriptorForInitializationComplete)objectToWrite).WriteToStream(binaryWriter);
            }
            else if (objectToWrite is LocalCallDescriptorForUpdateNodeSettings)
            {
                writeStream.WriteByte((byte)ObjectType.UpdateNodeSettings);
                ((LocalCallDescriptorForUpdateNodeSettings)objectToWrite).WriteToStream(binaryWriter);
            }
            else if (objectToWrite is LocalCallDescriptorForRequestStatus)
            {
                writeStream.WriteByte((byte)ObjectType.RequestStatus);
                ((LocalCallDescriptorForRequestStatus)objectToWrite).WriteToStream(binaryWriter);
            }
            else if (objectToWrite is LocalCallDescriptorForPostingCacheEntriesToHost)
            {
                writeStream.WriteByte((byte)ObjectType.PostCacheEntriesToHost);
                ((LocalCallDescriptorForPostingCacheEntriesToHost)objectToWrite).WriteToStream(binaryWriter);
            }
            else if (objectToWrite is LocalCallDescriptorForGettingCacheEntriesFromHost)
            {
                writeStream.WriteByte((byte)ObjectType.GetCacheEntriesFromHost);
                ((LocalCallDescriptorForGettingCacheEntriesFromHost)objectToWrite).WriteToStream(binaryWriter);
            }
            else if (objectToWrite is LocalCallDescriptorForShutdownComplete)
            {
                writeStream.WriteByte((byte)ObjectType.ShutdownComplete);
                ((LocalCallDescriptorForShutdownComplete)objectToWrite).WriteToStream(binaryWriter);
            }
            else if (objectToWrite is LocalCallDescriptorForShutdownNode)
            {
                writeStream.WriteByte((byte)ObjectType.ShutdownNode);
                ((LocalCallDescriptorForShutdownNode)objectToWrite).WriteToStream(binaryWriter);
            }
            else if (objectToWrite is LocalCallDescriptorForPostIntrospectorCommand)
            {
                writeStream.WriteByte((byte)ObjectType.PostIntrospectorCommand);
                ((LocalCallDescriptorForPostIntrospectorCommand)objectToWrite).WriteToStream(binaryWriter);
            }
            else if (objectToWrite is LocalReplyCallDescriptor)
            {
                writeStream.WriteByte((byte)ObjectType.GenericSingleObjectReply);
                ((LocalReplyCallDescriptor)objectToWrite).WriteToStream(binaryWriter);
            }
            else if (objectToWrite is LocalCallDescriptorForPostStatus)
            {
                writeStream.WriteByte((byte)ObjectType.PostStatus);
                ((LocalCallDescriptorForPostStatus)objectToWrite).WriteToStream(binaryWriter);
            }
            else
            {
                // If the object is not one of the well known local descriptors, use .net Serialization to serialize the object
                writeStream.WriteByte((byte)ObjectType.NetSerialization);
                binaryFormatter.Serialize(writeStream, objectToWrite);
            }
        }

        /// <summary>
        /// This function reads data from the shared memory buffer and returns a list
        /// of deserialized LocalCallDescriptors or null. The method will return null
        /// if the object being sent accross is a multi buffer object. Read needs to
        /// be called multiple times until the entire large object has been recived.
        /// Once this has happened the large object is deserialized and returned in
        /// the Ilist. Read is used by the shared memory reader threads in the LocalNode
        /// (child end) and the LocalNodeProvider(ParentEnd) to read LocalCallDescriptors
        /// from the shared memory. Read is called from loops in the SharedMemoryReaderThread
        /// </summary>
        internal IList Read()
        {
            ErrorUtilities.VerifyThrow(type == SharedMemoryType.ReadOnly, "Should only be calling Read from a readonly shared memory object");
            ArrayList objectsRead = null;
            lock (readLock)
            {
                if (NumberOfUnreadBatches > 0)
                {
                    // The read stream is a memory stream where data read from the shared memory section 
                    // will be copied to. From  this memory stream LocalCallDescriptors are deserialized. 
                    // Stream position may not be 0 if we are reading a multipart object
                    int readStartPosition = (int)readStream.Position;

                    // Read the first int from the memory file. This indicates the number of bytes written to 
                    // shared memory by the write method.
                    int endWritePosition = Marshal.ReadInt32((IntPtr)((int)pageFileView));

                    // Copy the bytes written into the shared memory section into the readStream memory stream.
                    Marshal.Copy
                    (
                        (IntPtr)((int)pageFileView + 4 + readStream.Position), // Source 
                        readBuffer, //Destination
                        (int)(readStream.Position + (readBytesTotal - readBytesRemaining)), // Start Index
                        (int)(endWritePosition - readStream.Position) //Length of data
                    );

                    // If a multi buffer object is being read - decrement the bytes remaining
                    if (readBytesRemaining != 0)
                    {
                        readBytesRemaining -= endWritePosition;
                    }

                    // If a multi buffer object is not being read (or just completed) - try
                    // deserializing the data from the buffer into a set of objects
                    if (readBytesRemaining == 0)
                    {
                        objectsRead = new ArrayList();

                        int objectId;
                        // Deserialize the object in the read stream to a LocalCallDescriptor. The objectId
                        // is the "ObjectType" which was written to the head of the object when written to the memory stream.
                        // It describes which kind of object was serialized
                        object objectRead = DeserializeFromStream(out objectId);

                        // Check if the writer wants to sent a multi-buffer object, by checking
                        // if the top object is a frame marker.
                        if (readStartPosition == 0)
                        {
                            if ((int)ObjectType.FrameMarker == objectId)
                            {
                                int frameSizeInPages = (int)((((int)objectRead) + NativeMethods.PAGE_SIZE)
                                                        / NativeMethods.PAGE_SIZE);

                                // Read the end marker off the stream
                                objectId = binaryReader.ReadByte();

                                // Allocate a bigger readStream buffer to contain the large object which will be sent if necessary
                                if (readBuffer.Length < frameSizeInPages * NativeMethods.PAGE_SIZE)
                                {
                                    // Close the binary reader and the underlying stream before recreating a larger buffer
                                    binaryReader.Close();

                                    this.readBuffer = new byte[frameSizeInPages * NativeMethods.PAGE_SIZE];
                                    this.readStream = new MemoryStream(this.readBuffer);
                                    this.readStream.Position = 0;

                                    // ReCreate the reader on the new stream
                                    binaryReader = new BinaryReader(readStream);
                                }

                                readBytesRemaining = (int)objectRead;
                                readBytesTotal = (int)objectRead;
                            }
                            else
                            {
                                readBytesTotal = 0;
                            }
                        }

                        // Deserialized the objects in the read stream and add them into the arrayList as long as 
                        // we did not encounter a frameMarker which says a large object is next or the end marker 
                        // which marks the end of the batch.
                        while (((int)ObjectType.EndMarker != objectId) && ((int)ObjectType.FrameMarker != objectId))
                        {
                            objectsRead.Add(objectRead);
                            objectRead = DeserializeFromStream(out objectId);
                        }
                    }

                    DecrementUnreadBatchCounter();
                }
                else
                {
                    MarkAsNotFull();
                    readStream.Position = 0;
                }
            }

            return objectsRead;
        }

        /// <summary>
        /// This method first reads the objectId as an int from the stream,
        /// this int should be found in the "ObjectType" enumeration. This
        /// objectId informs the method what kind of object should be
        /// deserialized and returned from the method. The objectId is an
        /// output parameter. This parameter is also returned so it can be
        /// used in the read and write methods to determine if
        /// a frame or end marker was found.
        /// </summary>
        private object DeserializeFromStream(out int objectId)
        {
            object objectRead = null;
            objectId = readStream.ReadByte();
            switch ((ObjectType)objectId)
            {
                case ObjectType.NetSerialization:
                    objectRead = binaryFormatter.Deserialize(readStream);
                    break;
                case ObjectType.FrameMarker:
                    objectRead = binaryReader.ReadInt32();
                    break;
                case ObjectType.PostBuildResult:
                    objectRead = new LocalCallDescriptorForPostBuildResult();
                    ((LocalCallDescriptorForPostBuildResult)objectRead).CreateFromStream(binaryReader);
                    break;
                case ObjectType.PostBuildRequests:
                    objectRead = new LocalCallDescriptorForPostBuildRequests();
                    ((LocalCallDescriptorForPostBuildRequests)objectRead).CreateFromStream(binaryReader);
                    break;
                case ObjectType.PostLoggingMessagesToHost:
                    objectRead = new LocalCallDescriptorForPostLoggingMessagesToHost();
                    ((LocalCallDescriptorForPostLoggingMessagesToHost)objectRead).CreateFromStream(binaryReader, loggingTypeCache);
                    break;
                case ObjectType.InitializeNode:
                    objectRead = new LocalCallDescriptorForInitializeNode();
                    ((LocalCallDescriptorForInitializeNode)objectRead).CreateFromStream(binaryReader);
                    break;
                case ObjectType.InitializationComplete:
                    objectRead = new LocalCallDescriptorForInitializationComplete();
                    ((LocalCallDescriptorForInitializationComplete)objectRead).CreateFromStream(binaryReader);
                    break;
                case ObjectType.UpdateNodeSettings:
                    objectRead = new LocalCallDescriptorForUpdateNodeSettings();
                    ((LocalCallDescriptorForUpdateNodeSettings)objectRead).CreateFromStream(binaryReader);
                    break;
                case ObjectType.RequestStatus:
                    objectRead = new LocalCallDescriptorForRequestStatus();
                    ((LocalCallDescriptorForRequestStatus)objectRead).CreateFromStream(binaryReader);
                    break;
                case ObjectType.PostCacheEntriesToHost:
                    objectRead = new LocalCallDescriptorForPostingCacheEntriesToHost();
                    ((LocalCallDescriptorForPostingCacheEntriesToHost)objectRead).CreateFromStream(binaryReader);
                    break;
                case ObjectType.GetCacheEntriesFromHost:
                    objectRead = new LocalCallDescriptorForGettingCacheEntriesFromHost();
                    ((LocalCallDescriptorForGettingCacheEntriesFromHost)objectRead).CreateFromStream(binaryReader);
                    break;
                case ObjectType.ShutdownComplete:
                    objectRead = new LocalCallDescriptorForShutdownComplete();
                    ((LocalCallDescriptorForShutdownComplete)objectRead).CreateFromStream(binaryReader);
                    break;
                case ObjectType.ShutdownNode:
                    objectRead = new LocalCallDescriptorForShutdownNode();
                    ((LocalCallDescriptorForShutdownNode)objectRead).CreateFromStream(binaryReader);
                    break;
                case ObjectType.PostIntrospectorCommand:
                    objectRead = new LocalCallDescriptorForPostIntrospectorCommand(null, null);
                    ((LocalCallDescriptorForPostIntrospectorCommand)objectRead).CreateFromStream(binaryReader);
                    break;
                case ObjectType.GenericSingleObjectReply:
                    objectRead = new LocalReplyCallDescriptor();
                    ((LocalReplyCallDescriptor)objectRead).CreateFromStream(binaryReader);
                    break;
                case ObjectType.PostStatus:
                    objectRead = new LocalCallDescriptorForPostStatus();
                    ((LocalCallDescriptorForPostStatus)objectRead).CreateFromStream(binaryReader);
                    break;
                case ObjectType.EndMarker:
                    return null;
                default:
                    ErrorUtilities.VerifyThrow(false, "Should not be here, ObjectId:" + objectId + "Next:" + readStream.ReadByte());
                    break;
            }
            return objectRead;
        }

        /// <summary>
        /// Reset the state of the shared memory, this is called when the node is
        /// initialized for the first time or when the node is activated due to node reuse.
        /// </summary>
        internal void Reset()
        {
            if (readStream != null)
            {
                readStream.Position = 0;
            }
            if (writeStream != null)
            {
                writeStream.SetLength(0);
                Marshal.WriteInt32((IntPtr)pageFileView, (int)writeStream.Position);
            }
            largeObjectsQueue = null;
        }

        #endregion

        #region Member data

        private const int size = 100 * 1024;
        private string name;
        private SafeFileHandle pageFileMapping;
        private IntPtr pageFileView;

        private BinaryFormatter binaryFormatter;

        // Binary reader and writer used to read and write from the memory streams used to contain the deserialized LocalCallDescriptors before and after they are copied 
        // to and from the shared memory region.
        private BinaryWriter binaryWriter;
        private BinaryReader binaryReader;

        /// <summary>
        /// Memory stream to contain the deserialized objects before they are sent accross the shared memory region
        /// </summary>
        private MemoryStream writeStream;

        // Backing byte array of the readStream
        private byte[] readBuffer;
        private MemoryStream readStream;

        // The count on a semaphore is decremented each time a thread enters the semaphore,
        // and incremented when a thread releases the semaphore. 
        // When the count is zero, subsequent requests block until other threads release the semaphore. 
        // A semaphore is considered siginaled when the count > 1 and not siginaled when the count is 0.

        // unreadBatchCounter is used to track how many batches are remaining to be read from shared memory.
        private Semaphore unreadBatchCounter;

        //Used to inform the shared memory reader threads the writer thread has written something in shared memory to read.
	//The semaphore is incremented when the shared memory is full and when there is an unreadBatch availiable to be read or the shared memory is full.
	//The semaphore is decremented when the shared memory reader thread is about to read from the shared memory.
        private Semaphore readActionCounter;

        // Whether or not the shared memory is full
        private EventWaitHandle fullFlag;
        private EventWaitHandle notFullFlag;

        private object writeLock;
        private object readLock;

        // How many bytes remain to be written for the large object to be fully written to shared memory
        private int writeBytesRemaining;
        // How many bytes remain to be read for the large object to be fully read to shared memory
        private int readBytesRemaining;
        // How many bytes is the large object in size
        private int readBytesTotal;

        // Have we disposed this object yet;
        private bool disposed;

        // Is the memory read only or write only
        private SharedMemoryType type;

        // Because we are using reflection to get the writeToStream and readFromStream methods from the classes in the framework assembly we found
        // we were spending a lot of time reflecting for these methods. The loggingTypeCache, caches the methodInfo for the classes and then look them
        // up when serializing or deserializing the objects. 
        private Hashtable loggingTypeCache;

        // Keep a pointer to the queue which contains the large object which is being deserialized. We do this because we want to make sure 
        // after the object is properly sent we dequeue off the correct queue.
        private DualQueue<LocalCallDescriptor> largeObjectsQueue;
        #endregion
    }
}
