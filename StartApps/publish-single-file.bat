@echo off
setlocal

set "CONFIG=Release"
set "RID=win-x64"
set "SCRIPT=publish-single-file.ps1"

if not exist "%SCRIPT%" (
    echo Publish script not found: %SCRIPT%
    exit /b 1
)

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
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" -StartAppsProfile "%PROFILE%" -Configuration "%CONFIG%" -RuntimeIdentifier "%RID%"
if errorlevel 1 (
    echo Publish failed for profile: %PROFILE%
    exit /b 1
)

exit /b 0
