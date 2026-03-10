@echo off
echo.
echo  Junkbot Arena — AutoPlay Mode
echo  ===============================
echo.

:: Start the web viewer in background
echo  Starting screenshot viewer on port 8090...
start "AutoPlay Viewer" python "%~dp0tools\autoplay-viewer\viewer.py"

:: Launch the game with --autoplay flag
echo  Launching game in autoplay mode...
echo.
"C:\Users\Stu\Downloads\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe" --path "%~dp0Godot" -- --autoplay

:: Cleanup
echo.
echo  Game closed. Stopping viewer...
taskkill /FI "WINDOWTITLE eq AutoPlay Viewer" >nul 2>&1
echo  Done.
