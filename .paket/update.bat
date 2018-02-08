@echo off
cd /d %~dp0
paket.bootstrapper.exe 5.124.1
paket.exe update
echo.
echo.
pause