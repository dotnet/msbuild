' Copyright (c) .NET Foundation and contributors. All rights reserved.
' Licensed under the MIT license. See LICENSE file in the project root for full license information.

Namespace TestLibrary

    Public Class Helper

        Public Shared Function GetMessage() As String
            Return "This string came from the test library!"
        End Function

        Public Sub SayHi()
            Console.WriteLine("Hello there!")
        End Sub

    End Class

End Namespace
