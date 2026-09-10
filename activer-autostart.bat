@echo off
title NextStep - Activation Autostart
echo ========================================================
echo   Activation du demarrage automatique avec Windows
echo ========================================================
echo.

powershell -Command "$wsh = New-Object -ComObject WScript.Shell; $startupPath = [System.IO.Path]::Combine($env:APPDATA, 'Microsoft', 'Windows', 'Start Menu', 'Programs', 'Startup', 'NextStep.lnk'); $shortcut = $wsh.CreateShortcut($startupPath); $shortcut.TargetPath = 'c:\Users\hp\Desktop\Tools\Containers\02_ENGINEERING\Personal_Projects\NextStep\start-all-hidden.vbs'; $shortcut.WorkingDirectory = 'c:\Users\hp\Desktop\Tools\Containers\02_ENGINEERING\Personal_Projects\NextStep'; $shortcut.Save()"

echo [OK] Demarrage automatique reactive avec succes !
echo Dès que vous allumerez votre PC, NextStep demarrera en arriere-plan.
echo.
pause
