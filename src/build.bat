@echo off
set MSBUILD_PATH="C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
set PROJECT_PATH="%~dp0FileSurfer.sln"

echo Restoring packages ...
cd %~dp0\FileSurfer\
dotnet restore
cd %~dp0..\

echo Building the project...
%MSBUILD_PATH% %PROJECT_PATH% /t:build /p:Configuration=Release

if %ERRORLEVEL% NEQ 0 (
    echo Build failed.
    exit /b %ERRORLEVEL%
)
