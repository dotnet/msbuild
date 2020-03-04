' Copyright (c) .NET Foundation and contributors. All rights reserved.
' Licensed under the MIT license. See LICENSE file in the project root for full license information.

Imports System

Public Module Program
    Public Sub Main(args As String())
        If args.Length > 0 Then
            Console.WriteLine("echo args:" & String.Join(";", args))
        End If
        
        Dim message As String = Environment.GetEnvironmentVariable("Message")

        If (String.IsNullOrEmpty(message)) Then
            message = "(NO MESSAGE)"
        End If

        Console.WriteLine(message)
    End Sub
End Module
