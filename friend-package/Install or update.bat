@echo off
rem RWC Formations: one-click setup. Runs install.ps1 (next to this file) with Windows' own PowerShell.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
echo.
pause
