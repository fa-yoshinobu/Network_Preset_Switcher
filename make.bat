@echo off
setlocal

set "PROJECT=NetworkPresetSwitcher\NetworkPresetSwitcher.csproj"
set "CONFIG=Release"
set "RUNTIME=win-x64"
set "OUTDIR=NetworkPresetSwitcher\bin\%CONFIG%\net8.0-windows\%RUNTIME%\publish"
set "DO_CLEAN=0"

if "%~1"=="" (
echo.
echo ===== Build Options =====
echo [1] Publish (normal)
echo [2] Clean + Publish
choice /C 12 /N /M "Select option (1/2): "
if errorlevel 2 set "DO_CLEAN=1"
)

if not "%~1"=="" set "RUNTIME=%~1"
if not "%~2"=="" set "OUTDIR=%~2"
if /I "%~3"=="clean" set "DO_CLEAN=1"
if /I "%~1"=="clean" set "DO_CLEAN=1"
if /I "%~2"=="clean" set "DO_CLEAN=1"

echo Runtime: "%RUNTIME%"
echo Output : "%OUTDIR%"
if "%DO_CLEAN%"=="1" (
  echo Clean  : enabled
) else (
  echo Clean  : disabled
)

if "%DO_CLEAN%"=="1" (
echo [0/4] clean...
dotnet clean "%PROJECT%" -c %CONFIG%
if errorlevel 1 goto :error
)

echo [1/4] restore...
dotnet restore "%PROJECT%"
if errorlevel 1 goto :error

echo [2/4] publish single-file exe...
dotnet publish "%PROJECT%" ^
  -c %CONFIG% ^
  -r %RUNTIME% ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:EnableCompressionInSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:DebugType=None ^
  /p:DebugSymbols=false ^
  -o "%OUTDIR%"
if errorlevel 1 goto :error

echo [3/4] done.
echo Output: "%OUTDIR%"
exit /b 0

:error
echo Build failed.
pause
exit /b 1
