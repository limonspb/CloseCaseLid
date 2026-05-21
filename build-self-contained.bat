@echo off
setlocal

set "ROOT=%~dp0"
set "ICON_DIR=%ROOT%icons\"
set "RUNTIME=%~1"
if "%RUNTIME%"=="" set "RUNTIME=win-x64"
set "PROJECT=%ROOT%CloseCaseLid.csproj"
set "PUBLISH_DIR=%ROOT%publish-self-contained"
set "DOTNET="

if exist "%ROOT%.dotnet\dotnet.exe" (
  set "DOTNET=%ROOT%.dotnet\dotnet.exe"
)

if not defined DOTNET (
  for /f "delims=" %%P in ('where dotnet 2^>nul') do (
    if not defined DOTNET set "DOTNET=%%~fP"
  )
)

if not defined DOTNET (
  echo Could not find dotnet. Install a .NET SDK or add a repo-local ".dotnet\dotnet.exe".
  exit /b 1
)

for /f %%i in ('"%DOTNET%" --list-sdks') do set "HAS_SDK=1"
if not defined HAS_SDK (
  echo dotnet was found at "%DOTNET%" but no .NET SDK was available.
  exit /b 1
)

if exist "%ICON_DIR%convert-user-icons.ps1" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ICON_DIR%convert-user-icons.ps1"
  if errorlevel 1 exit /b 1
)

"%DOTNET%" publish "%PROJECT%" ^
  -c Release ^
  -r %RUNTIME% ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:EnableCompressionInSingleFile=true ^
  -o "%PUBLISH_DIR%"

if errorlevel 1 exit /b 1

echo Built self-contained app in "%PUBLISH_DIR%"
