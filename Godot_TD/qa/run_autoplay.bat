@echo off
setlocal EnableDelayedExpansion

:: ═══════════════════════════════════════════════════════════════
:: AutoPlayer Runner — Automated gameplay testing
:: Runs all 8 strategies, collects reports, summarizes results.
:: Usage: run_autoplay.bat [strategy_name]
::   No args = run all 8 default strategies
::   With arg = run only that strategy
:: ═══════════════════════════════════════════════════════════════

set GODOT="C:\Program Files (x86)\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
set PROJECT_DIR=%~dp0..

echo ══════════════════════════════════════════════
echo  AutoPlayer Runner
echo ══════════════════════════════════════════════

:: Step 1: Build
echo.
echo [1/3] Building project...
cd /d "%PROJECT_DIR%"
dotnet build --nologo -v q 2>&1
if %errorlevel% neq 0 (
    echo [FAIL] Build failed.
    exit /b 1
)
echo [OK] Build succeeded.

:: Step 2: Run AutoPlayer
echo.
echo [2/3] Running AutoPlayer...

if "%~1"=="" (
    echo Running all 8 default strategies...
    %GODOT% --path "%PROJECT_DIR%" --autoplay 2>&1
) else (
    echo Running strategy: %~1
    if exist "%PROJECT_DIR%\qa\configs\%~1.json" (
        %GODOT% --path "%PROJECT_DIR%" --autoplay --config=qa/configs/%~1.json 2>&1
    ) else (
        echo [WARN] Config not found: qa/configs/%~1.json
        echo        Running default batch instead.
        %GODOT% --path "%PROJECT_DIR%" --autoplay 2>&1
    )
)

:: Step 3: Summarize reports
echo.
echo [3/3] Summarizing results...
echo.

set REPORT_DIR=%PROJECT_DIR%\autoplay-reports
if not exist "%REPORT_DIR%" (
    echo [WARN] No autoplay-reports directory found.
    exit /b 1
)

echo ══════════════════════════════════════════════
echo  AutoPlay Results
echo ══════════════════════════════════════════════

set TOTAL=0
set DEFEATS=0
set VICTORIES=0
set ERRORS_FOUND=0

for /r "%REPORT_DIR%" %%f in (report.json) do (
    set /a TOTAL+=1
    findstr /c:"\"result\": \"defeat\"" "%%f" >nul 2>&1 && set /a DEFEATS+=1
    findstr /c:"\"result\": \"victory\"" "%%f" >nul 2>&1 && set /a VICTORIES+=1
    findstr /c:"\"errors\": []" "%%f" >nul 2>&1 || set /a ERRORS_FOUND+=1

    for /f "tokens=2 delims=:," %%w in ('findstr /c:"waveReached" "%%f"') do (
        for /f "tokens=2 delims=:," %%s in ('findstr /c:"strategy" "%%f"') do (
            echo   %%~s  wave %%w
        )
    )
)

echo.
echo  Total runs: %TOTAL%
echo  Defeats:    %DEFEATS%
echo  Victories:  %VICTORIES%
echo  With errors: %ERRORS_FOUND%
echo ══════════════════════════════════════════════

if %ERRORS_FOUND% gtr 0 (
    echo [WARN] Some runs had errors. Check autoplay-reports/ for details.
    exit /b 1
)
exit /b 0
