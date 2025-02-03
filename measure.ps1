$results = @()

for ($i = 1; $i -le 10; $i++) {
    $time = Measure-Command {
        # Place the script or command you want to measure here
        # For example:
        dotnet build msbuild.binlog
    }
    $results += [pscustomobject]@{
        Iteration = $i
        Duration  = $time.TotalMilliseconds
    }
}

# Display or export the results
$results | Format-Table -AutoSize
$results | Export-Csv -Path "timings.csv" -NoTypeInformation


# second
$results = @()

for ($i = 1; $i -le 100; $i++) {
    $time = Measure-Command {
        # Place the script or command you want to measure here
        # For example:
        .\artifacts\bin\bootstrap\core\dotnet.exe build msbuild.binlog
    }
    $results += [pscustomobject]@{
        Iteration = $i
        Duration  = $time.TotalMilliseconds
    }
}

# Display or export the results
$results | Format-Table -AutoSize
$results | Export-Csv -Path "timings2.csv" -NoTypeInformation

