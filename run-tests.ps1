<#
.SYNOPSIS
    Full CI pipeline: build, unit test, runtime verification, smoke test.
.DESCRIPTION
    1. Build main project (WinUI)
    2. Build test project (xUnit)
    3. Run all unit tests
    4. Build runtime verification tool (WinUI.Verify)
    5. Launch app + runtime verification
    6. Smoke test (5 seconds)
.EXAMPLE
    .\run-tests.ps1
#>

$SolutionDir = $PSScriptRoot

$MainCsproj  = Join-Path $SolutionDir "Automatic_class_schedule\Automatic_class_schedule.csproj"
$TestCsproj  = Join-Path $SolutionDir "Automatic_class_schedule.Tests\Automatic_class_schedule.Tests.csproj"
$VerifyCsproj = Join-Path $SolutionDir "tests\runtime\WinUIVerify.csproj"
$VerifyExe   = Join-Path $SolutionDir "tests\runtime\bin\Debug\net10.0-windows10.0.26100.0\WinUIVerify.exe"
$ExePath     = Join-Path $SolutionDir "Automatic_class_schedule\bin\Debug\net10.0-windows10.0.26100.0\win-x64\Automatic_class_schedule.exe"

$Pass = 0
$Fail = 0

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  CI Pipeline: Build → Test → Verify" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build main project
Write-Host "▶ [1/6] Building main project..." -ForegroundColor Yellow
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
Write-Host "▶ [2/6] Building test project..." -ForegroundColor Yellow
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

# Step 3: Run unit tests
Write-Host "▶ [3/6] Running unit tests..." -ForegroundColor Yellow
dotnet test $TestCsproj -c Debug --no-build 2>&1 | ForEach-Object {
    if ($_ -match "^  失败|\[FAIL\]") {
        Write-Host $_ -ForegroundColor Red
    } elseif ($_ -match "^  已通过|\[PASS\]") {
        Write-Host $_ -ForegroundColor Green
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

# Step 4: Build runtime verification tool
Write-Host "▶ [4/6] Building runtime verification tool..." -ForegroundColor Yellow
$result = dotnet build $VerifyCsproj -c Debug -nologo 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    Write-Host "  BUILD FAILED" -ForegroundColor Red
    Write-Host $result
    $Fail++
} else {
    Write-Host "  ✓ WinUI.Verify build OK" -ForegroundColor Green
    $Pass++
}

Write-Host ""

# Step 5: Runtime verification (WinUI.Verify)
Write-Host "▶ [5/6] Runtime verification..." -ForegroundColor Yellow
if (Test-Path $VerifyExe) {
    & $VerifyExe
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Runtime verification PASS" -ForegroundColor Green
        $Pass++
    } else {
        Write-Host "  ✗ Runtime verification FAIL" -ForegroundColor Red
        $Fail++
    }
} else {
    Write-Host "  ⚠ WinUI.Verify.exe not found, skipping" -ForegroundColor Yellow
}

Write-Host ""

# Step 6: Smoke test
Write-Host "▶ [6/6] Smoke test..." -ForegroundColor Yellow
if (Test-Path $ExePath) {
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
} else {
    Write-Host "  ⚠ App exe not found, skipping smoke test" -ForegroundColor Yellow
}

Write-Host ""

# Summary
if ($Fail -eq 0) {
    Write-Host "============================================" -ForegroundColor Green
    Write-Host "  RESULT: PASS ($Pass/$($Pass+$Fail))" -ForegroundColor Green
    Write-Host "============================================" -ForegroundColor Green
} else {
    Write-Host "============================================" -ForegroundColor Red
    Write-Host "  RESULT: FAIL ($Pass passed, $Fail failed)" -ForegroundColor Red
    Write-Host "============================================" -ForegroundColor Red
}

exit $Fail
