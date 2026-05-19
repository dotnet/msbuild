// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
global using LockType = System.Threading.Lock;
#else
global using LockType = System.Object;
#endif