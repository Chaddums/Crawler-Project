@echo off
setlocal
:: The screens between runs as a save sees them (Scripts/Debug/HubLook.cs): for
:: every folder of saves in Godot_TD\test-reports\hub_saves\ the game opens the
:: Command Center, the territory map, the boss screen when a boss fight is next,
:: and the perk tree, and shoots each to Godot_TD\test-reports\hub_look\ under
:: the folder's name. No run is played; the saves are copied to a sandbox, never
:: the player's own. Usage: qa\hub_look.bat

set PROJECT_DIR=%~dp0..
set GODOT=
for %%G in (
  "%USERPROFILE%\Godot\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "%USERPROFILE%\Godot\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "%USERPROFILE%\Downloads\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "C:\Program Files (x86)\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
) do if not defined GODOT if exist %%G set GODOT=%%G
if not defined GODOT (
  echo Godot 4.6.1 mono not found. Set GODOT in qa\hub_look.bat.
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

if not exist test-reports\hub_saves (
  echo No saves to look at: put folders of saves in test-reports\hub_saves\
  pause
  exit /b 1
)
if not exist test-reports\hub_look mkdir test-reports\hub_look
set HUB_LOOK=%CD%\test-reports\hub_look
set SANDBOX_NAME=hublook
echo [2/2] Screens: %HUB_LOOK%
echo. > test-reports\hub_look.log
for /d %%S in (test-reports\hub_saves\*) do (
  echo   %%~nxS
  set SANDBOX_SEED=%CD%\%%S
  set HUB_LOOK_NAME=%%~nxS
  %GODOT% --path . --resolution 1920x1080 -- --autoplay --config qa/configs/campaign/novice.json >> test-reports\hub_look.log 2>&1
)
echo Done. Log: test-reports\hub_look.log
