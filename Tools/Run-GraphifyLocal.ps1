[CmdletBinding()]
param(
    [string]$Model
)

$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$graphify = Join-Path $env:USERPROFILE '.local\bin\graphify.exe'
if (-not (Test-Path $graphify)) {
    throw "Graphify is not installed at $graphify. Run: uv tool install --force --with openai graphifyy"
}

try {
    $models = Invoke-RestMethod 'http://localhost:1234/v1/models' -TimeoutSec 5
} catch {
    throw 'LM Studio is not serving at http://localhost:1234/v1. Load a chat/instruct model and start its local server.'
}

$modelIds = @($models.data | ForEach-Object id)
if (-not $Model) {
    if ($modelIds.Count -eq 1) {
        $Model = $modelIds[0]
    } else {
        throw "Pass -Model with one of: $($modelIds -join ', ')"
    }
}
if ($Model -notin $modelIds) {
    throw "LM Studio does not expose '$Model'. Available: $($modelIds -join ', ')"
}

$env:OPENAI_API_KEY = 'lm-studio'
$env:OPENAI_BASE_URL = 'http://localhost:1234/v1'
$env:OPENAI_MODEL = $Model

& $graphify extract $repo --backend openai --model $Model --max-concurrency 1 --token-budget 4000 --api-timeout 900
exit $LASTEXITCODE
