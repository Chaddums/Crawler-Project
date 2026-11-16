@echo off
setlocal
:: Is each of BIT's frames moving? (Scripts/Testing/Suites/FrameLookSuite.cs): BIT on
:: foot, in the Strider and in the Titan, standing, walking, shooting and aiming,
:: five frames of each close on BIT to Godot_TD\test-reports\frame_look\, and a
:: line each in test-reports\frame_look.log saying which clip played and how far
:: the bones moved. About 40 seconds. Usage: qa\frame_look.bat

set PROJECT_DIR=%~dp0..
set GODOT=
for %%G in (
  "%USERPROFILE%\Godot\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "%USERPROFILE%\Godot\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "%USERPROFILE%\Downloads\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "C:\Program Files (x86)\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
) do if not defined GODOT if exist %%G set GODOT=%%G
if not defined GODOT (
  echo Godot 4.6.1 mono not found. Set GODOT in qa\frame_look.bat.
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
set SANDBOX_NAME=framelook
set FRAME_LOOK=%CD%\test-reports\frame_look
echo [2/2] Looking. Frames: %FRAME_LOOK%
%GODOT% --path . --resolution 1920x1080 -- --test-harness --suite=framelook --request-id=framelook > test-reports\frame_look.log 2>&1
echo Done. Log: test-reports\frame_look.log
