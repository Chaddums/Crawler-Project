@echo off
setlocal
:: One campaign run in a window, from the save in Godot_TD\test-reports\campaign_save\
:: (Scripts/Debug/CampaignPilot.cs): the bot takes what the Command Center offers
:: next (a site, or the boss fight with its best suit), plays it at 4x, and the
:: screen the run ends on and its debrief are shot to
:: Godot_TD\test-reports\campaign_run\. The save is copied to a sandbox and is
:: not changed. Usage: qa\campaign_run.bat

set PROJECT_DIR=%~dp0..
set GODOT=
for %%G in (
  "%USERPROFILE%\Godot\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "%USERPROFILE%\Godot\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "%USERPROFILE%\Downloads\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
  "C:\Program Files (x86)\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
) do if not defined GODOT if exist %%G set GODOT=%%G
if not defined GODOT (
  echo Godot 4.6.1 mono not found. Set GODOT in qa\campaign_run.bat.
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

if not exist test-reports\campaign_save (
  echo No save to play from: put a folder of saves in test-reports\campaign_save\
  pause
  exit /b 1
)
if not exist test-reports\campaign_run mkdir test-reports\campaign_run
set CAMPAIGN_SHOTS=%CD%\test-reports\campaign_run
set CAMPAIGN_STATE=%CD%\test-reports\campaign_run
set SANDBOX_NAME=campaignrun
set SANDBOX_SEED=%CD%\test-reports\campaign_save
set CAMPAIGN=1
echo [2/2] Playing the campaign's next run. Shots: %CAMPAIGN_SHOTS%
%GODOT% --path . --resolution 1920x1080 -- --autoplay --config qa/configs/campaign/novice.json > test-reports\campaign_run.log 2>&1
echo Done. Log: test-reports\campaign_run.log
