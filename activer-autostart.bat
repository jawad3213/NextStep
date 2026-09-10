@echo off
title NextStep - Activation Autostart
echo ========================================================
echo   Activation du demarrage automatique avec Windows
echo ========================================================
echo.

copy /y "c:\Users\hp\Desktop\Tools\Containers\02_ENGINEERING\Personal_Projects\NextStep\NextStep-Autostart.cmd" "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\" >nul

echo [OK] Demarrage automatique active avec succes !
echo Dès que vous allumerez votre PC, NextStep demarrera automatiquement.
echo.
pause
