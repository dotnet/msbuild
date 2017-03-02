//--------------------------------------------------------------------------
//
// Copyright (C) 2015 Microsoft Corporation,
// All rights reserved
//
// File:    MSBuildTasks.h
//
// Unmanaged C++ code that needs to use MSBuild should #include this header
// file to get access to the MSBuildTasks interfaces.  See the generated
// file MSBuildTasks.tlh for the interface definitions.
//
//---------------------------------------------------------------------------
#if (_MSC_VER > 1000) && !defined(NO_PRAGMA_ONCE)
#pragma once
#endif

#ifndef __MSBUILDTASKINTERFACES_H__
#define __MSBUILDTASKINTERFACES_H__


#import "Microsoft.Build.Tasks.Core.tlb"                                     \
    no_registry                                                         \
    raw_interfaces_only named_guids                                     \
    rename("Microsoft_Build_Tasks_Core",    "MSBuildTasks")

#endif //__MSBUILDTASKINTERFACES_H__
