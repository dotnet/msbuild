$Architecture = 'x64'

 $Path = "dev\msb3\msbuild\artifacts\bin\bootstrap\net472\MSBuild\Current\Bin\Microsoft.Build.Utilities.Core.dll"
 $msUtilities = [System.Reflection.Assembly]::LoadFrom($Path)

 [type]$t = $msUtilities.GetType('Microsoft.Build.Utilities.ToolLocationHelper')
 if ($t -ne $null)
 {
  [System.Reflection.MethodInfo] $mi = $t.GetMethod("GetPathToBuildToolsFile",[type[]]@( [string], [string], $msUtilities.GetType("Microsoft.Build.Utilities.DotNetFrameworkArchitecture") ))

 $param3 = $mi.GetParameters()[2]
  $archValues = [System.Enum]::GetValues($param3. ParameterType)


 [object] $archValue = $null
   if ($Architecture -eq 'x86') {
    $archValue = $archValues.GetValue(1) # DotNetFrameworkArchitecture.Bitness32
   } elseif ($Architecture -eq 'x64') {
    $archValue = $archValues.GetValue(2) # DotNetFrameworkArchitecture.Bitness64
   } else {
    $archValue = $archValues.GetValue(1) # DotNetFrameworkArchitecture.Bitness32
   }
  #Write-Host "archValue = $archValue"

 $msBuildPath = $mi.Invoke($null, @( 'msbuild.exe', '17.0', $archValue ))
  $msBuildPath
 }
