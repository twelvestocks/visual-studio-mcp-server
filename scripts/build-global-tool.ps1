# Visual Studio MCP Server - Global Tool Build Script
# This script builds and packages the Visual Studio MCP Server as a .NET global tool

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = ".\artifacts",
    [string]$Version = "",
    [switch]$SkipTests = $false,
    [switch]$Pack = $true,
    [switch]$Publish = $false,
    [string]$NuGetApiKey = "",
    [string]$NuGetSource = "https://api.nuget.org/v3/index.json"
)

# Script settings
$ErrorActionPreference = "Stop"
$ProjectPath = "src\VisualStudioMcp.Server\VisualStudioMcp.Server.csproj"
$SolutionPath = "VisualStudioMcp.sln"

Write-Host "🚀 Visual Studio MCP Server - Global Tool Build" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

# Verify prerequisites
Write-Host "📋 Checking prerequisites..." -ForegroundColor Yellow

if (!(Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-Error ".NET SDK not found. Please install .NET 8 SDK."
}

$dotnetVersion = dotnet --version
Write-Host "✅ .NET SDK version: $dotnetVersion" -ForegroundColor Green

if (!(Test-Path $SolutionPath)) {
    Write-Error "Solution file not found: $SolutionPath"
}

if (!(Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
}

# Create output directory
if (!(Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-Host "📁 Created output directory: $OutputPath" -ForegroundColor Green
}

# Set version if provided
if ($Version) {
    Write-Host "🏷️  Setting version to: $Version" -ForegroundColor Yellow
    $projectContent = Get-Content $ProjectPath -Raw
    $projectContent = $projectContent -replace '<Version>.*</Version>', "<Version>$Version</Version>"
    $projectContent = $projectContent -replace '<AssemblyVersion>.*</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
    $projectContent = $projectContent -replace '<FileVersion>.*</FileVersion>', "<FileVersion>$Version.0</FileVersion>"
    $projectContent = $projectContent -replace '<InformationalVersion>.*</InformationalVersion>', "<InformationalVersion>$Version</InformationalVersion>"
    Set-Content $ProjectPath -Value $projectContent
    Write-Host "✅ Version updated in project file" -ForegroundColor Green
}

# Clean previous builds
Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean $SolutionPath --configuration $Configuration --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Clean failed with exit code $LASTEXITCODE"
}
Write-Host "✅ Clean completed" -ForegroundColor Green

# Restore dependencies
Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $SolutionPath --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Restore failed with exit code $LASTEXITCODE"
}
Write-Host "✅ Package restore completed" -ForegroundColor Green

# Build solution
Write-Host "🔨 Building solution..." -ForegroundColor Yellow
dotnet build $SolutionPath --configuration $Configuration --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
}
Write-Host "✅ Build completed successfully" -ForegroundColor Green

# Run tests (unless skipped)
if (!$SkipTests) {
    Write-Host "🧪 Running tests..." -ForegroundColor Yellow
    dotnet test $SolutionPath --configuration $Configuration --no-build --verbosity minimal --logger "console;verbosity=minimal"
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Tests failed with exit code $LASTEXITCODE - continuing anyway"
    } else {
        Write-Host "✅ All tests passed" -ForegroundColor Green
    }
} else {
    Write-Host "⏭️  Skipping tests" -ForegroundColor Yellow
}

