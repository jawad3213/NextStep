@echo off
title NextStep - Arret des services
echo ========================================================
echo   Arret des services NextStep (Backend, Agents, Frontend)
echo ========================================================
echo.

taskkill /F /IM NextStep.exe 2>nul
taskkill /F /IM dotnet.exe 2>nul
taskkill /F /IM python.exe 2>nul
taskkill /F /IM node.exe 2>nul

echo [OK] Tous les services ont ete arretes avec succes.
echo.
pause
