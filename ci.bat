@echo off
setlocal

cd /d "%~dp0"

echo [1/5] restore...
dotnet restore NetworkPresetSwitcher.sln
if errorlevel 1 goto :error

echo [2/5] format check...
dotnet format NetworkPresetSwitcher.sln --verify-no-changes --no-restore
if errorlevel 1 goto :error

echo [3/5] build...
dotnet build NetworkPresetSwitcher.sln -c Release --no-restore -p:EnableNETAnalyzers=true -p:AnalysisMode=Minimum -p:AnalysisLevel=latest
if errorlevel 1 goto :error

echo [4/5] test...
dotnet test NetworkPresetSwitcher.sln -c Release --no-build --verbosity normal
if errorlevel 1 goto :error

echo [5/5] publish smoke test...
dotnet publish NetworkPresetSwitcher\NetworkPresetSwitcher.csproj -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o artifacts\ci
if errorlevel 1 goto :error
if not exist artifacts\ci\NetworkPresetSwitcher.exe goto :error

echo CI checks passed.
exit /b 0

:error
echo CI checks failed.
exit /b 1
