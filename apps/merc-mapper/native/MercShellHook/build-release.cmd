@echo off
setlocal

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "OUTDIR=%~dp0..\..\src\Merc.Mapper\native\win-x64"
set "OBJDIR=%TEMP%\merc-shell-hook-build"
set "PATH=C:\Windows\System32;C:\Windows;C:\Windows\System32\Wbem;%PATH%"

if not exist "%VSWHERE%" goto MissingVswhere

for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find Common7\Tools\VsDevCmd.bat`) do set "VSDEVCMD=%%I"

if not defined VSDEVCMD goto MissingVsDevCmd

if not exist "%OUTDIR%" mkdir "%OUTDIR%"
if not exist "%OBJDIR%" mkdir "%OBJDIR%"

del /q "%OUTDIR%\MercShellHook64.dll" "%OUTDIR%\MercShellHook32.dll" "%OUTDIR%\MercShellHookHost32.exe" "%OUTDIR%\MercKeyboardMapper.exe" "%OUTDIR%\MercKeyboardMapperUninstall.exe" 2>nul

call "%VSDEVCMD%" -arch=x64 || exit /b 1

cl.exe /nologo /LD /W4 /WX /O2 /DUNICODE /D_UNICODE /Fo"%OBJDIR%\MercShellHook64.obj" "%~dp0MercShellHook.cpp" /link /out:"%OUTDIR%\MercShellHook64.dll" /implib:"%OUTDIR%\MercShellHook64.lib" /pdb:"%OUTDIR%\MercShellHook64.pdb" user32.lib || exit /b 1

call "%VSDEVCMD%" -arch=x86 || exit /b 1

cl.exe /nologo /LD /W4 /WX /O2 /DUNICODE /D_UNICODE /Fo"%OBJDIR%\MercShellHook32.obj" "%~dp0MercShellHook.cpp" /link /out:"%OUTDIR%\MercShellHook32.dll" /implib:"%OUTDIR%\MercShellHook32.lib" /pdb:"%OUTDIR%\MercShellHook32.pdb" user32.lib || exit /b 1

cl.exe /nologo /W4 /WX /O2 /DUNICODE /D_UNICODE /Fo"%OBJDIR%\MercShellHookHost32.obj" "%~dp0MercShellHookHost.cpp" /link /subsystem:windows /out:"%OUTDIR%\MercShellHookHost32.exe" /pdb:"%OUTDIR%\MercShellHookHost32.pdb" user32.lib
if errorlevel 1 exit /b %ERRORLEVEL%

call "%VSDEVCMD%" -arch=x64 || exit /b 1

cl.exe /nologo /W4 /WX /O2 /DUNICODE /D_UNICODE /Fo"%OBJDIR%\MercUninstall.obj" "%~dp0MercUninstall.cpp" /link /subsystem:windows /out:"%OUTDIR%\MercKeyboardMapperUninstall.exe" /pdb:"%OUTDIR%\MercKeyboardMapperUninstall.pdb" user32.lib shell32.lib shlwapi.lib advapi32.lib
if errorlevel 1 exit /b %ERRORLEVEL%

cl.exe /nologo /W4 /WX /O2 /DUNICODE /D_UNICODE /EHsc /Fo"%OBJDIR%\MercKeyboardMapper.obj" "%~dp0MercKeyboardMapper.cpp" /link /subsystem:windows /out:"%OUTDIR%\MercKeyboardMapper.exe" /pdb:"%OUTDIR%\MercKeyboardMapper.pdb" user32.lib gdi32.lib shell32.lib shlwapi.lib advapi32.lib
if errorlevel 1 exit /b %ERRORLEVEL%

if not exist "%OUTDIR%\MercShellHook64.dll" exit /b 1
if not exist "%OUTDIR%\MercShellHook32.dll" exit /b 1
if not exist "%OUTDIR%\MercShellHookHost32.exe" exit /b 1
if not exist "%OUTDIR%\MercKeyboardMapper.exe" exit /b 1
if not exist "%OUTDIR%\MercKeyboardMapperUninstall.exe" exit /b 1
exit /b 0

:MissingVswhere
echo vswhere.exe not found: %VSWHERE%
echo Install Visual Studio 2022/2026 or Build Tools with the Desktop development with C++ workload.
exit /b 1

:MissingVsDevCmd
echo VsDevCmd.bat with C++ build tools was not found.
echo Install the Desktop development with C++ workload, then rerun this script.
exit /b 1
