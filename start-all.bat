@echo off
title NextStep - Demarrage Complet
cd /d "c:\Users\hp\Desktop\Tools\Containers\02_ENGINEERING\Personal_Projects\NextStep"

:: 1. Demarrer le backend .NET (port 5000)
start /b "NextStep-Backend" cmd /c "cd backend && dotnet run --no-build"

:: 2. Demarrer les agents Python (port 8000)
start /b "NextStep-Agents" cmd /c "cd agents && python -m uvicorn main:app --port 8000"

:: 3. Demarrer le frontend Angular (port 4200)
start /b "NextStep-Frontend" cmd /c "cd frontend && npm start"

:: 4. Attendre quelques secondes que le frontend demarre
timeout /t 5 /nobreak >nul

:: 5. Lancer le tunnel permanent
call "c:\Users\hp\Desktop\Tools\Containers\02_ENGINEERING\Personal_Projects\NextStep\tunnel-permanent.bat"
