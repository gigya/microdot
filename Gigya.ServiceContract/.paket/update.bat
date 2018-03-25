@echo off
cd /d %~dp0
paket.bootstrapper.exe
paket.exe update
echo.
echo.
pause