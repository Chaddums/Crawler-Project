@echo off
setlocal
:: A bot run in a window, to watch and to shoot (Scripts/Debug/VisualWalk.cs):
:: a Bruteforge bot plays to wave 22 at 2x on Relay Station Alpha, through the
:: First Cache, the themed blocks and the W20 safe room, then the debrief.
:: Numbered screenshots go to Godot_TD\test-reports\visual\. Its saves go to a
:: sandbox, never the player's own. Usage: qa\visual_run.bat [end wave]

set PROJECT_DIR=%~dp0..
set GODOT=
for %%G in (
  "%USERPROFILE%\Godot\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "%USERPROFILE%\Godot\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "%USERPROFILE%\Downloads\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "C:\Program Files (x86)\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
) do if not defined GODOT if exist %%G set GODOT=%%G
if not defined GODOT (
  echo Godot 4.6.1 mono not found. Set GODOT in qa\visual_run.bat.
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
set VISUAL_RUN=%CD%\test-reports\visual
set VISUAL_END=%1
if "%VISUAL_END%"=="" set VISUAL_END=22
set WAVE_SEED=1
echo [2/2] Running to wave %VISUAL_END%. Screenshots: %VISUAL_RUN%
%GODOT% --path . --resolution 1920x1080 -- --autoplay --config qa/configs/visual_bru.json > test-reports\visual_run.log 2>&1
echo Done. Log: test-reports\visual_run.log
