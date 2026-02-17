#Requires -Version 5.1
<#
.SYNOPSIS
    Exports first-party ASP.NET project source to docs/llm/dump.txt for LLM review.

.DESCRIPTION
    Walks the project tree and writes every first-party source file into a single
    text file, separated by headers showing path, size, and modification time.
    Third-party libraries, build artefacts, and generated files are excluded.

.NOTES
    Run from the repository root:
        .\Export.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Paths ────────────────────────────────────────────────────────────────────

$repoRoot   = $PSScriptRoot
$projectDir = Join-Path $repoRoot 'MyWebForms'
$outputDir  = Join-Path $repoRoot 'docs\llm'
$outputFile = Join-Path $outputDir 'dump.txt'

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

# ── Exclusion rules ──────────────────────────────────────────────────────────
#
# Excluded DIRECTORIES (entire subtree skipped):
#   bin, obj, packages, .vs, node_modules
#   Scripts/WebForms    — MS Ajax / WebForms built-in scripts
#   Scripts/lib         — third-party JS installed by LibMan (Chart.js, etc.)
#   Content             — Bootstrap CSS, Animate.css, and other third-party CSS
#                         EXCEPT Site.css which is first-party (handled below)
#
# Excluded FILE PATTERNS:
#   *.designer.cs       — auto-generated, not useful for review
#   *.min.js / *.min.css — minified third-party copies
#   jquery*             — jQuery (NuGet)
#   bootstrap*          — Bootstrap JS (NuGet delivers to Scripts/)
#   respond*            — Respond.js
#   modernizr*          — Modernizr
#   WebForms.js etc     — MS Ajax / WebForms helper scripts
#   *.map               — source maps
#   *.suo / *.user      — VS user files
#   *.dll / *.pdb / *.exe / *.config (bin copies) — build artefacts
#   packages.lock.json  — auto-generated
#   dump.txt            — the output file itself
#   *.ico / *.png / *.jpg / *.gif / *.svg — binary assets

$excludedDirs = @(
    'bin', 'obj', 'packages', '.vs', 'node_modules', 'TestResults',
    'Scripts\WebForms',
    'Scripts\lib'
    # NOTE: Content\ is NOT fully excluded — we handle it per-file below
    # so that Site.css (first-party) is still included.
)

$excludedFilePatterns = @(
    '*.designer.cs',
    '*.min.js',
    '*.min.css',
    'jquery*.js',
    'jquery*.map',
    'bootstrap*.js',
    'bootstrap*.css',
    'bootstrap*.map',
    'respond*.js',
    'modernizr*.js',
    'animate*.css',
    '*.map',
    '*.suo',
    '*.user',
    '*.dll',
    '*.pdb',
    '*.exe',
    '*.ico',
    '*.png',
    '*.jpg',
    '*.jpeg',
    '*.gif',
    '*.svg',
    '*.woff',
    '*.woff2',
    '*.ttf',
    '*.eot',
    'packages.lock.json',
    'dump.txt',
    'libman.json'   # LibMan config is useful but very short; include if you want — remove this line to include it
)

# Files explicitly included even if they live under Content\
$alwaysInclude = @(
    'Site.css'
)

# ── Helpers ──────────────────────────────────────────────────────────────────

function Test-ExcludedDir {
    param([string]$fullPath)
    foreach ($seg in $excludedDirs) {
        # Normalise separators for comparison
        $needle = $seg.Replace('/', '\')
        if ($fullPath -like "*\$needle\*" -or $fullPath -like "*\$needle") {
            return $true
        }
    }
    return $false
}

function Test-ExcludedFile {
    param([System.IO.FileInfo]$file)

    # Always include explicitly listed files regardless of directory
    if ($alwaysInclude -contains $file.Name) {
        return $false
    }

    # Skip anything under an excluded directory tree
    if (Test-ExcludedDir $file.FullName) {
        return $true
    }

    # Skip the Content\ folder entirely except for alwaysInclude entries
    $relativeToProject = $file.FullName.Substring($projectDir.Length).TrimStart('\')
    if ($relativeToProject -like 'Content\*') {
        return $true
    }

    # Skip by filename pattern
    foreach ($pattern in $excludedFilePatterns) {
        if ($file.Name -like $pattern) {
            return $true
        }
    }

    return $false
}

function Format-FileSize {
    param([long]$bytes)
    if ($bytes -ge 1MB) { return '{0:F2} MB' -f ($bytes / 1MB) }
    if ($bytes -ge 1KB) { return '{0:F2} KB' -f ($bytes / 1KB) }
    return "$bytes B"
}

# ── Collect files ────────────────────────────────────────────────────────────

Write-Host "Scanning $projectDir ..."

$files = Get-ChildItem -Path $projectDir -Recurse -File |
    Where-Object { -not (Test-ExcludedFile $_) } |
    Sort-Object FullName

Write-Host "  Found $($files.Count) first-party files to export."

# ── Write output ─────────────────────────────────────────────────────────────

$header = @"
===============================================================================
ASP.NET PROJECT EXPORT
Generated: $(Get-Date -Format 'MM/dd/yyyy HH:mm:ss')
Project Path: $projectDir
===============================================================================

DIRECTORY STRUCTURE:
===================

"@

# Build a tree using cmd's tree (works everywhere on Windows)
$treeOutput = & cmd /c "tree /F `"$projectDir`" 2>nul"

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add($header)
$lines.Add(($treeOutput -join "`n"))
$lines.Add("`n`n")

$totalSize = 0L
foreach ($file in $files) {
    $totalSize += $file.Length
    $sep = '=' * 80
    $lines.Add($sep)
    $lines.Add("FILE: $($file.FullName)")
    $lines.Add("SIZE: $(Format-FileSize $file.Length)")
    $lines.Add("MODIFIED: $($file.LastWriteTime.ToString('MM/dd/yyyy HH:mm:ss'))")
    $lines.Add($sep)
    $lines.Add('')
    try {
        $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
        $lines.Add($content)
    }
    catch {
        $lines.Add("[BINARY OR UNREADABLE FILE — SKIPPED]")
    }
    $lines.Add('')
    $lines.Add('')
}

$lines.Add("===============================================================================")
$lines.Add("END OF EXPORT — $($files.Count) files, $(Format-FileSize $totalSize) total")
$lines.Add("===============================================================================")

$lines | Set-Content -Path $outputFile -Encoding UTF8

Write-Host ""
Write-Host "Export complete: $outputFile"
Write-Host "  Files : $($files.Count)"
Write-Host "  Size  : $(Format-FileSize $totalSize)"
Write-Host ""
Write-Host "Files included:"
foreach ($f in $files) {
    $rel = $f.FullName.Substring($projectDir.Length).TrimStart('\')
    Write-Host "  $rel"
}
