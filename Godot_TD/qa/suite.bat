@echo off
setlocal
:: Any test suite in a window, for the ones that take pictures or need this
:: machine's renderer and models. The suite's name is the first line of
:: Godot_TD\test-reports\suite.txt (relics-sheets, framelook, mech, ...); the log goes to
:: test-reports\suite_<name>.log. Usage: qa\suite.bat

set PROJECT_DIR=%~dp0..
set GODOT=
for %%G in (
  "%USERPROFILE%\Godot\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "%USERPROFILE%\Godot\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "%USERPROFILE%\Downloads\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "C:\Program Files (x86)\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
) do if not defined GODOT if exist %%G set GODOT=%%G
if not defined GODOT (
  echo Godot 4.6.1 mono not found. Set GODOT in qa\suite.bat.
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

if not exist test-reports\suite.txt (
  echo Name the suite in test-reports\suite.txt
  pause
  exit /b 1
)
set /p SUITE=<test-reports\suite.txt
set SANDBOX_NAME=suite
set FRAME_LOOK=%CD%\test-reports\frame_look
echo [2/2] Running %SUITE%. Log: test-reports\suite_%SUITE%.log
%GODOT% --path . --resolution 1920x1080 -- --test-harness --suite=%SUITE% --request-id=%SUITE% > test-reports\suite_%SUITE%.log 2>&1
echo Done.
