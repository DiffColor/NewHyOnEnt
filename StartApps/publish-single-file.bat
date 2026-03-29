@echo off
setlocal

set "CONFIG=Release"
set "RID=win-x64"
set "PROJ=StartApps.csproj"
set "OUTPUT=bin\%CONFIG%\net9.0-windows\%RID%\publish"
set "BASE_EXE=%OUTPUT%\StartApps.exe"
set "MANAGER_EXE=%OUTPUT%\StartApps.Manager.exe"
set "PLAYER_EXE=%OUTPUT%\StartApps.Player.exe"

if not exist "%PROJ%" (
    echo Project file not found: %PROJ%
    exit /b 1
)

dotnet publish "%PROJ%" -c %CONFIG% -r %RID% --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:PublishReadyToRun=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:DebugType=None

if errorlevel 1 (
    echo Publish failed
    exit /b 1
)

if not exist "%BASE_EXE%" (
    echo Base executable not found: %BASE_EXE%
    exit /b 1
)

copy /Y "%BASE_EXE%" "%MANAGER_EXE%" >nul
if errorlevel 1 (
    echo Failed to create manager variant
    exit /b 1
)

copy /Y "%BASE_EXE%" "%PLAYER_EXE%" >nul
if errorlevel 1 (
    echo Failed to create player variant
    exit /b 1
)

echo.
echo Base    : %BASE_EXE%
echo Manager : %MANAGER_EXE%
echo Player  : %PLAYER_EXE%

endlocal
