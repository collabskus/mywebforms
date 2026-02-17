# Export ASP.NET Project Files to Single Text File
# PowerShell 5 compatible script
# Use this to create a comprehensive dump of your project for LLM code review

param(
    [string]$ProjectPath = ".",
    [string]$OutputFile = "docs/llm/dump.txt"
)

# Define file extensions to include
$IncludeExtensions = @(
    "*.cs",           # C# files
    "*.json",         # JSON configuration files
    "*.xml",          # XML files
    "*.csproj",       # C# project files
    "*.sln",          # Solution files
    "*.config",       # Configuration files
    "*.cshtml",       # Razor views
    "*.razor",        # Razor components
    "*.js",           # JavaScript files
    "*.css",          # CSS files
    "*.scss",         # SCSS files
    "*.html",         # HTML files
    "*.yml",          # YAML files
    "*.yaml",         # YAML files
    "*.sql",          # SQL files
    "*.props",        # MSBuild props files
    "*.targets",      # MSBuild targets files
    "*.sh",           # Shell scripts
    "*.aspx",         # Web Forms pages
    "*.ascx",         # Web Forms user controls
    "*.master"        # Web Forms master pages
)

# Specific files without extensions to include
$IncludeSpecificFiles = @(
    "Dockerfile",
    "Dockerfile.*",
    ".dockerignore",
    ".editorconfig",
    ".gitignore",
    ".gitattributes"
)

# Directories to exclude
$ExcludeDirectories = @(
    "bin",
    "obj",
    ".vs",
    ".git",
    "node_modules",
    "packages",
    ".vscode",
    ".idea",
    "docs",           # Documentation folder
    "Scripts\WebForms",  # Built-in WebForms scripts (MsAjax, GridView, etc.)
    "Scripts\lib"        # Third-party lib folder (Chart.js, etc.)
)

# Files to exclude
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
    # Bootstrap CSS (all variants)
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
    # Animate.css
    "animate*.css",
    # Designer files - auto-generated, rarely useful for review
    "*.designer.cs"
)

Write-Host "Starting project export..." -ForegroundColor Green
Write-Host "Project Path: $ProjectPath" -ForegroundColor Yellow
Write-Host "Output File: $OutputFile" -ForegroundColor Yellow

# Initialize output file
$OutputPath = Join-Path $ProjectPath $OutputFile
"" | Out-File -FilePath $OutputPath -Encoding UTF8

# Add header
$Header = @"
===============================================================================
ASP.NET PROJECT EXPORT
Generated: $(Get-Date)
Project Path: $((Resolve-Path $ProjectPath).Path)
===============================================================================

"@

$Header | Out-File -FilePath $OutputPath -Append -Encoding UTF8

# Generate directory structure
Write-Host "Generating directory structure..." -ForegroundColor Cyan

"DIRECTORY STRUCTURE:" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
"===================" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
"" | Out-File -FilePath $OutputPath -Append -Encoding UTF8

try {
    $treeOutput = & tree $ProjectPath /F /A 2>$null
    if ($LASTEXITCODE -eq 0) {
        $treeOutput | Out-File -FilePath $OutputPath -Append -Encoding UTF8
    } else {
        throw "Tree command failed"
    }
} catch {
    Write-Host "Tree command not available, using PowerShell alternative..." -ForegroundColor Yellow

    function Get-DirectoryTree {
        param([string]$Path, [string]$Prefix = "")

        $items = Get-ChildItem -Path $Path -Force | Where-Object {
            $_.Name -notin $ExcludeDirectories
        } | Sort-Object @{Expression={$_.PSIsContainer}; Descending=$true}, Name

        for ($i = 0; $i -lt $items.Count; $i++) {
            $item = $items[$i]
            $isLast = ($i -eq $items.Count - 1)
            $connector = if ($isLast) { "+-- " } else { "+-- " }

            "$Prefix$connector$($item.Name)" | Out-File -FilePath $OutputPath -Append -Encoding UTF8

            if ($item.PSIsContainer) {
                $newPrefix = if ($isLast) { "$Prefix    " } else { "$Prefix|   " }
                Get-DirectoryTree -Path $item.FullName -Prefix $newPrefix
            }
        }
    }

    (Split-Path $ProjectPath -Leaf) | Out-File -FilePath $OutputPath -Append -Encoding UTF8
    Get-DirectoryTree -Path $ProjectPath
}

"" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
"" | Out-File -FilePath $OutputPath -Append -Encoding UTF8

# Get all relevant files
Write-Host "Collecting files..." -ForegroundColor Cyan

$AllFiles = @()

# Helper: check if a file should be excluded
function Should-Exclude {
    param($File)

    # Check excluded directories (supports both \ and / path separators)
    foreach ($excludeDir in $ExcludeDirectories) {
        $normalizedExclude = $excludeDir.Replace("/", "\")
        if ($File.FullName -like "*\$normalizedExclude\*") {
            return $true
        }
    }

    # Check excluded file patterns
    foreach ($excludePattern in $ExcludeFiles) {
        if ($File.Name -like $excludePattern) {
            return $true
        }
    }

    return $false
}

# Collect files by extension
foreach ($extension in $IncludeExtensions) {
    $files = Get-ChildItem -Path $ProjectPath -Recurse -Include $extension -File |
        Where-Object { -not (Should-Exclude $_) }
    $AllFiles += $files
}

# Collect specific files without extensions (like Dockerfile)
foreach ($specificFile in $IncludeSpecificFiles) {
    $files = Get-ChildItem -Path $ProjectPath -Recurse -Include $specificFile -File |
        Where-Object { -not (Should-Exclude $_) }
    $AllFiles += $files
}

# Remove duplicates and sort
$AllFiles = $AllFiles | Sort-Object FullName -Unique

Write-Host "Found $($AllFiles.Count) files to export" -ForegroundColor Green

# Export each file
"FILE CONTENTS:" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
"==============" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
"" | Out-File -FilePath $OutputPath -Append -Encoding UTF8

$fileCount = 0
foreach ($file in $AllFiles) {
    $fileCount++
    $relativePath = $file.FullName.Substring($ProjectPath.Length).TrimStart('\')

    Write-Host "Processing ($fileCount/$($AllFiles.Count)): $relativePath" -ForegroundColor White

    $separator = "=" * 80
    $fileHeader = @"
$separator
FILE: $relativePath
SIZE: $([math]::Round($file.Length / 1KB, 2)) KB
MODIFIED: $($file.LastWriteTime)
$separator

"@

    $fileHeader | Out-File -FilePath $OutputPath -Append -Encoding UTF8

    try {
        $content = Get-Content -Path $file.FullName -Raw -ErrorAction Stop
        if ($content) {
            $content | Out-File -FilePath $OutputPath -Append -Encoding UTF8
        } else {
            "[EMPTY FILE]" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
        }
    } catch {
        "[ERROR READING FILE: $($_.Exception.Message)]" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
    }

    "" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
    "" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
}

# Add footer
$Footer = @"
===============================================================================
EXPORT COMPLETED: $(Get-Date)
Total Files Exported: $fileCount
Output File: $OutputPath
===============================================================================
"@

$Footer | Out-File -FilePath $OutputPath -Append -Encoding UTF8

Write-Host "`nExport completed successfully!" -ForegroundColor Green
Write-Host "Output file: $OutputPath" -ForegroundColor Yellow
Write-Host "Total files exported: $fileCount" -ForegroundColor Green

$outputFileInfo = Get-Item $OutputPath
Write-Host "Output file size: $([math]::Round($outputFileInfo.Length / 1MB, 2)) MB" -ForegroundColor Cyan
