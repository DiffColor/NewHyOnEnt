@echo off
setlocal

set "CONFIG=Release"
set "RID=win-x64"
set "PROJ=StartApps.csproj"
set "PUBLISH_ROOT=bin\publish"

if not exist "%PROJ%" (
    echo Project file not found: %PROJ%
    exit /b 1
)

if exist "%PUBLISH_ROOT%" rmdir /s /q "%PUBLISH_ROOT%"

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
set "OUT_DIR=%PUBLISH_ROOT%\%PROFILE%"

dotnet publish "%PROJ%" -c %CONFIG% -r %RID% --self-contained true ^
    -o "%OUT_DIR%" ^
    /p:StartAppsProfile=%PROFILE% ^
    /p:PublishSingleFile=true ^
    /p:PublishReadyToRun=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:DebugType=None

if errorlevel 1 (
    echo Publish failed for profile: %PROFILE%
    exit /b 1
)

if not exist "%OUT_DIR%\StartApps.exe" (
    echo Profile executable not found: %OUT_DIR%\StartApps.exe
    exit /b 1
)

echo %PROFILE% : %OUT_DIR%\StartApps.exe
exit /b 0
