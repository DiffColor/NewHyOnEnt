@echo off
setlocal

set "CONFIG=Release"
set "RID=win-x64"
set "SCRIPT_DIR=%~dp0"
set "SCRIPT=%SCRIPT_DIR%publish-single-file.ps1"
set "POWERSHELL_EXE="

if not exist "%SCRIPT%" (
    echo Publish script not found: %SCRIPT%
    exit /b 1
)

call :resolve_powershell
if errorlevel 1 exit /b 1

echo Using PowerShell engine: %POWERSHELL_EXE%

call :publish_profile default
if errorlevel 1 exit /b 1

call :publish_profile manager
if errorlevel 1 exit /b 1

call :publish_profile player
if errorlevel 1 exit /b 1

echo.
echo Publish completed successfully.
endlocal
exit /b 0

:publish_profile
set "PROFILE=%~1"
"%POWERSHELL_EXE%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" -StartAppsProfile "%PROFILE%" -Configuration "%CONFIG%" -RuntimeIdentifier "%RID%"
if errorlevel 1 (
    echo Publish failed for profile: %PROFILE%
    exit /b 1
)

exit /b 0

:resolve_powershell
for /f "delims=" %%I in ('where pwsh 2^>nul') do (
    if not defined POWERSHELL_EXE (
        set "POWERSHELL_EXE=%%~fI"
    )
)

if defined POWERSHELL_EXE exit /b 0

for /f "delims=" %%I in ('where powershell 2^>nul') do (
    if not defined POWERSHELL_EXE (
        set "POWERSHELL_EXE=%%~fI"
    )
)

if defined POWERSHELL_EXE exit /b 0

echo Neither 'pwsh' nor 'powershell' was found in PATH.
exit /b 1
