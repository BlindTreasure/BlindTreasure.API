#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Local script để chạy unit tests và generate Excel report
.DESCRIPTION
    Script này sẽ chạy unit tests, generate coverage report, và tạo Excel file
    giống như trong GitHub Actions nhưng ở local environment
#>

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "./TestResults",
    [string]$ReportsDir = "./Reports", 
    [switch]$SkipBuild = $false,
    [switch]$OpenReport = $true,
    [string]$ProjectName = "BlindTreasure API",
    [string]$Version = "local-dev"
)

# Colors for output
$ErrorColor = "Red"
$SuccessColor = "Green"
$InfoColor = "Cyan"
$WarningColor = "Yellow"

function Write-ColorMessage {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

function Test-Prerequisites {
    Write-ColorMessage "🔍 Checking prerequisites..." $InfoColor
    
    # Check .NET SDK
    try {
        $dotnetVersion = dotnet --version
        Write-ColorMessage "✅ .NET SDK version: $dotnetVersion" $SuccessColor
    }
    catch {
        Write-ColorMessage "❌ .NET SDK not found! Please install .NET SDK 8.0 or later." $ErrorColor
        exit 1
    }
    
    # Check Python (for Excel generation)
    try {
        $pythonVersion = python --version 2>$null
        if ($pythonVersion) {
            Write-ColorMessage "✅ Python version: $pythonVersion" $SuccessColor
        } else {
            Write-ColorMessage "⚠️ Python not found. Excel report will not be generated." $WarningColor
            return $false
        }
    }
    catch {
        Write-ColorMessage "⚠️ Python not found. Excel report will not be generated." $WarningColor
        return $false
    }
    
    return $true
}

function Install-PythonDependencies {
    Write-ColorMessage "📦 Installing Python dependencies..." $InfoColor
    
    $packages = @("pandas", "openpyxl", "lxml", "beautifulsoup4")
    
    foreach ($package in $packages) {
        try {
            Write-ColorMessage "Installing $package..." $InfoColor
            pip install $package --quiet
            Write-ColorMessage "✅ $package installed" $SuccessColor
        }
        catch {
            Write-ColorMessage "❌ Failed to install $package" $ErrorColor
            return $false
        }
    }
    
    return $true
}

function Clean-Directories {
    Write-ColorMessage "🧹 Cleaning directories..." $InfoColor
    
    if (Test-Path $OutputDir) {
        Remove-Item -Path $OutputDir -Recurse -Force
        Write-ColorMessage "✅ Cleaned $OutputDir" $SuccessColor
    }
    
    if (Test-Path $ReportsDir) {
        Remove-Item -Path $ReportsDir -Recurse -Force  
        Write-ColorMessage "✅ Cleaned $ReportsDir" $SuccessColor
    }
    
    # Create directories
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    New-Item -ItemType Directory -Path $ReportsDir -Force | Out-Null
}

function Build-Solution {
    if ($SkipBuild) {
        Write-ColorMessage "⏭️ Skipping build step..." $WarningColor
        return $true
    }
    
    Write-ColorMessage "🏗️ Building solution..." $InfoColor
    
    try {
        # Restore packages
        Write-ColorMessage "📦 Restoring NuGet packages..." $InfoColor
        dotnet restore "./BlindTreasure.API.sln"
        
        if ($LASTEXITCODE -ne 0) {
            Write-ColorMessage "❌ Package restore failed!" $ErrorColor
            return $false
        }
        
        # Build solution
        Write-ColorMessage "🔨 Building solution..." $InfoColor
        dotnet build "./BlindTreasure.API.sln" --configuration $Configuration --no-restore
        
        if ($LASTEXITCODE -ne 0) {
            Write-ColorMessage "❌ Build failed!" $ErrorColor
            return $false
        }
        
        Write-ColorMessage "✅ Build completed successfully!" $SuccessColor
        return $true
    }
    catch {
        Write-ColorMessage "❌ Build process failed: $_" $ErrorColor
        return $false
    }
}

function Run-Tests {
    Write-ColorMessage "🧪 Running unit tests..." $InfoColor
    
    try {
        $testProject = "./BlindTreaure.UnitTest/BlindTreaure.UnitTest.csproj"
        
        if (-not (Test-Path $testProject)) {
            Write-ColorMessage "❌ Test project not found: $testProject" $ErrorColor
            return $false
        }
        
        # Run tests with coverage
        dotnet test $testProject `
            --configuration $Configuration `
            --no-build `
            --verbosity normal `
            --logger "trx;LogFileName=test-results.trx" `
            --logger "console;verbosity=detailed" `
            --logger "junit;LogFileName=junit-results.xml" `
            --collect:"XPlat Code Coverage" `
            --results-directory $OutputDir `
            --settings ./coverlet.runsettings
        
        if ($LASTEXITCODE -ne 0) {
            Write-ColorMessage "⚠️ Some tests failed, but continuing with report generation..." $WarningColor
        } else {
            Write-ColorMessage "✅ All tests passed!" $SuccessColor
        }
        
        return $true
    }
    catch {
        Write-ColorMessage "❌ Test execution failed: $_" $ErrorColor
        return $false
    }
}

function Generate-CoverageReport {
    Write-ColorMessage "📊 Generating coverage report..." $InfoColor
    
    try {
        # Install ReportGenerator if not already installed
        $reportGenerator = Get-Command "reportgenerator" -ErrorAction SilentlyContinue
        if (-not $reportGenerator) {
            Write-ColorMessage "📦 Installing ReportGenerator..." $InfoColor
            dotnet tool install -g dotnet-reportgenerator-globaltool
        }
        
        # Generate coverage report
        $coverageFiles = Get-ChildItem -Path $OutputDir -Filter "coverage.opencover.xml" -Recurse
        
        if ($coverageFiles.Count -eq 0) {
            Write-ColorMessage "⚠️ No coverage files found" $WarningColor
            return $false
        }
        
        $coverageReports = ($coverageFiles | ForEach-Object { $_.FullName }) -join ";"
        
        reportgenerator `
            "-reports:$coverageReports" `
            "-targetdir:$ReportsDir/coverage" `
            "-reporttypes:Html;Cobertura;JsonSummary;Badges" `
            "-title:$ProjectName Coverage Report" `
            "-tag:local-$(Get-Date -Format 'yyyy-MM-dd-HH-mm')"
        
        Write-ColorMessage "✅ Coverage report generated!" $SuccessColor
        return $true
    }
    catch {
        Write-ColorMessage "❌ Coverage report generation failed: $_" $ErrorColor
        return $false
    }
}

function Generate-ExcelReport {
    param([bool]$HasPython)
    
    if (-not $HasPython) {
        Write-ColorMessage "⏭️ Skipping Excel generation (Python not available)" $WarningColor
        return $false
    }
    
    Write-ColorMessage "📊 Generating Excel report..." $InfoColor
    
    try {
        $scriptPath = "./.github/scripts/convert-tests-to-excel.py"
        
        if (-not (Test-Path $scriptPath)) {
            Write-ColorMessage "❌ Excel generation script not found: $scriptPath" $ErrorColor
            return $false
        }
        
        $currentBranch = git branch --show-current 2>$null
        if (-not $currentBranch) { $currentBranch = "local" }
        
        $buildNumber = "local-$(Get-Date -Format 'yyyyMMdd-HHmm')"
        $outputFile = "$ProjectName-TestReport-$buildNumber.xlsx"
        
        python $scriptPath `
            --test-results-dir $OutputDir `
            --reports-dir $ReportsDir `
            --output-file $outputFile `
            --project-name $ProjectName `
            --version $Version `
            --branch $currentBranch `
--build-number $buildNumber
       
       if ($LASTEXITCODE -eq 0) {
           Write-ColorMessage "✅ Excel report generated: $outputFile" $SuccessColor
           return $outputFile
       } else {
           Write-ColorMessage "❌ Excel generation failed!" $ErrorColor
           return $false
       }
   }
   catch {
       Write-ColorMessage "❌ Excel generation failed: $_" $ErrorColor
       return $false
   }
}

function Show-Summary {
   param([string]$ExcelFile, [bool]$HasCoverage)
   
   Write-ColorMessage "`n" + "="*60 $InfoColor
   Write-ColorMessage "📊 TEST EXECUTION SUMMARY" $InfoColor
   Write-ColorMessage "="*60 $InfoColor
   
   # Count test results
   $trxFiles = Get-ChildItem -Path $OutputDir -Filter "*.trx" -Recurse
   
   if ($trxFiles.Count -gt 0) {
       try {
           $trxContent = Get-Content $trxFiles[0].FullName -Raw
           $xml = [xml]$trxContent
           
           $totalTests = ($xml.TestRun.ResultSummary.Counters.total -as [int])
           $passedTests = ($xml.TestRun.ResultSummary.Counters.passed -as [int])
           $failedTests = ($xml.TestRun.ResultSummary.Counters.failed -as [int])
           $skippedTests = ($xml.TestRun.ResultSummary.Counters.inconclusive -as [int])
           
           if ($totalTests -gt 0) {
               $passRate = [math]::Round(($passedTests / $totalTests) * 100, 2)
               
               Write-ColorMessage "🧪 Total Tests: $totalTests" $InfoColor
               Write-ColorMessage "✅ Passed: $passedTests" $SuccessColor
               Write-ColorMessage "❌ Failed: $failedTests" $(if ($failedTests -gt 0) { $ErrorColor } else { $SuccessColor })
               Write-ColorMessage "⏭️ Skipped: $skippedTests" $WarningColor
               Write-ColorMessage "📈 Pass Rate: $passRate%" $(if ($passRate -ge 80) { $SuccessColor } elseif ($passRate -ge 60) { $WarningColor } else { $ErrorColor })
           }
       }
       catch {
           Write-ColorMessage "⚠️ Could not parse test results" $WarningColor
       }
   }
   
   Write-ColorMessage "`n📁 Generated Files:" $InfoColor
   
   if (Test-Path "$OutputDir") {
       Write-ColorMessage "  📄 Test Results: $OutputDir" $InfoColor
   }
   
   if ($HasCoverage -and (Test-Path "$ReportsDir/coverage")) {
       Write-ColorMessage "  📊 Coverage Report: $ReportsDir/coverage/index.html" $InfoColor
   }
   
   if ($ExcelFile -and (Test-Path $ExcelFile)) {
       Write-ColorMessage "  📋 Excel Report: $ExcelFile" $SuccessColor
   }
   
   Write-ColorMessage "="*60 $InfoColor
}

function Open-Reports {
   param([string]$ExcelFile, [bool]$HasCoverage)
   
   if (-not $OpenReport) {
       return
   }
   
   Write-ColorMessage "🚀 Opening reports..." $InfoColor
   
   # Open Excel report
   if ($ExcelFile -and (Test-Path $ExcelFile)) {
       try {
           Start-Process $ExcelFile
           Write-ColorMessage "✅ Opened Excel report" $SuccessColor
       }
       catch {
           Write-ColorMessage "⚠️ Could not open Excel report automatically" $WarningColor
       }
   }
   
   # Open coverage report
   if ($HasCoverage -and (Test-Path "$ReportsDir/coverage/index.html")) {
       try {
           Start-Process "$ReportsDir/coverage/index.html"
           Write-ColorMessage "✅ Opened coverage report" $SuccessColor
       }
       catch {
           Write-ColorMessage "⚠️ Could not open coverage report automatically" $WarningColor
       }
   }
}

# Main execution
function Main {
   Write-ColorMessage "🚀 Starting BlindTreasure Unit Test Execution" $InfoColor
   Write-ColorMessage "Configuration: $Configuration" $InfoColor
   Write-ColorMessage "Output Directory: $OutputDir" $InfoColor
   Write-ColorMessage "Reports Directory: $ReportsDir" $InfoColor
   
   # Check prerequisites
   $hasPython = Test-Prerequisites
   
   # Install Python dependencies if available
   if ($hasPython) {
       $hasPython = Install-PythonDependencies
   }
   
   # Clean directories
   Clean-Directories
   
   # Build solution
   if (-not (Build-Solution)) {
       Write-ColorMessage "💥 Build failed! Exiting..." $ErrorColor
       exit 1
   }
   
   # Run tests
   if (-not (Run-Tests)) {
       Write-ColorMessage "💥 Test execution failed! Exiting..." $ErrorColor
       exit 1
   }
   
   # Generate coverage report
   $hasCoverage = Generate-CoverageReport
   
   # Generate Excel report
   $excelFile = $false
   if ($hasPython) {
       $excelFile = Generate-ExcelReport -HasPython $hasPython
   }
   
   # Show summary
   Show-Summary -ExcelFile $excelFile -HasCoverage $hasCoverage
   
   # Open reports
   Open-Reports -ExcelFile $excelFile -HasCoverage $hasCoverage
   
   Write-ColorMessage "`n🎉 Test execution completed!" $SuccessColor
}

# Run main function
Main