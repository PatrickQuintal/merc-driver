@echo off
setlocal

set "RELEASE_DIR=%USERPROFILE%\Desktop\merc-mapper-run"
if not "%~1"=="" set "RELEASE_DIR=%~1"

set "ROOT=%~dp0..\.."
set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
set "PAYLOAD_ZIP=%TEMP%\merc-mapper-payload.zip"
set "PACKAGE_DIR=%TEMP%\merc-mapper-package-output"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "NATIVE_OBJ=%TEMP%\merc-native-setup-build"

echo Building native shell hook...
call "%~dp0native\MercShellHook\build-release.cmd" || exit /b 1

echo Cleaning package staging...
if exist "%PACKAGE_DIR%" rmdir /s /q "%PACKAGE_DIR%" || exit /b 1
mkdir "%PACKAGE_DIR%" || exit /b 1

echo Publishing hidden mapper engine...
"%DOTNET%" restore "%~dp0src\Merc.Mapper\Merc.Mapper.csproj" -r win-x64 || exit /b 1
"%DOTNET%" publish "%~dp0src\Merc.Mapper\Merc.Mapper.csproj" --no-restore -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -p:ProductionEngine=true -o "%PACKAGE_DIR%" || exit /b 1

echo Copying native wrapper and uninstaller...
copy /y "%~dp0src\Merc.Mapper\native\win-x64\MercKeyboardMapper.exe" "%PACKAGE_DIR%\MercKeyboardMapper.exe" > nul || exit /b 1
copy /y "%~dp0src\Merc.Mapper\native\win-x64\MercKeyboardMapperUninstall.exe" "%PACKAGE_DIR%\MercKeyboardMapperUninstall.exe" > nul || exit /b 1

echo Validating package payload...
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$keep = @('MercKeyboardMapperEngine.exe','MercKeyboardMapperEngine.dll','MercKeyboardMapperEngine.pdb','MercKeyboardMapperEngine.deps.json','MercKeyboardMapperEngine.runtimeconfig.json','Merc.Mapper.Core.dll','Merc.Mapper.Core.pdb','Merc.Mapper.Core.deps.json','MercKeyboardMapper.exe','MercKeyboardMapperUninstall.exe','MercShellHook32.dll','MercShellHook64.dll','MercShellHookHost32.exe'); Get-ChildItem -LiteralPath '%PACKAGE_DIR%' | Where-Object { $_.PSIsContainer -or ($keep -notcontains $_.Name) } | Remove-Item -Recurse -Force; $required = @('MercKeyboardMapperEngine.exe','MercKeyboardMapperEngine.dll','MercKeyboardMapperEngine.runtimeconfig.json','Merc.Mapper.Core.dll','MercKeyboardMapper.exe','MercKeyboardMapperUninstall.exe','MercShellHook32.dll','MercShellHook64.dll','MercShellHookHost32.exe'); foreach ($item in $required) { $path = Join-Path '%PACKAGE_DIR%' $item; if (-not (Test-Path -LiteralPath $path)) { throw ('Missing package file: ' + $item) }; if ((Get-Item -LiteralPath $path).Length -le 0) { throw ('Zero-byte package file: ' + $item) } }; foreach ($forbidden in @('Merc.Mapper.exe','Merc.Mapper.Gui.exe','MercShellHook.dll')) { if (Test-Path -LiteralPath (Join-Path '%PACKAGE_DIR%' $forbidden)) { throw ('Forbidden production artifact: ' + $forbidden) } }" || exit /b 1

