# Visual Studio MCP Server - Release Management Script
# This script manages the complete release process including versioning, changelog, and publishing

param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[a-zA-Z0-9]+(\.[a-zA-Z0-9]+)*)?$')]
    [string]$Version,
    
    [ValidateSet('patch', 'minor', 'major', 'prerelease')]
    [string]$VersionBump = 'patch',
    
    [switch]$DryRun = $false,
    [switch]$SkipTests = $false,
    [switch]$SkipGitOperations = $false,
    [switch]$CreateGitHubRelease = $true,
    [switch]$PublishToNuGet = $true,
    
    [string]$GitHubToken = $env:GITHUB_TOKEN,
    [string]$NuGetApiKey = $env:NUGET_API_KEY,
    [string]$Branch = "main"
)

# Script configuration
$ErrorActionPreference = "Stop"
$ProjectFile = "src\VisualStudioMcp.Server\VisualStudioMcp.Server.csproj"
$SolutionFile = "VisualStudioMcp.sln"
$ChangelogFile = "CHANGELOG.md"

Write-Host "🚀 Visual Studio MCP Server - Release Management" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
}

# =====================================
# Validation Functions
# =====================================

function Test-Prerequisites {
    Write-Host "📋 Checking prerequisites..." -ForegroundColor Yellow
    
    # Check required tools
    $requiredTools = @('git', 'dotnet', 'gh')
    foreach ($tool in $requiredTools) {
        if (!(Get-Command $tool -ErrorAction SilentlyContinue)) {
            Write-Error "Required tool not found: $tool"
        }
    }
    
    # Check .NET version
    $dotnetVersion = dotnet --version
    if (-not $dotnetVersion.StartsWith('8.')) {
        Write-Error ".NET 8 SDK required. Found: $dotnetVersion"
    }
    
    # Check Git status
    $gitStatus = git status --porcelain
    if ($gitStatus -and !$SkipGitOperations) {
        Write-Error "Working directory is not clean. Commit or stash changes before release."
    }
    
    # Check current branch
    $currentBranch = git branch --show-current
    if ($currentBranch -ne $Branch -and !$SkipGitOperations) {
        Write-Error "Not on $Branch branch. Current branch: $currentBranch"
    }
    
    # Check GitHub CLI authentication
    if ($CreateGitHubRelease) {
        try {
            gh auth status | Out-Null
        } catch {
            Write-Error "GitHub CLI not authenticated. Run 'gh auth login' first."
        }
    }
    
    Write-Host "✅ All prerequisites checked" -ForegroundColor Green
}

function Get-CurrentVersion {
    $content = Get-Content $ProjectFile -Raw
    if ($content -match '<Version>(.*?)</Version>') {
        return $matches[1]
    }
    Write-Error "Could not find version in $ProjectFile"
}

function Update-ProjectVersion {
    param([string]$NewVersion)
    
    Write-Host "🏷️  Updating version to $NewVersion..." -ForegroundColor Yellow
    
    if ($DryRun) {
        Write-Host "   [DRY RUN] Would update $ProjectFile" -ForegroundColor Gray
        return
    }
    
    $content = Get-Content $ProjectFile -Raw
    $content = $content -replace '<Version>.*?</Version>', "<Version>$NewVersion</Version>"
    $content = $content -replace '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$NewVersion.0</AssemblyVersion>"
    $content = $content -replace '<FileVersion>.*?</FileVersion>', "<FileVersion>$NewVersion.0</FileVersion>"
    $content = $content -replace '<InformationalVersion>.*?</InformationalVersion>', "<InformationalVersion>$NewVersion</InformationalVersion>"
    
    Set-Content $ProjectFile -Value $content
    Write-Host "✅ Project version updated" -ForegroundColor Green
}

function Update-Changelog {
    param([string]$NewVersion)
    
    Write-Host "📝 Updating changelog..." -ForegroundColor Yellow
    
    $today = Get-Date -Format "yyyy-MM-dd"
    $newEntry = @"
## [$NewVersion] - $today

### Added
- New features and enhancements in this release

### Changed  
- Improvements and modifications to existing functionality

### Fixed
- Bug fixes and issue resolutions

### Security
- Security improvements and vulnerability fixes

"@

    if ($DryRun) {
        Write-Host "   [DRY RUN] Would add changelog entry for $NewVersion" -ForegroundColor Gray
        return
    }
    
    if (Test-Path $ChangelogFile) {
        $content = Get-Content $ChangelogFile -Raw
        $updatedContent = $content -replace '(# Changelog\s*)', "`$1`n$newEntry"
        Set-Content $ChangelogFile -Value $updatedContent
    } else {
        $changelogContent = @"
# Changelog

All notable changes to the Visual Studio MCP Server project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

$newEntry
"@
        Set-Content $ChangelogFile -Value $changelogContent
    }
    
    Write-Host "✅ Changelog updated" -ForegroundColor Green
}

