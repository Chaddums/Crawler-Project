@echo off
echo.
echo  Junkbot Arena — Playtest Launch
echo  ================================
echo.

:: Launch the reporter in background
echo  Starting playtest reporter...
start "Playtest Reporter" python "%~dp0tools\playtest-reporter\reporter.py"

:: Launch the game (blocks until game closes)
echo  Launching game...
echo.
"C:\Users\Stu\Downloads\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe" --path "%~dp0Godot"

:: Kill the reporter when the game exits
echo.
echo  Game closed. Stopping reporter...
taskkill /FI "WINDOWTITLE eq Playtest Reporter" >nul 2>&1
echo  Done.
