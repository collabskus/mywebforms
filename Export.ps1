# Export ASP.NET Project Files to Single Text File
# PowerShell 5 compatible script
# Use this to create a comprehensive dump of your project for LLM code review

param(
    [string]$ProjectPath = ".",
    [string]$OutputFile  = "docs/llm/dump.txt"
)

# ---------------------------------------------------------------------------
# File extensions to include
# ---------------------------------------------------------------------------
$IncludeExtensions = @(
    "*.cs",       # C# source (including *.designer.cs — useful for LLM control-tree review)
    "*.json",     # JSON configuration
    "*.xml",      # XML files
    "*.csproj",   # C# project files
    "*.sln",      # Solution files
    "*.config",   # Web.config, packages.config, etc.
    "*.cshtml",   # Razor views
    "*.razor",    # Razor components
    "*.js",       # JavaScript files
    "*.css",      # CSS files
    "*.scss",     # SCSS files
    "*.html",     # HTML files
    "*.yml",      # YAML files
    "*.yaml",     # YAML files
    "*.sql",      # SQL files
    "*.props",    # MSBuild props
    "*.targets",  # MSBuild targets
    "*.sh",       # Shell scripts
    "*.aspx",     # Web Forms pages
    "*.ascx",     # Web Forms user controls
    "*.master"    # Web Forms master pages
)

# Specific filenames (no extension) to include
$IncludeSpecificFiles = @(
    "Dockerfile",
    "Dockerfile.*",
    ".dockerignore",
    ".editorconfig",
    ".gitignore",
    ".gitattributes"
)

# ---------------------------------------------------------------------------
# Directories to exclude (matched against any segment of the full path)
# Keep entries as simple folder names where possible; use sub-path entries
# only when a partial path is required (e.g. Scripts\WebForms).
# ---------------------------------------------------------------------------
$ExcludeDirectories = @(
    "bin",
    "obj",
    ".vs",
    ".git",
    "node_modules",
    "packages",
    ".vscode",
    ".idea",
    "docs",            # Documentation / generated output folder (dump.txt lives here)
    "Scripts\WebForms", # Built-in WebForms scripts (MsAjax, GridView, etc.) — minified, not authored
    "Scripts\lib"       # Third-party client libs acquired via LibMan (Chart.js, etc.)
)

# ---------------------------------------------------------------------------
# File patterns to exclude
# ---------------------------------------------------------------------------
$ExcludeFiles = @(
    "*.exe",
    "*.dll",
    "*.pdb",
    "*.cache",
    "*.log",
    "*.md",
    "*.txt",
    "LICENSE*",
    "LICENCE*",
    # Bootstrap CSS (all variants — dist output, not authored)
    "bootstrap*.css",
    "bootstrap*.css.map",
    # Bootstrap JS (all variants)
    "bootstrap*.js",
    "bootstrap*.js.map",
    # jQuery (all variants)
    "jquery*.js",
    "jquery*.js.map",
    # Modernizr
    "modernizr*.js",
    # Animate.css (LibMan-acquired)
    "animate*.css"
    # NOTE: *.designer.cs is intentionally NOT excluded.
    # Designer files declare the server-control fields for every .aspx/.ascx/.master
    # and are essential for the LLM to understand the full control tree of each page.
    # They are auto-generated and should never be edited manually, but they ARE
    # valuable reference material for code review and assistance.
)