function Invoke-Build {
    Write-Host "🏗️ Building solution..." -ForegroundColor Yellow
    
    if ($DryRun) {
        Write-Host "   [DRY RUN] Would build solution" -ForegroundColor Gray
        return
    }
    
    # Clean
    dotnet clean $SolutionFile --configuration Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) { Write-Error "Clean failed" }
    
    # Restore
    dotnet restore $SolutionFile --verbosity minimal
    if ($LASTEXITCODE -ne 0) { Write-Error "Restore failed" }
    
    # Build
    dotnet build $SolutionFile --configuration Release --no-restore --verbosity minimal
    if ($LASTEXITCODE -ne 0) { Write-Error "Build failed" }
    
    Write-Host "✅ Build completed" -ForegroundColor Green
}

function Invoke-Tests {
    if ($SkipTests) {
        Write-Host "⏭️  Skipping tests" -ForegroundColor Yellow
        return
    }
    
    Write-Host "🧪 Running tests..." -ForegroundColor Yellow
    
    if ($DryRun) {
        Write-Host "   [DRY RUN] Would run tests" -ForegroundColor Gray
        return
    }
    
    dotnet test $SolutionFile --configuration Release --no-build --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed. Release aborted."
    }
    
    Write-Host "✅ All tests passed" -ForegroundColor Green
}

function New-GitTag {
    param([string]$TagVersion)
    
    if ($SkipGitOperations) {
        Write-Host "⏭️  Skipping Git operations" -ForegroundColor Yellow
        return
    }
    
    Write-Host "🏷️  Creating Git tag..." -ForegroundColor Yellow
    
    $tagName = "v$TagVersion"
    $tagMessage = "Release version $TagVersion"
    
    if ($DryRun) {
        Write-Host "   [DRY RUN] Would create tag: $tagName" -ForegroundColor Gray
        return
    }
    
    # Commit version changes
    git add $ProjectFile $ChangelogFile
    git commit -m "chore: bump version to $TagVersion"
    
    # Create and push tag
    git tag -a $tagName -m $tagMessage
    git push origin $Branch
    git push origin $tagName
    
    Write-Host "✅ Git tag created and pushed" -ForegroundColor Green
}

