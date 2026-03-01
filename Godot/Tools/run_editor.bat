@echo off
cd /d "%~dp0"

:menu
echo.
echo  Junkbot Arena - Asset Editors
echo  =============================
echo.
echo   [1] String Editor  (port 8080)
echo   [2] Icon Editor    (port 8081)
echo   [3] Audio Editor   (port 8082)
echo   [4] All Editors
echo   [Q] Quit
echo.
set /p choice="Select: "

if "%choice%"=="1" goto string
if "%choice%"=="2" goto icon
if "%choice%"=="3" goto audio
if "%choice%"=="4" goto all
if /i "%choice%"=="q" goto end
echo Invalid choice.
goto menu

:string
echo Starting String Editor on port 8080...
python string_editor.py
pause
goto menu

:icon
echo Starting Icon Editor on port 8081...
python icon_editor.py
pause
goto menu

:audio
echo Starting Audio Editor on port 8082...
python audio_editor.py
pause
goto menu

:all
echo Starting all editors...
start "String Editor" cmd /c "python string_editor.py"
start "Icon Editor" cmd /c "python icon_editor.py"
start "Audio Editor" cmd /c "python audio_editor.py"
echo.
echo All editors launched in separate windows.
echo Press any key to return to menu...
pause >nul
goto menu

:end
