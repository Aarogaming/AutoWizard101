@echo off
setlocal
cd /d "%~dp0"
set "OBSERVER_PYTHON=%~dp0.local\envs\dance\Scripts\pythonw.exe"
if not exist "%OBSERVER_PYTHON%" (
    echo The isolated vision environment is missing. Restore the tools setup first.
    pause
    exit /b 1
)
start "" "%OBSERVER_PYTHON%" "%~dp0tools\diego_observer.py"
