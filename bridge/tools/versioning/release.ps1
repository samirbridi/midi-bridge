param(
    [switch] $Commit,
    [switch] $Tag,
    [switch] $Push
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$bridgeRoot = Split-Path -Parent (Split-Path -Parent $scriptRoot)

function Get-LastVersionTag {
    $t = ""
    try {
        $t = git describe --tags --match "v[0-9]*.[0-9]*.[0-9]*" --abbrev=0 2>$null
    } catch {
        $t = ""
    }
    if ([string]::IsNullOrWhiteSpace($t)) { return $null }
    return $t.Trim()
}

function Read-VersionJson([string] $path) {
    $o = Get-Content -Raw -Path $path | ConvertFrom-Json
    return [pscustomobject]@{
        major = [int]$o.major
        minor = [int]$o.minor
        patch = [int]$o.patch
        build = [int]$o.build
    }
}

function Write-VersionJson([string] $path, [int] $major, [int] $minor, [int] $patch, [int] $build) {
    $obj = [ordered]@{ major = $major; minor = $minor; patch = $patch; build = $build }
    $json = ($obj | ConvertTo-Json -Depth 5)
    Set-Content -Path $path -Value $json -Encoding UTF8
}

function Write-DirectoryBuildProps([string] $path, [int] $major, [int] $minor, [int] $patch, [int] $build) {
    $vp = "$major.$minor.$patch"
    $av = "$major.$minor.$patch.0"
    $fv = "$major.$minor.$patch.$build"
    $iv = "v$vp (build $build)"
    $xml = @"
<Project>
  <PropertyGroup>
    <VersionPrefix>$vp</VersionPrefix>
    <AssemblyVersion>$av</AssemblyVersion>
    <FileVersion>$fv</FileVersion>
    <InformationalVersion>$iv</InformationalVersion>
  </PropertyGroup>
</Project>
"@
    Set-Content -Path $path -Value $xml -Encoding UTF8
}

function Normalize-Subject([string] $subject, [string] $type) {
    $prefix = "${type}: "
    if ($subject.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $subject.Substring($prefix.Length).Trim()
    }
    return $subject.Trim()
}

function Parse-Commits([string] $range) {
    $format = "%H%x1f%s%x1f%b%x1e"
    $raw = git log $range --no-decorate --pretty=format:$format
    $records = $raw -split [char]0x1e
    $commits = @()
    foreach ($r in $records) {
        if ([string]::IsNullOrWhiteSpace($r)) { continue }
        $parts = $r -split [char]0x1f
        if ($parts.Length -lt 2) { continue }
        $sha = $parts[0].Trim()
        $subject = $parts[1].Trim()
        $body = ""
        if ($parts.Length -ge 3) { $body = $parts[2] }
        $commits += [pscustomobject]@{ sha = $sha; subject = $subject; body = $body }
    }
    return $commits
}

function Classify-Commit($c) {
    $allowed = @("feat","fix","improve","perf","refactor","break")
    $breaking = $false
    if ($c.body -match "(?m)^\s*BREAKING CHANGE\b" -or $c.subject -match "(?i)\bbreaking change\b") {
        $breaking = $true
    }

    if ($c.subject -match "^(?<t>[a-zA-Z]+):\s+.+$") {
        $t = $Matches["t"].ToLowerInvariant()
        if ($allowed -notcontains $t) {
            throw "Commit fora do padrão: $($c.subject) ($($c.sha))"
        }
        return [pscustomobject]@{ type = $t; breaking = ($breaking -or $t -eq "break") }
    }

    throw "Commit fora do padrão: $($c.subject) ($($c.sha))"
}

Push-Location $bridgeRoot
try {
    $versionPath = Join-Path $bridgeRoot "version.json"
    $propsPath = Join-Path $bridgeRoot "Directory.Build.props"
    $changelogPath = Join-Path $bridgeRoot "CHANGELOG.md"

    $base = Read-VersionJson $versionPath
    $lastTag = Get-LastVersionTag
    if ($null -eq $lastTag) {
        $vp0 = "$($base.major).$($base.minor).$($base.patch)"
        $tagName0 = "v$vp0"
        Write-DirectoryBuildProps $propsPath $base.major $base.minor $base.patch $base.build
        if (-not (Test-Path $changelogPath)) {
            $date0 = (Get-Date).ToString("yyyy-MM-dd")
            $initial = @(
                "# Changelog",
                "",
                "## v$vp0 (build $($base.build)) - $date0",
                "",
                "### 💥 Breaking Changes",
                "",
                "- N/A",
                "",
                "### ✨ Features",
                "",
                "- N/A",
                "",
                "### 🐛 Fixes",
                "",
                "- N/A",
                "",
                "### ⚡ Improvements",
                "",
                "- N/A",
                ""
            ) -join "`n"
            Set-Content -Path $changelogPath -Value $initial -Encoding UTF8
        }

        if ($env:GITHUB_OUTPUT) {
            "tag=$tagName0" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding UTF8 -Append
            "version=$vp0" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding UTF8 -Append
            "build=$($base.build)" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding UTF8 -Append
            "name=v$vp0 (build $($base.build))" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding UTF8 -Append
        }

        if ($Commit) {
            git add $versionPath $propsPath $changelogPath
            git diff --cached --quiet
            if ($LASTEXITCODE -ne 0) {
                git commit -m "improve: release v$vp0 (build $($base.build))"
            }
        }

        if ($Tag) {
            git show-ref --tags --verify --quiet "refs/tags/$tagName0"
            if ($LASTEXITCODE -ne 0) {
                git tag $tagName0
            }
        }

        if ($Push) {
            git push origin HEAD:main --follow-tags
        }

        return
    }

    $range = "$lastTag..HEAD"

    $commits = Parse-Commits $range
    if ($commits.Count -eq 0) {
        throw "Nenhum commit encontrado para release (range: $range)"
    }

    $classified = @()
    foreach ($c in $commits) {
        $cls = Classify-Commit $c
        $classified += [pscustomobject]@{ sha = $c.sha; subject = $c.subject; body = $c.body; type = $cls.type; breaking = $cls.breaking }
    }

    $hasBreaking = ($classified | Where-Object { $_.breaking }).Count -gt 0
    $hasFeat = ($classified | Where-Object { $_.type -eq "feat" }).Count -gt 0

    $major = $base.major
    $minor = $base.minor
    $patch = $base.patch
    $build = $base.build + 1

    if ($hasBreaking) {
        $major += 1
        $minor = 0
        $patch = 0
    } elseif ($hasFeat) {
        $minor += 1
        $patch = 0
    } else {
        $patch += 1
    }

    Write-VersionJson $versionPath $major $minor $patch $build
    Write-DirectoryBuildProps $propsPath $major $minor $patch $build

    $date = (Get-Date).ToString("yyyy-MM-dd")
    $vp = "$major.$minor.$patch"
    $header = "## v$vp (build $build) - $date"

    $repo = $env:GITHUB_REPOSITORY
    $mkItem = {
        param($c)
        $short = $c.sha.Substring(0,7)
        $desc = Normalize-Subject $c.subject $c.type
        if ([string]::IsNullOrWhiteSpace($repo)) {
            return "- $desc ($short)"
        }
        return "- $desc ($short) https://github.com/$repo/commit/$($c.sha)"
    }

    $breakingItems = ($classified | Where-Object { $_.breaking } | ForEach-Object { & $mkItem $_ })
    $featureItems = ($classified | Where-Object { $_.type -eq "feat" -and -not $_.breaking } | ForEach-Object { & $mkItem $_ })
    $fixItems = ($classified | Where-Object { $_.type -eq "fix" -and -not $_.breaking } | ForEach-Object { & $mkItem $_ })
    $improveItems = ($classified | Where-Object { @("improve","perf","refactor") -contains $_.type -and -not $_.breaking } | ForEach-Object { & $mkItem $_ })

    if ($breakingItems.Count -eq 0) { $breakingItems = @("- N/A") }
    if ($featureItems.Count -eq 0) { $featureItems = @("- N/A") }
    if ($fixItems.Count -eq 0) { $fixItems = @("- N/A") }
    if ($improveItems.Count -eq 0) { $improveItems = @("- N/A") }

    $newSection = @()
    $newSection += $header
    $newSection += ""
    $newSection += "### 💥 Breaking Changes"
    $newSection += ""
    $newSection += $breakingItems
    $newSection += ""
    $newSection += "### ✨ Features"
    $newSection += ""
    $newSection += $featureItems
    $newSection += ""
    $newSection += "### 🐛 Fixes"
    $newSection += ""
    $newSection += $fixItems
    $newSection += ""
    $newSection += "### ⚡ Improvements"
    $newSection += ""
    $newSection += $improveItems
    $newSection += ""

    $existing = ""
    if (Test-Path $changelogPath) {
        $existing = Get-Content -Raw -Path $changelogPath
    } else {
        $existing = "# Changelog`n`n"
    }

    if (-not $existing.StartsWith("# Changelog")) {
        $existing = "# Changelog`n`n" + $existing
    }

    $parts = $existing -split "(?m)^# Changelog\s*$", 2
    $tail = ""
    if ($parts.Count -eq 2) { $tail = $parts[1].TrimStart("`r","`n") } else { $tail = "" }
    $out = "# Changelog`n`n" + ($newSection -join "`n") + $tail
    Set-Content -Path $changelogPath -Value $out -Encoding UTF8

    $tagName = "v$vp"
    if ($env:GITHUB_OUTPUT) {
        "tag=$tagName" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding UTF8 -Append
        "version=$vp" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding UTF8 -Append
        "build=$build" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding UTF8 -Append
        "name=v$vp (build $build)" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding UTF8 -Append
    }

    if ($Commit) {
        git add $versionPath $propsPath $changelogPath
        git diff --cached --quiet
        if ($LASTEXITCODE -ne 0) {
            git commit -m "improve: release v$vp (build $build)"
        }
    }

    if ($Tag) {
        git show-ref --tags --verify --quiet "refs/tags/$tagName"
        if ($LASTEXITCODE -ne 0) {
            git tag $tagName
        }
    }

    if ($Push) {
        git push origin HEAD:main --follow-tags
    }
} finally {
    Pop-Location
}
