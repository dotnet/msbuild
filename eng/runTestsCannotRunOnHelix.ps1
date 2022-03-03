Write-Host "Sleeping for 90 seconds..."

#hack to make sure build of helix run is done, so no file lock
Start-Sleep -Seconds 90

Write-Host "Running tests that can't run on Helix..."

$repoRoot = (get-item $PSScriptRoot).parent
$allTests = Get-Childitem -Path $repoRoot.GetDirectories("src").GetDirectories("Tests").FullName -Recurse '*.Tests.csproj'

$testsProjectCannotRunOnHelixListPath = $repoRoot.GetDirectories("src").GetDirectories("Tests").GetFiles("testsProjectCannotRunOnHelixList.txt").FullName
$buildshScriptPath = $repoRoot.GetDirectories("eng").GetDirectories("common").GetFiles("build.ps1").FullName

$args = $args
$passin = @("-test")
$passin = $passin  +  $args

foreach($line in ([System.IO.File]::ReadLines($testsProjectCannotRunOnHelixListPath) | Where-Object { $_.Trim() -ne '' }))
{
    foreach ($testProject in $allTests)
    {
        if ($testProject.FullName.Contains($line.Trim())) {
            $baseName = $testProject.BaseName

            $passInArgs = @("-test") + $args + @("-projects", $testProject.FullName, "/bl:$buildSourcesDirectory\artifacts\log\$configuration\$baseName.binlog")
            $anyError = $false
            try {
                Invoke-Expression "& $buildshScriptPath $passInArgs"
            }
            catch {
                $anyError = $true
            }

            if ($anyError)
            {
                return -1
            }
        }
    }
}

Write-Host "Done running tests that can't run on Helix..."