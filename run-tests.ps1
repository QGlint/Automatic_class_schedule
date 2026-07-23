<#
.SYNOPSIS
    Builds the WinUI project and runs all automated tests.
.DESCRIPTION
    1. Build main project (WinUI)
    2. Build test project (xUnit)
    3. Run all tests
    4. Launch app as smoke test (5 seconds)
.EXAMPLE
    .\run-tests.ps1
#>

$SolutionDir = $PSScriptRoot

$MainCsproj = Join-Path $SolutionDir "Automatic_class_schedule\Automatic_class_schedule.csproj"
$TestCsproj  = Join-Path $SolutionDir "Automatic_class_schedule.Tests\Automatic_class_schedule.Tests.csproj"
$ExePath     = Join-Path $SolutionDir "Automatic_class_schedule\bin\Debug\net10.0-windows10.0.26100.0\win-x64\Automatic_class_schedule.exe"

$Pass = 0
$Fail = 0

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  自动排课系统 - 测试运行脚本" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build main project
Write-Host "▶ [1/3] Building main project..." -ForegroundColor Yellow
$result = dotnet build $MainCsproj -c Debug -nologo 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    Write-Host "  BUILD FAILED" -ForegroundColor Red
    Write-Host $result
    $Fail++
} else {
    Write-Host "  ✓ Main project build OK" -ForegroundColor Green
    $Pass++
}

Write-Host ""

# Step 2: Build test project
Write-Host "▶ [2/3] Building test project..." -ForegroundColor Yellow
$result = dotnet build $TestCsproj -c Debug -nologo 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    Write-Host "  BUILD FAILED" -ForegroundColor Red
    Write-Host $result
    $Fail++
} else {
    Write-Host "  ✓ Test project build OK" -ForegroundColor Green
    $Pass++
}

Write-Host ""

# Step 3: Run tests
Write-Host "▶ [3/3] Running tests..." -ForegroundColor Yellow
dotnet test $TestCsproj -c Debug --no-build 2>&1 | ForEach-Object {
    if ($_ -match "^  失败") {
        Write-Host $_ -ForegroundColor Red
    } elseif ($_ -match "^  已通过") {
        Write-Host $_ -ForegroundColor Green
    } elseif ($_ -match "\[FAIL\]") {
        Write-Host $_ -ForegroundColor Red
    } else {
        Write-Host $_
    }
}

if ($LASTEXITCODE -eq 0) {
    $Pass++
} else {
    $Fail++
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan

# Step 4: Smoke test (launch app)
if (Test-Path $ExePath) {
    Write-Host "▶ Smoke test: launching app..." -ForegroundColor Yellow
    try {
        $proc = Start-Process -FilePath $ExePath -NoNewWindow -PassThru
        Start-Sleep -Seconds 5
        if (-not $proc.HasExited) {
            Write-Host "  ✓ App launches and runs (5s)" -ForegroundColor Green
            $proc.Kill()
            $Pass++
        } else {
            Write-Host "  ✗ App exited prematurely: $($proc.ExitCode)" -ForegroundColor Red
            $Fail++
        }
    } catch {
        Write-Host "  ✗ Failed to launch: $_" -ForegroundColor Red
        $Fail++
    }
}

Write-Host ""
Write-Host "Result: $Pass passed, $Fail failed" -ForegroundColor $(if ($Fail -eq 0) { "Green" } else { "Red" })
Write-Host "============================================" -ForegroundColor Cyan
exit $Fail
