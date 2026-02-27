@echo off
setlocal

set "ROOT_DIR=%~dp0"
set "APP_PROJECT=%ROOT_DIR%HideProcess.App\HideProcess.App.csproj"
set "CONFIG=Release"
set "RID=win-x64"
set "OUT_ROOT=%ROOT_DIR%artifacts"
set "BUILD_OUT=%OUT_ROOT%\build"
set "PUBLISH_OUT=%OUT_ROOT%\publish"
set "SINGLE_OUT=%OUT_ROOT%\singlefile"
set "INSTALLER_OUT=%OUT_ROOT%\installer"
set "ISS_SCRIPT=%ROOT_DIR%installer\HideProcess.iss"

if not exist "%APP_PROJECT%" (
    echo [ERROR] Project file not found:
    echo         %APP_PROJECT%
    pause
    exit /b 1
)

echo [INFO] Stopping running app to avoid file locks...
taskkill /F /IM HideProcess.App.exe >nul 2>nul

echo [1/6] Restore...
dotnet restore "%APP_PROJECT%"
if errorlevel 1 goto :fail

echo [2/6] Build Release...
dotnet build "%APP_PROJECT%" -c %CONFIG% -o "%BUILD_OUT%"
if errorlevel 1 goto :fail

echo [3/6] Publish Multi-File (self-contained, %RID%)...
dotnet publish "%APP_PROJECT%" ^
  -c %CONFIG% ^
  -r %RID% ^
  --self-contained true ^
  -p:PublishSingleFile=false ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%PUBLISH_OUT%"
if errorlevel 1 goto :fail

echo [4/6] Publish Single-File (self-contained, %RID%)...
dotnet publish "%APP_PROJECT%" ^
  -c %CONFIG% ^
  -r %RID% ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%SINGLE_OUT%"
if errorlevel 1 goto :fail

echo [5/6] Build Installer (Inno Setup)...
if not exist "%ISS_SCRIPT%" (
    echo [WARN] Installer script not found: %ISS_SCRIPT%
    echo [WARN] Skipping installer build.
    goto :done
)

if not exist "%PUBLISH_OUT%\HideProcess.App.exe" (
    echo [WARN] Publish executable not found.
    echo [WARN] Skipping installer build.
    goto :done
)

set "ISCC="
for /f "delims=" %%I in ('where ISCC.exe 2^>nul') do (
    set "ISCC=%%~fI"
    goto :build_installer
)

if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "C:\Program Files\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
if not defined ISCC goto :no_iscc
goto :build_installer

:no_iscc
echo [WARN] Inno Setup compiler (ISCC.exe) not found.
echo [WARN] Install Inno Setup 6 or add ISCC.exe to PATH.
goto :done

:build_installer

if not exist "%INSTALLER_OUT%" mkdir "%INSTALLER_OUT%"
"%ISCC%" ^
  "/DSourceDir=%PUBLISH_OUT%" ^
  "/DSourceIcon=%ROOT_DIR%HideProcess.App\Assets\HideProcess.ico" ^
  "/DOutputDir=%INSTALLER_OUT%" ^
  "%ISS_SCRIPT%"
if errorlevel 1 goto :fail

:done
echo [6/6] Done.
echo.
echo Release build output:
echo   %BUILD_OUT%
echo.
echo Multi-file publish output:
echo   %PUBLISH_OUT%
echo.
echo Single-file publish output:
echo   %SINGLE_OUT%\HideProcess.App.exe
echo.
echo Installer output:
echo   %INSTALLER_OUT%
echo.
pause
exit /b 0

:fail
echo.
echo [ERROR] Build failed. See logs above.
pause
exit /b 1
