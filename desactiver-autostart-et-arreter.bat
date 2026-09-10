@echo off
title NextStep - Desactivation et Arret
echo ========================================================
echo   Desactivation du demarrage automatique et arret
echo ========================================================
echo.

:: 1. Supprimer le raccourci du dossier de demarrage automatique de Windows
if exist "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\NextStep-Autostart.cmd" (
    del /f /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\NextStep-Autostart.cmd"
    echo [OK] Script de demarrage automatique supprime de Windows.
)
if exist "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\NextStep.lnk" (
    del /f /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\NextStep.lnk"
    echo [OK] Raccourci de demarrage automatique supprime de Windows.
)

:: 2. Arreter les tunnels (Ngrok / Cloudflare / Localtunnel)
echo Arret du tunnel...
taskkill /F /IM ngrok.exe 2>nul
taskkill /F /IM cloudflared.exe 2>nul
wmic process where "commandline like '%%localtunnel%%'" delete 2>nul

:: 3. Arreter les serveurs d'arriere-plan
echo Arret des serveurs (Backend .NET, Agents Python, Frontend)...
taskkill /F /IM NextStep.exe 2>nul
taskkill /F /IM dotnet.exe 2>nul
taskkill /F /IM python.exe 2>nul
taskkill /F /IM node.exe 2>nul

echo.
echo ========================================================
echo   Tous les services ont ete arretes avec succes !
echo   NextStep ne demarrera plus au demarrage du PC.
echo ========================================================
echo.
pause
