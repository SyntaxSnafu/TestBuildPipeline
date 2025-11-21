param(
    [string]$RepoUrl,       # Repo to clone
    [string]$Commit,        # Commit hash to check out
    [string]$UnityVersion,  # Unity version to use
    [string]$Platforms      # "Windows,Android,iOS"
)

$BaseBuildDir = "C:\BuildServer\Repos"
$BaseOutputDir = "C:\BuildServer\Output"

# Extract repo name from URL
$RepoName = ($RepoUrl.Split('/')[-1]).Replace(".git","")

# Working directory for this build
$RepoPath = "$BaseBuildDir\$RepoName"

Write-Host "`n=== CLEAN AND PREPARE WORK DIR ==="
if (Test-Path $RepoPath) {
    Remove-Item $RepoPath -Recurse -Force
}

New-Item -ItemType Directory -Path $RepoPath | Out-Null

Write-Host "Repo folder: $RepoPath"

Write-Host "`n=== CLONING REPO ==="
git clone $RepoUrl $RepoPath

if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Git clone failed"
    exit 1
}

cd $RepoPath
git checkout $Commit

Write-Host "`nUsing commit $Commit"

# Unity path
$UnityExe = "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe"
if (!(Test-Path $UnityExe)) {
    Write-Error "❌ Unity version $UnityVersion not found!"
    exit 1
}

Write-Host "=== Starting Unity Build ==="
$arguments = @(
    "-quit"
    "-batchmode"
    "-nographics"
    "-projectPath `"$RepoPath`""
    "-executeMethod BlocBuildPipeline.BuildFromCLI"
    "-platforms `"$Platforms`""
    "-logFile `"$RepoPath\build.log`""
)

$unityProcess = Start-Process -FilePath $UnityExe -ArgumentList $arguments -PassThru
$unityProcess.WaitForExit()

if ($unityProcess.ExitCode -ne 0) {
    Write-Error "Unity build failed with exit code $($unityProcess.ExitCode)"
    exit 1
}

Write-Host "=== Unity Build Finished ==="

Write-Host "`n=== PREPARING OUTPUT FOLDER ==="
# Get Project Version
function Get-ProjectVersion{
    param([string]$RepoPath)

    $projectSettingsPath = "$RepoPath\ProjectSettings\ProjectSettings.asset"
    if (Test-Path $projectSettingsPath) {
        $content = Get-Content $projectSettingsPath
        foreach ($line in $content) {
            if ($line -match '^\s*bundleVersion\s*:\s*"?([^\s"]+)"?') {
                return $matches[1]
            }
        }
    }
    return "UnknownVersion"
}
$Version = Get-ProjectVersion -RepoPath $RepoPath
Write-Host "Project Version: $Version"

$ProjectOutput = "$BaseOutputDir\$RepoName\$Version"

if (!(Test-Path $ProjectOutput)) {
    New-Item -ItemType Directory -Path $ProjectOutput | Out-Null
}
Write-Host "Output folder: $ProjectOutput"

Write-Host "`n=== COPYING PLATFORM BUILDS ==="

$PlatformList = $Platforms.Split(',')

foreach ($platform in $PlatformList) {
    $platform = $platform.Trim()

    switch ($platform) {
        "Windows"  { $Source = "$RepoPath\Builds\Windows" }
        "Android"  { $Source = "$RepoPath\Builds\Android" }
        "iOS"      { $Source = "$RepoPath\Builds\iOS" }
        "WebGL"    { $Source = "$RepoPath\Builds\WebGL" }
        default    {
            Write-Host "⚠ Unknown platform '$platform', skipping"
            continue
        }
    }

    if (!(Test-Path $Source)) {
        Write-Host "⚠ No output for $platform (folder missing), skipping copy"
        continue
    }

    $PlatformOutput = "$ProjectOutput\$platform"

    if (!(Test-Path $PlatformOutput)) {
        New-Item -ItemType Directory -Path $PlatformOutput | Out-Null
    }

    Write-Host "Copying $platform → $PlatformOutput"

    Copy-Item "$Source\*" $PlatformOutput -Recurse -Force
}

Write-Host "`n=== BUILD PROCESS COMPLETE ==="
