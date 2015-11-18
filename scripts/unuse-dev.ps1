# Remove the stage2 output from the path
$splat = $env:PATH.Split(";")
$stripped = @($splat | where { $_ -notlike "*artifacts\win7-x64\stage2*" })
$env:PATH = [string]::Join(";", $stripped)