function New-GitHubRelease {
    param([string]$TagVersion)
    
    if (!$CreateGitHubRelease) {
        Write-Host "⏭️  Skipping GitHub release" -ForegroundColor Yellow
        return
    }
    
    Write-Host "🚀 Creating GitHub release..." -ForegroundColor Yellow
    
    $tagName = "v$TagVersion"
    $releaseTitle = "Visual Studio MCP Server $TagVersion"
    
    if ($DryRun) {
        Write-Host "   [DRY RUN] Would create GitHub release: $releaseTitle" -ForegroundColor Gray
        return
    }
    
    # Extract changelog for this version
    $changelogNotes = ""
    if (Test-Path $ChangelogFile) {
        $content = Get-Content $ChangelogFile -Raw
        if ($content -match "## \[$TagVersion\].*?(?=## \[|\z)") {
            $changelogNotes = $matches[0]
        }
    }
    
    # Create release notes
    $releaseNotes = @"
# Visual Studio MCP Server v$TagVersion

## 🚀 What's New

$changelogNotes

## 📦 Installation

Install as a .NET global tool:

``````bash
dotnet tool install --global VisualStudioMcp
``````

## 🔧 Claude Code Configuration

Add to your ``mcp_servers.json``:

``````json
{
  "mcpServers": {
    "visual-studio": {
      "command": "vsmcp",
      "args": [],
      "env": {}
    }
  }
}
``````

## 📚 Documentation

- [Complete API Reference](https://github.com/automint/visual-studio-mcp-server/blob/main/docs/api/mcp-tools-reference.md)
- [Installation Guide](https://github.com/automint/visual-studio-mcp-server/blob/main/docs/operations/installation-guide.md)
- [Claude Code Integration](https://github.com/automint/visual-studio-mcp-server/blob/main/docs/user-guides/claude-code-integration.md)

## 🔧 System Requirements

- Windows 10/11
- Visual Studio 2022 (17.8+)
- .NET 8 Runtime
- Claude Code
"@
    
    # Save release notes to file
    $releaseNotes | Out-File -FilePath "release-notes-temp.md" -Encoding utf8
    
    # Create GitHub release
    try {
        $packageFile = Get-ChildItem "artifacts" -Filter "VisualStudioMcp.*.nupkg" -ErrorAction SilentlyContinue | Select-Object -First 1
        
        if ($packageFile) {
            gh release create $tagName $packageFile.FullName `
                --title $releaseTitle `
                --notes-file "release-notes-temp.md" `
                --target $Branch
        } else {
            gh release create $tagName `
                --title $releaseTitle `
                --notes-file "release-notes-temp.md" `
                --target $Branch
        }
        
        Write-Host "✅ GitHub release created: $releaseTitle" -ForegroundColor Green
    } finally {
        Remove-Item "release-notes-temp.md" -ErrorAction SilentlyContinue
    }
}

function Publish-ToNuGet {
    param([string]$PackageVersion)
    
    if (!$PublishToNuGet) {
        Write-Host "⏭️  Skipping NuGet publishing" -ForegroundColor Yellow
        return
    }
    
    if (!$NuGetApiKey) {
        Write-Warning "NuGet API key not provided. Skipping NuGet publishing."
        return
    }
    
    Write-Host "📦 Publishing to NuGet..." -ForegroundColor Yellow
    
    if ($DryRun) {
        Write-Host "   [DRY RUN] Would publish to NuGet" -ForegroundColor Gray
        return
    }
    
    # Package the tool
    $outputPath = "artifacts"
    if (!(Test-Path $outputPath)) {
        New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
    }
    
    dotnet pack $ProjectFile --configuration Release --no-build --output $outputPath
    if ($LASTEXITCODE -ne 0) { Write-Error "Packaging failed" }
    
    # Find package file
    $packageFile = Get-ChildItem $outputPath -Filter "VisualStudioMcp.*.nupkg" | Select-Object -First 1
    if (!$packageFile) {
        Write-Error "Package file not found"
    }
    
    # Publish to NuGet
    dotnet nuget push $packageFile.FullName `
        --api-key $NuGetApiKey `
        --source https://api.nuget.org/v3/index.json `
        --timeout 300
    
    if ($LASTEXITCODE -ne 0) { Write-Error "NuGet publishing failed" }
    
    Write-Host "✅ Published to NuGet: $($packageFile.Name)" -ForegroundColor Green
    Write-Host "🌐 Package available at: https://www.nuget.org/packages/VisualStudioMcp/$PackageVersion" -ForegroundColor Cyan
}

# =====================================
# Main Release Process
# =====================================

try {
    # Validate prerequisites
    Test-Prerequisites
    
    # Display current state
    $currentVersion = Get-CurrentVersion
    Write-Host "📋 Release Information" -ForegroundColor Cyan
    Write-Host "======================" -ForegroundColor Cyan
    Write-Host "Current Version: $currentVersion" -ForegroundColor White
    Write-Host "Target Version:  $Version" -ForegroundColor White
    Write-Host "Branch:          $Branch" -ForegroundColor White
    Write-Host "Dry Run:         $DryRun" -ForegroundColor White
    Write-Host ""
    
    # Confirm release
    if (!$DryRun) {
        Write-Host "⚠️  This will create a new release. Continue? (y/N): " -ForegroundColor Yellow -NoNewline
        $confirmation = Read-Host
        if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
            Write-Host "❌ Release cancelled by user" -ForegroundColor Red
            exit 0
        }
    }
    
    Write-Host "🚀 Starting release process..." -ForegroundColor Cyan
    
    # Step 1: Update version
    Update-ProjectVersion -NewVersion $Version
    
    # Step 2: Update changelog
    Update-Changelog -NewVersion $Version
    
    # Step 3: Build solution
    Invoke-Build
    
    # Step 4: Run tests
    Invoke-Tests
    
    # Step 5: Create Git tag
    New-GitTag -TagVersion $Version
    
    # Step 6: Publish to NuGet
    Publish-ToNuGet -PackageVersion $Version
    
    # Step 7: Create GitHub release
    New-GitHubRelease -TagVersion $Version
    
    # Success summary
    Write-Host ""
    Write-Host "🎉 Release Completed Successfully!" -ForegroundColor Green
    Write-Host "==================================" -ForegroundColor Green
    Write-Host "✅ Version:        $Version" -ForegroundColor Green
    Write-Host "✅ Git Tag:        v$Version" -ForegroundColor Green
    
    if ($PublishToNuGet -and $NuGetApiKey) {
        Write-Host "✅ NuGet:          Published" -ForegroundColor Green
    }
    
    if ($CreateGitHubRelease) {
        Write-Host "✅ GitHub Release: Created" -ForegroundColor Green
    }
    
    Write-Host ""
    Write-Host "🔗 Next Steps:" -ForegroundColor Cyan
    Write-Host "- Verify release at: https://github.com/automint/visual-studio-mcp-server/releases" -ForegroundColor White
    Write-Host "- Monitor NuGet downloads: https://www.nuget.org/packages/VisualStudioMcp" -ForegroundColor White
    Write-Host "- Update documentation if needed" -ForegroundColor White
    Write-Host "- Communicate release to team/users" -ForegroundColor White
    
} catch {
    Write-Host ""
    Write-Host "❌ Release Failed!" -ForegroundColor Red
    Write-Host "==================" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "🔍 Troubleshooting:" -ForegroundColor Yellow
    Write-Host "- Check all prerequisites are installed" -ForegroundColor White
    Write-Host "- Verify Git status is clean" -ForegroundColor White
    Write-Host "- Ensure you're on the correct branch" -ForegroundColor White
    Write-Host "- Check GitHub CLI authentication" -ForegroundColor White
    Write-Host "- Verify NuGet API key if publishing" -ForegroundColor White
    
    exit 1
}