# ---------------------------------------------------------------------------
# Helper — resolve and normalise the project root to a consistent absolute path
# so that relative-path trimming works correctly on both \ and / separators.
# ---------------------------------------------------------------------------
$ResolvedProject = (Resolve-Path $ProjectPath).Path.TrimEnd('\').TrimEnd('/')

# ---------------------------------------------------------------------------
# Helper — returns $true if $File should be excluded
# ---------------------------------------------------------------------------
function Should-Exclude {
    param([System.IO.FileInfo]$File)

    # Normalise to backslashes for consistent matching on Windows
    $fullPath = $File.FullName.Replace('/', '\')

    # Check excluded directory segments.
    # Each entry is tested as a whole path segment (surrounded by \) so that
    # e.g. "bin" does not accidentally match a folder called "cabin".
    foreach ($excludeDir in $ExcludeDirectories) {
        $normalised = $excludeDir.Replace('/', '\')
        # Match as an interior segment (\bin\) or a trailing segment (\bin at end of string).
        if ($fullPath -like "*\$normalised\*" -or $fullPath -like "*\$normalised") {
            return $true
        }
    }

    # Check excluded file-name patterns
    foreach ($pattern in $ExcludeFiles) {
        if ($File.Name -like $pattern) {
            return $true
        }
    }

    return $false
}

# ---------------------------------------------------------------------------
# Helper — PowerShell-native directory tree (used when tree.com is unavailable)
# Respects $ExcludeDirectories by name segment, not full path, because at this
# point we are walking the tree and only have the item Name available cheaply.
# ---------------------------------------------------------------------------
function Write-DirectoryTree {
    param(
        [string]$Path,
        [string]$Prefix = "",
        [string]$OutFile
    )

    # Exclude by simple folder name (the path-based entries won't match here,
    # but simple names like bin/obj/packages/.vs/.git will be caught).
    $simpleExcludes = $ExcludeDirectories | ForEach-Object { $_.Split('\')[-1] }

    $items = Get-ChildItem -Path $Path -Force -ErrorAction SilentlyContinue |
             Where-Object { $_.Name -notin $simpleExcludes } |
             Sort-Object @{ Expression = { $_.PSIsContainer }; Descending = $true }, Name

    for ($i = 0; $i -lt $items.Count; $i++) {
        $item   = $items[$i]
        $isLast = ($i -eq $items.Count - 1)
        $branch = if ($isLast) { "+-- " } else { "+-- " }

        "$Prefix$branch$($item.Name)" | Out-File -FilePath $OutFile -Append -Encoding UTF8

        if ($item.PSIsContainer) {
            $childPrefix = if ($isLast) { "$Prefix    " } else { "$Prefix|   " }
            Write-DirectoryTree -Path $item.FullName -Prefix $childPrefix -OutFile $OutFile
        }
    }
}

# ===========================================================================
# Main
# ===========================================================================

Write-Host "Starting project export..." -ForegroundColor Green
Write-Host "Project Path : $ResolvedProject"  -ForegroundColor Yellow
Write-Host "Output File  : $OutputFile"        -ForegroundColor Yellow

# Ensure output directory exists
$OutputPath    = Join-Path $ResolvedProject $OutputFile
$OutputDir     = Split-Path $OutputPath -Parent
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# Write header
@"
===============================================================================
ASP.NET PROJECT EXPORT
Generated: $(Get-Date)
Project Path: $ResolvedProject
===============================================================================

"@ | Out-File -FilePath $OutputPath -Encoding UTF8

# ---------------------------------------------------------------------------
# Directory tree
# ---------------------------------------------------------------------------
Write-Host "Generating directory structure..." -ForegroundColor Cyan

"DIRECTORY STRUCTURE:" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
"===================" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
""                    | Out-File -FilePath $OutputPath -Append -Encoding UTF8

$treeWritten = $false
try {
    $treeOutput = & tree $ResolvedProject /F /A 2>$null
    if ($LASTEXITCODE -eq 0 -and $treeOutput) {
        $treeOutput | Out-File -FilePath $OutputPath -Append -Encoding UTF8
        $treeWritten = $true
    }
} catch { }

if (-not $treeWritten) {
    Write-Host "tree.com unavailable — using PowerShell fallback." -ForegroundColor Yellow
    (Split-Path $ResolvedProject -Leaf) | Out-File -FilePath $OutputPath -Append -Encoding UTF8
    Write-DirectoryTree -Path $ResolvedProject -OutFile $OutputPath
}

"" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
"" | Out-File -FilePath $OutputPath -Append -Encoding UTF8

# ---------------------------------------------------------------------------
# Collect files
# ---------------------------------------------------------------------------
Write-Host "Collecting files..." -ForegroundColor Cyan

$AllFiles = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
$seen     = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

# Sweep by extension
foreach ($ext in $IncludeExtensions) {
    $found = Get-ChildItem -Path $ResolvedProject -Recurse -Include $ext -File -ErrorAction SilentlyContinue |
             Where-Object { -not (Should-Exclude $_) }
    foreach ($f in $found) {
        if ($seen.Add($f.FullName)) { $AllFiles.Add($f) }
    }
}

# Sweep for specific filenames
foreach ($spec in $IncludeSpecificFiles) {
    $found = Get-ChildItem -Path $ResolvedProject -Recurse -Include $spec -File -ErrorAction SilentlyContinue |
             Where-Object { -not (Should-Exclude $_) }
    foreach ($f in $found) {
        if ($seen.Add($f.FullName)) { $AllFiles.Add($f) }
    }
}

# Sort for deterministic output
$AllFiles = $AllFiles | Sort-Object FullName

Write-Host "Found $($AllFiles.Count) files to export" -ForegroundColor Green

# ---------------------------------------------------------------------------
# Write file contents
# ---------------------------------------------------------------------------
"FILE CONTENTS:" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
"==============" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
""               | Out-File -FilePath $OutputPath -Append -Encoding UTF8

$fileCount = 0
foreach ($file in $AllFiles) {
    $fileCount++
    # Trim the resolved project root to get a clean relative path
    $relativePath = $file.FullName.Substring($ResolvedProject.Length).TrimStart('\').TrimStart('/')

    Write-Host "  ($fileCount/$($AllFiles.Count)) $relativePath" -ForegroundColor White

    $bar = "=" * 80
    @"
$bar
FILE: $($file.FullName)
SIZE: $([math]::Round($file.Length / 1KB, 2)) KB
MODIFIED: $($file.LastWriteTime)
$bar

"@ | Out-File -FilePath $OutputPath -Append -Encoding UTF8

    try {
        $content = Get-Content -Path $file.FullName -Raw -ErrorAction Stop
        if ([string]::IsNullOrEmpty($content)) {
            "[EMPTY FILE]" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
        } else {
            $content | Out-File -FilePath $OutputPath -Append -Encoding UTF8
        }
    } catch {
        "[ERROR READING FILE: $($_.Exception.Message)]" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
    }

    "" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
    "" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
}

# ---------------------------------------------------------------------------
# Footer
# ---------------------------------------------------------------------------
@"
===============================================================================
EXPORT COMPLETED: $(Get-Date)
Total Files Exported: $fileCount
Output File: $OutputPath
===============================================================================
"@ | Out-File -FilePath $OutputPath -Append -Encoding UTF8

$sizeKB = [math]::Round((Get-Item $OutputPath).Length / 1MB, 2)
Write-Host "`nExport complete." -ForegroundColor Green
Write-Host "Output : $OutputPath ($sizeKB MB, $fileCount files)" -ForegroundColor Yellow
