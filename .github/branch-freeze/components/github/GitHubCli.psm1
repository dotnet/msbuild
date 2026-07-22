function Invoke-GitHubCli {
    [OutputType([string], [int])]
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [switch]$DiscardOutput,
        [switch]$IgnoreExitCode,
        [switch]$PassThruExitCode,
        [switch]$NoRetry
    )

    $maximumAttempts = if ($IgnoreExitCode -or $NoRetry) { 1 } else { 3 }
    for ($attempt = 1; $attempt -le $maximumAttempts; $attempt++) {
        $output = & gh @Arguments
        $exitCode = $LASTEXITCODE
        if ($exitCode -eq 0 -or $IgnoreExitCode) {
            break
        }

        if ($attempt -eq $maximumAttempts) {
            throw "gh command failed with exit code $exitCode after $maximumAttempts attempt(s)."
        }

        $delaySeconds = [int][Math]::Pow(2, $attempt - 1)
        Write-Warning "gh command failed with exit code $exitCode. Retrying in $delaySeconds second(s)."
        Start-Sleep -Seconds $delaySeconds
    }

    if ($PassThruExitCode) {
        return $exitCode
    }

    if (-not $DiscardOutput) {
        return $output -join [Environment]::NewLine
    }
}

Export-ModuleMember -Function 'Invoke-GitHubCli'