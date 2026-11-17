@echo off
setlocal
:: Pictures of the Company Store, the ending, a second campaign's Command Center
:: and map, the boss screens, and each boss on the field beside towers
:: (Scripts/Testing/Suites/EndLookSuite.cs), to Godot_TD\test-reports\end_look\.
:: END_LOOK_ONLY=store,ending,silencer,overloader picks parts. About two minutes.
:: Usage: qa\end_look.bat

set PROJECT_DIR=%~dp0..
set GODOT=
for %%G in (
  "%USERPROFILE%\Godot\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "%USERPROFILE%\Godot\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "%USERPROFILE%\Downloads\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "C:\Program Files (x86)\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
) do if not defined GODOT if exist %%G set GODOT=%%G
if not defined GODOT (
  echo Godot 4.6.1 mono not found. Set GODOT in qa\end_look.bat.
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
set SANDBOX_NAME=endlook
set END_LOOK=%CD%\test-reports\end_look
echo [2/2] Looking. Pictures: %END_LOOK%
%GODOT% --path . --resolution 1920x1080 -- --test-harness --suite=endlook --request-id=endlook > test-reports\end_look.log 2>&1
echo Done. Log: test-reports\end_look.log
