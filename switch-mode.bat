@echo off
title NextStep - Gestion du mode Authentification
cd /d "%~dp0"

:menu
cls
echo ============================================================
echo      NextStep - Basculer le mode d'Authentification
echo ============================================================
echo.
echo   [1] Activer le Mode AUTH (Keycloak / Login obligatoire)
echo   [2] Activer le Mode DEV  (Sans Login / Utilisateur Seed direct)
echo   [3] Demarrer Keycloak via Docker
echo   [4] Quitter
echo.
echo ============================================================
set /p choix="Entrez votre choix (1, 2, 3 ou 4) : "

if "%choix%"=="1" (
    echo.
    python scripts\switch_auth_mode.py auth
    echo.
    echo Pensez a redemarrer le backend et le frontend si necessaire.
    pause
    goto menu
)

if "%choix%"=="2" (
    echo.
    python scripts\switch_auth_mode.py dev
    echo.
    echo Pensez a redemarrer le backend et le frontend si necessaire.
    pause
    goto menu
)

if "%choix%"=="3" (
    echo.
    echo Lancement de Keycloak avec Docker Compose...
    docker compose up -d keycloak
    echo.
    pause
    goto menu
)

if "%choix%"=="4" (
    exit /b 0
)

goto menu