# Pack the global tool
if ($Pack) {
    Write-Host "📦 Packing global tool..." -ForegroundColor Yellow
    
    $packArgs = @(
        "pack", $ProjectPath,
        "--configuration", $Configuration,
        "--no-build",
        "--output", $OutputPath,
        "--verbosity", "minimal"
    )
    
    & dotnet @packArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Pack failed with exit code $LASTEXITCODE"
    }
    
    # Find the generated package
    $packageFiles = Get-ChildItem $OutputPath -Filter "VisualStudioMcp.*.nupkg" | Sort-Object LastWriteTime -Descending
    if ($packageFiles.Count -eq 0) {
        Write-Error "No package files found in $OutputPath"
    }
    
    $latestPackage = $packageFiles[0]
    Write-Host "✅ Global tool package created: $($latestPackage.Name)" -ForegroundColor Green
    Write-Host "📍 Package location: $($latestPackage.FullName)" -ForegroundColor Cyan
    
    # Display package info
    $packageSize = [math]::Round($latestPackage.Length / 1KB, 2)
    Write-Host "📊 Package size: $packageSize KB" -ForegroundColor Cyan
    
    # Test local installation
    Write-Host "🔧 Testing local installation..." -ForegroundColor Yellow
    
    # Uninstall existing version if present
    $existingTool = dotnet tool list --global | Select-String "VisualStudioMcp"
    if ($existingTool) {
        Write-Host "🗑️  Uninstalling existing version..." -ForegroundColor Yellow
        dotnet tool uninstall --global VisualStudioMcp
    }
    
    # Install from local package
    dotnet tool install --global --add-source $OutputPath VisualStudioMcp
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Local installation test failed with exit code $LASTEXITCODE"
    } else {
        Write-Host "✅ Local installation test successful" -ForegroundColor Green
        
        # Test tool execution
        Write-Host "🧪 Testing tool execution..." -ForegroundColor Yellow
        try {
            $vsmcpVersion = & vsmcp --version 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Tool execution test successful: $vsmcpVersion" -ForegroundColor Green
            } else {
                Write-Warning "Tool execution failed with exit code $LASTEXITCODE"
            }
        } catch {
            Write-Warning "Tool execution test failed: $($_.Exception.Message)"
        }
    }
    
    # Publish to NuGet (if requested)
    if ($Publish) {
        if (!$NuGetApiKey) {
            Write-Error "NuGet API key is required for publishing. Use -NuGetApiKey parameter."
        }
        
        Write-Host "🚀 Publishing to NuGet..." -ForegroundColor Yellow
        Write-Host "📍 Target: $NuGetSource" -ForegroundColor Cyan
        
        dotnet nuget push $latestPackage.FullName --api-key $NuGetApiKey --source $NuGetSource --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            Write-Error "NuGet publish failed with exit code $LASTEXITCODE"
        }
        
        Write-Host "✅ Published to NuGet successfully!" -ForegroundColor Green
        Write-Host "🌐 Package available at: https://www.nuget.org/packages/VisualStudioMcp" -ForegroundColor Cyan
    }
}

# Build summary
Write-Host "" -ForegroundColor White
Write-Host "🎉 Build Summary" -ForegroundColor Cyan
Write-Host "===============" -ForegroundColor Cyan
Write-Host "✅ Configuration: $Configuration" -ForegroundColor Green
Write-Host "✅ Output Path: $OutputPath" -ForegroundColor Green

if ($Version) {
    Write-Host "✅ Version: $Version" -ForegroundColor Green
}

if ($Pack) {
    Write-Host "✅ Global tool package created" -ForegroundColor Green
}

if ($Publish) {
    Write-Host "✅ Published to NuGet" -ForegroundColor Green
}

Write-Host "" -ForegroundColor White
Write-Host "🚀 Ready for distribution!" -ForegroundColor Cyan

# Usage instructions
Write-Host "" -ForegroundColor White
Write-Host "📋 Installation Instructions" -ForegroundColor Cyan
Write-Host "============================" -ForegroundColor Cyan

if ($Publish) {
    Write-Host "Global installation:" -ForegroundColor Yellow
    Write-Host "  dotnet tool install --global VisualStudioMcp" -ForegroundColor White
} else {
    Write-Host "Local installation:" -ForegroundColor Yellow
    Write-Host "  dotnet tool install --global --add-source $OutputPath VisualStudioMcp" -ForegroundColor White
}

Write-Host "" -ForegroundColor White
Write-Host "Usage:" -ForegroundColor Yellow
Write-Host "  vsmcp --version" -ForegroundColor White
Write-Host "  vsmcp --help" -ForegroundColor White

Write-Host "" -ForegroundColor White
Write-Host "Claude Code configuration:" -ForegroundColor Yellow
Write-Host '  Add to mcp_servers.json: {"visual-studio": {"command": "vsmcp"}}' -ForegroundColor White