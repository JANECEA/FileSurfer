@echo off
setlocal enabledelayedexpansion

set VSWHERE="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

for /f "usebackq tokens=*" %%i in (`%VSWHERE% -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
    set MSBUILD=%%i
)

if not defined MSBUILD (
    echo [ERROR] Could not find MSBuild via vswhere. Is Visual Studio installed?
    exit /b 1
)

echo [INFO] Using MSBuild: %MSBUILD%

set SLN=%~dp0..\FileSurfer.sln
set PROJECT=%~dp0FileSurfer.Windows.csproj

rmdir /s /q bin\Publish

echo.
echo [INFO] Restoring solution...
"%MSBUILD%" "%SLN%" /t:Restore /p:Configuration=Release

if !ERRORLEVEL! NEQ 0 (
    echo [ERROR] Restore failed with exit code !ERRORLEVEL!.
    exit /b !ERRORLEVEL!
)

for %%R in (win-x64 win-arm64) do (
    echo.
    echo [INFO] Publishing %%R...

    "%MSBUILD%" "%PROJECT%" ^
        /t:Publish ^
        /p:Configuration=Release ^
        /p:TargetFramework=net8.0-windows ^
        /p:RuntimeIdentifier=%%R ^
        /p:SelfContained=true ^
        /p:PublishReadyToRun=true ^
        /p:PublishDir="%~dp0bin\Publish\net8.0-windows\%%R"

    if !ERRORLEVEL! NEQ 0 (
        echo [ERROR] Publish failed for %%R with exit code !ERRORLEVEL!.
        exit /b !ERRORLEVEL!
    )
    echo [OK] Published %%R
)

echo.
echo [OK] All targets published.