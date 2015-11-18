# Put the stage2 output on the front of the path
$stage2 = "$PSScriptRoot\..\artifacts\win7-x64\stage2\bin"
if (Test-Path $stage2) {
    $splat = $env:PATH.Split(";")
    $stage2 = Convert-Path $stage2
    if ($splat -notcontains $stage2) {
        $env:PATH="$stage2;$env:PATH"
    }
} else {
    Write-Host "You don't have a dev build in the 'artifacts\win7-x64\stage2' folder!"
}
