@echo off
setlocal

set "ROOT=%~dp0"
set "ICON_DIR=%ROOT%icons\"
set "CSC="

for %%P in (
  "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
  "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
  "%WINDIR%\Microsoft.NET\Framework64\v3.5\csc.exe"
  "%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe"
) do (
  if not defined CSC if exist %%~P set "CSC=%%~P"
)

if not defined CSC (
  for /f "delims=" %%P in ('where csc 2^>nul') do (
    if not defined CSC set "CSC=%%~fP"
  )
)

if not defined CSC (
  echo Could not find a usable csc.exe. Install .NET Framework build tools or Visual Studio build tools.
  exit /b 1
)

if exist "%ICON_DIR%convert-user-icons.ps1" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ICON_DIR%convert-user-icons.ps1"
  if errorlevel 1 exit /b 1
)

"%CSC%" ^
  /nologo ^
  /target:winexe ^
  /out:"%ROOT%CloseCaseLid.exe" ^
  /win32icon:"%ICON_DIR%icon-sleep.ico" ^
  /resource:"%ICON_DIR%icon-sleep.ico",CloseCaseLid.icon-sleep.ico ^
  /resource:"%ICON_DIR%icon-do-nothing.ico",CloseCaseLid.icon-do-nothing.ico ^
  /resource:"%ICON_DIR%icon-unknown.ico",CloseCaseLid.icon-unknown.ico ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.Drawing.dll ^
  "%ROOT%Program.cs"

if errorlevel 1 exit /b 1

echo Built framework app at "%ROOT%CloseCaseLid.exe"