echo Building setup wizard...
if exist "%PAYLOAD_ZIP%" del /q "%PAYLOAD_ZIP%" || exit /b 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%PACKAGE_DIR%\*' -DestinationPath '%PAYLOAD_ZIP%' -Force" || exit /b 1
if not exist "%VSWHERE%" exit /b 1
for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find Common7\Tools\VsDevCmd.bat`) do set "VSDEVCMD=%%I"
if not defined VSDEVCMD exit /b 1
if exist "%NATIVE_OBJ%" rmdir /s /q "%NATIVE_OBJ%" || exit /b 1
mkdir "%NATIVE_OBJ%" || exit /b 1
copy /y "%PAYLOAD_ZIP%" "%~dp0native\MercShellHook\merc-mapper-payload.zip" > nul || exit /b 1
call "%VSDEVCMD%" -arch=x64 || exit /b 1
rc.exe /nologo /fo "%NATIVE_OBJ%\MercSetup.res" "%~dp0native\MercShellHook\MercSetup.rc" || exit /b 1
cl.exe /nologo /W4 /WX /O2 /DUNICODE /D_UNICODE /Fo"%NATIVE_OBJ%\MercSetup.obj" "%~dp0native\MercShellHook\MercSetup.cpp" "%NATIVE_OBJ%\MercSetup.res" /link /subsystem:windows /out:"%PACKAGE_DIR%\MercKeyboardMapperSetup.exe" /pdb:"%NATIVE_OBJ%\MercKeyboardMapperSetup.pdb" user32.lib gdi32.lib shell32.lib shlwapi.lib advapi32.lib ole32.lib uuid.lib || exit /b 1
del /q "%~dp0native\MercShellHook\merc-mapper-payload.zip" 2> nul
copy /y "%~dp0src\Merc.Mapper\native\win-x64\MercKeyboardMapperUninstall.exe" "%PACKAGE_DIR%\MercKeyboardMapperUninstall.exe" > nul || exit /b 1

echo Stopping running mapper processes...
taskkill /im MercKeyboardMapper.exe /f > nul 2> nul
taskkill /im MercKeyboardMapperEngine.exe /f > nul 2> nul
taskkill /im Merc.Mapper.exe /f > nul 2> nul
taskkill /im MercShellHookHost32.exe /f > nul 2> nul

echo Updating output: %RELEASE_DIR%
if not exist "%RELEASE_DIR%" mkdir "%RELEASE_DIR%" || exit /b 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$src = '%PACKAGE_DIR%'; $dst = '%RELEASE_DIR%'; $keep = (Get-ChildItem -LiteralPath $src -File).Name; Get-ChildItem -LiteralPath $dst -Force | Where-Object { $_.PSIsContainer -or ($keep -notcontains $_.Name) } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue; foreach ($file in Get-ChildItem -LiteralPath $src -File) { $target = Join-Path $dst $file.Name; try { Copy-Item -LiteralPath $file.FullName -Destination $target -Force -ErrorAction Stop } catch { if (-not (Test-Path -LiteralPath $target)) { throw }; Write-Warning ('Kept existing locked file: ' + $target) } }" || exit /b 1
echo Validating release output...
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$required = @('MercKeyboardMapperEngine.exe','MercKeyboardMapperEngine.dll','MercKeyboardMapperEngine.runtimeconfig.json','Merc.Mapper.Core.dll','MercKeyboardMapper.exe','MercKeyboardMapperSetup.exe','MercKeyboardMapperUninstall.exe','MercShellHook32.dll','MercShellHook64.dll','MercShellHookHost32.exe'); foreach ($item in $required) { $path = Join-Path '%RELEASE_DIR%' $item; if (-not (Test-Path -LiteralPath $path)) { throw ('Missing release file: ' + $item) }; if ((Get-Item -LiteralPath $path).Length -le 0) { throw ('Zero-byte release file: ' + $item) } }; foreach ($forbidden in @('Merc.Mapper.exe','Merc.Mapper.Gui.exe','MercShellHook.dll')) { if (Test-Path -LiteralPath (Join-Path '%RELEASE_DIR%' $forbidden)) { throw ('Forbidden release artifact remained: ' + $forbidden) } }" || exit /b 1
rmdir /s /q "%NATIVE_OBJ%" || exit /b 1
del /q "%PAYLOAD_ZIP%" || exit /b 1
rmdir /s /q "%PACKAGE_DIR%" || exit /b 1

echo Merc mapper release ready: %RELEASE_DIR%
exit /b 0
