@echo off
setlocal
:: The talented bot in a window, to watch (Scripts/Debug/BitAce.cs): Greedy
:: building, Spire and BIT upgrades, and BIT walked by the skilled pilot, on
:: Relay Station Alpha at 2x to wave 18. Its saves go to a sandbox, never the
:: player's own. Usage: qa\talented_run.bat

set PROJECT_DIR=%~dp0..
set GODOT=
for %%G in (
  "%USERPROFILE%\Godot\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "%USERPROFILE%\Godot\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "%USERPROFILE%\Downloads\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "C:\Program Files (x86)\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
) do if not defined GODOT if exist %%G set GODOT=%%G
if not defined GODOT (
  echo Godot 4.6.1 mono not found. Set GODOT in qa\talented_run.bat.
  pause
  exit /b 1
)

cd /d "%PROJECT_DIR%"
echo [1/2] Building...
dotnet build --nologo -v q
if %errorlevel% neq 0 (
  echo Build failed.
  pause
  exit /b 1
)

if not exist test-reports mkdir test-reports
set SANDBOX_NAME=talentedrun
set WAVE_SEED=1
echo [2/2] Playing. Log: test-reports\talented_run.log
%GODOT% --path . --resolution 1920x1080 -- --autoplay --config qa/configs/talented_watch.json > test-reports\talented_run.log 2>&1
echo Done. Log: test-reports\talented_run.log
