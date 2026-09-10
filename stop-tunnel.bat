@echo off
echo Arret du tunnel NextStep...
taskkill /F /IM ngrok.exe 2>nul
taskkill /F /IM cloudflared.exe 2>nul
wmic process where "commandline like '%%localtunnel%%'" delete 2>nul
echo Tunnel arrete avec succes.
pause
