Option Strict On
Option Infer On
Imports System.Windows

Class MainWindow
    Sub Foo()
        Dim dataObject As DataObject
        For Each format In dataObject.GetFormats()

        Next
    End Sub
End Class