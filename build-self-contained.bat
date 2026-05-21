@echo off
setlocal

set "ROOT=%~dp0"
set "ICON_DIR=%ROOT%icons\"
set "RUNTIME=%~1"
if "%RUNTIME%"=="" set "RUNTIME=win-x64"
set "PROJECT=%ROOT%CloseCaseLid.csproj"
set "PUBLISH_DIR=%ROOT%publish-self-contained"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo dotnet CLI is not installed. Install a .NET SDK first.
  exit /b 1
)

for /f %%i in ('dotnet --list-sdks') do set "HAS_SDK=1"
if not defined HAS_SDK (
  echo dotnet is installed but no .NET SDK was found.
  exit /b 1
)

if exist "%ICON_DIR%convert-user-icons.ps1" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ICON_DIR%convert-user-icons.ps1"
  if errorlevel 1 exit /b 1
)

dotnet publish "%PROJECT%" ^
  -c Release ^
  -r %RUNTIME% ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:EnableCompressionInSingleFile=true ^
  -o "%PUBLISH_DIR%"

if errorlevel 1 exit /b 1

echo Built self-contained app in "%PUBLISH_DIR%"
