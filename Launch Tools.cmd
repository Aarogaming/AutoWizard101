@echo off
setlocal
cd /d "%~dp0"
set "TOOLS_APP=%~dp0.local\ToolsApp\ProjectMaelstrom.exe"
if not exist "%TOOLS_APP%" (
    echo Preparing AutoWizard101 Tools for first use...
    if not exist "%~dp0.local" mkdir "%~dp0.local"
    dotnet publish "%~dp0ProjectMaelstrom\ProjectMaelstrom\ProjectMaelstrom.csproj" -c Release -o "%~dp0.local\ToolsApp" > "%~dp0.local\tools-build.log" 2>&1
    if errorlevel 1 (
        echo Tools could not be prepared. Details are in .local\tools-build.log.
        pause
        exit /b 1
    )
)
start "" "%TOOLS_APP%" --tools
