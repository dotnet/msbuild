Module Module1
'#If defaultTrue
    Sub DefaultTrueIncluded()
    End Sub
'#Else
    Sub DefaultTrueExcluded()
    End Sub
'#End If

'-:cnd:noEmit
'#If defaultTrue
    Sub InsideUnknownDirectiveNoEmit()
    End Sub
'#End If
'+:cnd:noEmit

'#If (defaultFalse)
    Sub DefaultFalseExcluded()
    End Sub
'#Else
    Sub DefaultFalseIncluded()
    End Sub
'#End If

' Without noEmit the following line will be emitted
'-:cnd
'#If defaultFalse
    Sub InsideUnknownDirectiveEmit()
    End Sub
'#End If
'+:cnd
End Module