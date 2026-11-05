@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Run.ps1" -BattingPreview %*
if errorlevel 1 pause
