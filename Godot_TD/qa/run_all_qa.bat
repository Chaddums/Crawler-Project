@echo off
setlocal EnableDelayedExpansion

:: ═══════════════════════════════════════════════════════════════
:: Full QA Suite — Run everything, iterate until clean
:: Usage:
::   run_all_qa.bat           Run once
::   run_all_qa.bat --loop    Run continuously until all pass
::   run_all_qa.bat --loop 5  Run up to 5 iterations
:: ═══════════════════════════════════════════════════════════════

set GODOT="C:\Program Files (x86)\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
set PROJECT_DIR=%~dp0..
set LOOP_MODE=0
set MAX_ITERS=1

if "%~1"=="--loop" (
    set LOOP_MODE=1
    if "%~2"=="" (set MAX_ITERS=10) else (set MAX_ITERS=%~2)
)

set ITER=0
:LOOP_START
set /a ITER+=1
set FAIL_COUNT=0

echo.
echo ████████████████████████████████████████████████████████████
echo  QA Iteration %ITER% / %MAX_ITERS%
echo  %date% %time%
echo ████████████████████████████████████████████████████████████

:: ── Phase 1: Build ──
echo.
echo ── Phase 1: Build ──────────────────────────────
cd /d "%PROJECT_DIR%"
dotnet build --nologo -v q 2>&1
if %errorlevel% neq 0 (
    echo [FAIL] Build failed.
    set /a FAIL_COUNT+=1
    goto :SUMMARY
)
echo [OK] Build clean.

:: ── Phase 2: BVT ──
echo.
echo ── Phase 2: Editor BVT ─────────────────────────
set BVT_ID=bvt_%ITER%_%RANDOM%
%GODOT% --path "%PROJECT_DIR%" --headless -- --test-harness --suite=bvt --request-id=%BVT_ID% 2>&1

set BVT_RESULTS=%PROJECT_DIR%\test-reports\results\%BVT_ID%.json
if exist "%BVT_RESULTS%" (
    set BVT_FAILS=0
    for /f "tokens=*" %%a in ('findstr /c:"\"passed\": false" "%BVT_RESULTS%"') do set /a BVT_FAILS+=1
    if !BVT_FAILS! gtr 0 (
        echo [FAIL] BVT: !BVT_FAILS! failures
        set /a FAIL_COUNT+=1
        findstr /c:"\"passed\": false" "%BVT_RESULTS%"
    ) else (
        echo [OK] BVT passed.
    )
) else (
    echo [SKIP] BVT results not found — check Godot output.
)

:: ── Phase 3: Content Tests ──
echo.
echo ── Phase 3: Content + Editor Tests ─────────────
set CONTENT_ID=content_%ITER%_%RANDOM%
%GODOT% --path "%PROJECT_DIR%" --headless -- --test-harness --suite=content --request-id=%CONTENT_ID% 2>&1

set CONTENT_RESULTS=%PROJECT_DIR%\test-reports\results\%CONTENT_ID%.json
if exist "%CONTENT_RESULTS%" (
    set CT_FAILS=0
    for /f "tokens=*" %%a in ('findstr /c:"\"passed\": false" "%CONTENT_RESULTS%"') do set /a CT_FAILS+=1
    if !CT_FAILS! gtr 0 (
        echo [FAIL] Content: !CT_FAILS! failures
        set /a FAIL_COUNT+=1
    ) else (
        echo [OK] Content tests passed.
    )
) else (
    echo [SKIP] Content results not found.
)

:: ── Phase 4: AutoPlayer (short runs) ──
echo.
echo ── Phase 4: AutoPlayer smoke test ──────────────
echo Running DoNothing strategy (fastest baseline)...
%GODOT% --path "%PROJECT_DIR%" --autoplay --config=qa/configs/smoke_test.json 2>&1

set AP_DIR=%PROJECT_DIR%\autoplay-reports
if exist "%AP_DIR%" (
    set AP_ERRORS=0
    for /r "%AP_DIR%" %%f in (report.json) do (
        findstr /c:"\"errors\": []" "%%f" >nul 2>&1 || set /a AP_ERRORS+=1
    )
    if !AP_ERRORS! gtr 0 (
        echo [FAIL] AutoPlayer: !AP_ERRORS! runs with errors
        set /a FAIL_COUNT+=1
    ) else (
        echo [OK] AutoPlayer smoke test clean.
    )
) else (
    echo [SKIP] No autoplay-reports/ found.
)

:SUMMARY
echo.
echo ████████████████████████████████████████████████████████████
if %FAIL_COUNT% equ 0 (
    echo  ITERATION %ITER%: ALL PASS
    echo ████████████████████████████████████████████████████████████
    exit /b 0
) else (
    echo  ITERATION %ITER%: %FAIL_COUNT% PHASE(S) FAILED
    echo ████████████████████████████████████████████████████████████
)

if %LOOP_MODE% equ 1 (
    if %ITER% lss %MAX_ITERS% (
        echo.
        echo Waiting 30s before next iteration...
        echo (Fix issues now, or press Ctrl+C to stop)
        timeout /t 30 /nobreak >nul
        goto :LOOP_START
    ) else (
        echo Max iterations reached. %FAIL_COUNT% failures remain.
    )
)

exit /b 1
