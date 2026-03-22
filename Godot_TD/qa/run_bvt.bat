@echo off
setlocal EnableDelayedExpansion

:: ═══════════════════════════════════════════════════════════════
:: BVT Runner — Build Verification Test
:: Builds the project, runs BVT suite, parses results.
:: Exit code 0 = all pass, 1 = failures found.
:: ═══════════════════════════════════════════════════════════════

set GODOT="C:\Program Files (x86)\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
set PROJECT_DIR=%~dp0..
set REQUEST_ID=bvt_%date:~-4%%date:~4,2%%date:~7,2%_%time:~0,2%%time:~3,2%%time:~6,2%
set REQUEST_ID=%REQUEST_ID: =0%

echo ══════════════════════════════════════════════
echo  BVT Runner
echo ══════════════════════════════════════════════

:: Step 1: Build
echo.
echo [1/3] Building project...
cd /d "%PROJECT_DIR%"
dotnet build --nologo -v q 2>&1
if %errorlevel% neq 0 (
    echo [FAIL] Build failed. Fix compilation errors first.
    exit /b 1
)
echo [OK] Build succeeded.

:: Step 2: Run BVT suite
echo.
echo [2/3] Running BVT suite...
%GODOT% --path "%PROJECT_DIR%" --headless -- --test-harness --suite=bvt --request-id=%REQUEST_ID% 2>&1

:: Step 3: Parse results
echo.
echo [3/3] Parsing results...
set RESULTS_FILE=%PROJECT_DIR%\test-reports\results\%REQUEST_ID%.json

if not exist "%RESULTS_FILE%" (
    echo [WARN] Results file not found: %RESULTS_FILE%
    echo        The test harness may not have written output.
    echo        Check the Godot console output above for errors.
    exit /b 1
)

:: Quick parse — count passed/failed lines
set TOTAL=0
set PASSED=0
set FAILED=0
for /f "tokens=*" %%a in ('findstr /c:"\"passed\": true" "%RESULTS_FILE%"') do set /a PASSED+=1
for /f "tokens=*" %%a in ('findstr /c:"\"passed\": false" "%RESULTS_FILE%"') do set /a FAILED+=1
set /a TOTAL=PASSED+FAILED

echo.
echo ══════════════════════════════════════════════
echo  Results: %PASSED% passed, %FAILED% failed / %TOTAL% total
echo  Report:  %RESULTS_FILE%
echo ══════════════════════════════════════════════

if %FAILED% gtr 0 (
    echo.
    echo FAILURES:
    findstr /c:"\"passed\": false" "%RESULTS_FILE%"
    echo.
    exit /b 1
)

echo [ALL PASS]
exit /b 0
