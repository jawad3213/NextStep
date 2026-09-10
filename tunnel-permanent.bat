@echo off
title NextStep Permanent Tunnel
cd /d "c:\Users\hp\Desktop\Tools\Containers\02_ENGINEERING\Personal_Projects\NextStep"

:boucle
echo [%DATE% %TIME%] Lancement du tunnel Ngrok permanent (https://ladybird-distinct-regularly.ngrok-free.app)...
call "C:\Users\hp\AppData\Roaming\npm\ngrok.cmd" http 4200 --url=ladybird-distinct-regularly.ngrok-free.app
echo [%DATE% %TIME%] Deconnexion detectee, reconnexion dans 3 secondes...
timeout /t 3 /nobreak >nul
goto boucle
