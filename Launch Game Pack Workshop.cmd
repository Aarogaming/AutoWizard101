@echo off
setlocal
cd /d "%~dp0"
set "WORKSHOP_PYTHON=%~dp0.local\envs\dance\Scripts\pythonw.exe"
if not exist "%WORKSHOP_PYTHON%" (
    echo The local Python environment is missing. Restore the tools setup first.
    pause
    exit /b 1
)
start "" "%WORKSHOP_PYTHON%" "%~dp0tools\game_pack_workshop.py"
