@echo off
title NextStep - Demarrage Local
cd /d "c:\Users\hp\Desktop\Tools\Containers\02_ENGINEERING\Personal_Projects\NextStep"

echo ========================================================
echo   Demarrage de NextStep (Environnement Local)
echo ========================================================
echo.

:: 1. Demarrer le backend .NET (port 5000)
echo [1/3] Demarrage du backend .NET (port 5000)...
start "NextStep-Backend" /min cmd /c "cd backend && dotnet run --no-build"

:: 2. Demarrer les agents Python (port 8000)
echo [2/3] Demarrage des agents Python (port 8000)...
start "NextStep-Agents" /min cmd /c "cd agents && python -m uvicorn main:app --port 8000"

:: 3. Demarrer le frontend Angular (port 4200)
echo [3/3] Demarrage du frontend Angular (port 4200)...
start "NextStep-Frontend" /min cmd /c "cd frontend && npm start"

echo.
echo ========================================================
echo   NextStep demarre avec succes !
echo   Application disponible sur : http://localhost:4200
echo ========================================================
echo.
timeout /t 5 >nul
