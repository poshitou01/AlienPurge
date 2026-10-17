param([Parameter(Mandatory=$true)][string]$BaselineCommit)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location -LiteralPath $taskRoot
try {
    $taskSha = (git rev-parse --verify ($BaselineCommit + '^{commit}')).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Baseline must be an existing reviewed commit.' }
    # Read committed blob hashes; never stage, reset, switch or modify game assets.
    $taskEntries = @(git -c core.quotepath=false ls-tree -r $taskSha | ForEach-Object {
        if ($_ -match '^\d+ blob ([0-9a-f]+)\t(Assets/.+\.(?:unity|prefab|asset|mat|anim|controller)(?:\.meta)?)$') {
            $taskPath = $Matches[2]
            if ($taskPath -notlike 'Assets/_Project/Data/Level01/RegionLoop/*' -and $taskPath -notlike '*Level01_FullWorld_GameplayV01.unity*') {
                [ordered]@{path=$taskPath;blob=$Matches[1]}
            }
        }
    })
    [ordered]@{baseline=$taskSha;files=$taskEntries} | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $PSScriptRoot 'RegionLoop_ProtectedBaseline.json') -Encoding utf8
    Write-Output "Protected baseline now records $taskSha ($($taskEntries.Count) assets). Review the JSON diff before committing."
} finally { Pop-Location }
