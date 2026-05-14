param(
    [string]$PackageDir = "$env:USERPROFILE\Desktop\merc-mapper-run",
    [string]$InstallDir = "",
    [switch]$MachineInstall
)

$ErrorActionPreference = "Stop"

if ($MachineInstall) {
    $InstallDir = Join-Path $env:ProgramFiles "Merc Keyboard Mapper"
} elseif ([string]::IsNullOrWhiteSpace($InstallDir)) {
    $InstallDir = Join-Path $env:TEMP ("Merc Keyboard Mapper Smoke " + [Guid]::NewGuid().ToString("N"))
}

$uninstallRoot = if ($MachineInstall) {
    "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MercKeyboardMapper"
} else {
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MercKeyboardMapper"
}
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

function Assert-True($Condition, $Message) {
    if (-not $Condition) {
        throw $Message
    }
}

function Stop-MercProcesses {
    Get-Process MercKeyboardMapper, MercKeyboardMapperEngine, MercShellHookHost32, Merc.Mapper -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

function Assert-GuiSubsystem($Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
    $subsystem = [BitConverter]::ToUInt16($bytes, $peOffset + 0x5c)
    Assert-True ($subsystem -eq 2) "$Path is not a Windows GUI subsystem executable."
}

function Quote-Arg($Value) {
    '"' + ($Value -replace '"', '\"') + '"'
}

function Invoke-ProcessWithTimeout($FilePath, $ArgumentList, $Name, $TimeoutSeconds = 60) {
    Write-Host "Running $Name..."
    $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -PassThru
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        throw "$Name timed out after $TimeoutSeconds seconds."
    }

    $process.Refresh()
    return $process.ExitCode
}

$requiredPackage = @(
    "MercKeyboardMapper.exe",
    "MercKeyboardMapperEngine.exe",
    "MercKeyboardMapperEngine.dll",
    "MercKeyboardMapperEngine.deps.json",
    "MercKeyboardMapperEngine.runtimeconfig.json",
    "Merc.Mapper.Core.dll",
    "MercShellHook64.dll",
    "MercShellHook32.dll",
    "MercShellHookHost32.exe",
    "MercKeyboardMapperSetup.exe",
    "MercKeyboardMapperUninstall.exe"
)

Stop-MercProcesses
Write-Host "Cleaning previous smoke state..."
Remove-Item -LiteralPath $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $uninstallRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path $runKey -Name MercMapper, MercMapperGui -ErrorAction SilentlyContinue

Write-Host "Validating package files..."
foreach ($file in $requiredPackage) {
    $path = Join-Path $PackageDir $file
    Assert-True (Test-Path -LiteralPath $path) "Missing package file: $file"
    Assert-True ((Get-Item -LiteralPath $path).Length -gt 0) "Zero-byte package file: $file"
}

foreach ($forbidden in @("Merc.Mapper.exe", "Merc.Mapper.Gui.exe", "MercShellHook.dll")) {
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $PackageDir $forbidden))) "Forbidden production artifact present: $forbidden"
}

foreach ($exe in @("MercKeyboardMapper.exe", "MercKeyboardMapperEngine.exe", "MercKeyboardMapperSetup.exe", "MercKeyboardMapperUninstall.exe")) {
    Assert-GuiSubsystem (Join-Path $PackageDir $exe)
}

$setup = Join-Path $PackageDir "MercKeyboardMapperSetup.exe"
$setupArgs = "--quiet-install --install-dir $(Quote-Arg $InstallDir) --skip-runtime-check --launch --start-with-windows"
$setupExitCode = Invoke-ProcessWithTimeout -FilePath $setup -ArgumentList $setupArgs -Name "setup"
$setupLog = Join-Path $env:TEMP "MercKeyboardMapperSetup.log"
Assert-True ($setupExitCode -eq 0) "Setup failed with exit $setupExitCode. Log: $(Get-Content -LiteralPath $setupLog -Raw -ErrorAction SilentlyContinue)"

Write-Host "Validating installed files..."
foreach ($file in $requiredPackage | Where-Object { $_ -ne "MercKeyboardMapperSetup.exe" }) {
    Assert-True (Test-Path -LiteralPath (Join-Path $InstallDir $file)) "Installed payload missing: $file"
}
Assert-True (-not (Test-Path -LiteralPath (Join-Path $InstallDir "Merc.Mapper.exe"))) "Old console exe exists in installed payload."
Assert-True ((Get-ChildItem -LiteralPath $InstallDir -File).Count -ge 10) "Install directory is empty or incomplete."

Write-Host "Validating Add/Remove Programs registration..."
$uninstall = Get-ItemProperty -LiteralPath $uninstallRoot
Assert-True ($uninstall.DisplayName -eq "Merc Keyboard Mapper") "Add/Remove Programs DisplayName missing."
Assert-True ($uninstall.InstallLocation -eq $InstallDir) "Add/Remove Programs InstallLocation wrong."
Assert-True (Test-Path -LiteralPath $uninstall.DisplayIcon) "Add/Remove Programs DisplayIcon target missing."
Assert-True ($uninstall.UninstallString -match 'MercKeyboardMapperUninstall\.exe" --uninstall$') "UninstallString is wrong."
Assert-True ($uninstall.QuietUninstallString -match 'MercKeyboardMapperUninstall\.exe" --quiet-uninstall$') "QuietUninstallString is not quiet."

Start-Sleep -Seconds 5
Write-Host "Validating launched processes..."
$wrapper = Get-CimInstance Win32_Process -Filter "Name='MercKeyboardMapper.exe'" |
    Where-Object { $_.ExecutablePath -eq (Join-Path $InstallDir "MercKeyboardMapper.exe") }
$engine = Get-CimInstance Win32_Process -Filter "Name='MercKeyboardMapperEngine.exe'" |
    Where-Object { $_.ExecutablePath -eq (Join-Path $InstallDir "MercKeyboardMapperEngine.exe") }
Assert-True $wrapper "Launch-after-install did not start wrapper."
Assert-True $engine "Wrapper did not start hidden engine."

$startup = (Get-ItemProperty -Path $runKey -Name MercMapperGui -ErrorAction Stop).MercMapperGui
Assert-True ($startup -eq "`"$InstallDir\MercKeyboardMapper.exe`" --startup") "Startup Run value wrong: $startup"

Write-Host "Running uninstaller..."
$uninstaller = Join-Path $InstallDir "MercKeyboardMapperUninstall.exe"
$uninstallExitCode = Invoke-ProcessWithTimeout -FilePath $uninstaller -ArgumentList "--quiet-uninstall" -Name "uninstaller"
Assert-True ($uninstallExitCode -eq 0) "Uninstaller failed with exit $uninstallExitCode."

Write-Host "Validating uninstall cleanup..."
for ($attempt = 0; $attempt -lt 15 -and (Test-Path -LiteralPath $InstallDir); $attempt++) {
    Start-Sleep -Seconds 1
}

if (Test-Path -LiteralPath $InstallDir) {
    $remaining = @(Get-ChildItem -LiteralPath $InstallDir -File -Force)
    $unexpected = @($remaining | Where-Object { $_.Name -notmatch '^MercShellHook(32|64)\.dll$' })
    Assert-True ($unexpected.Count -eq 0) "Unexpected files remained after uninstall: $($unexpected.Name -join ', ')"
    Write-Warning "Locked hook DLL remained after uninstall and should be removed after the owning process exits or Windows restarts: $($remaining.Name -join ', ')"
}
Assert-True (-not (Test-Path -LiteralPath $uninstallRoot)) "Add/Remove Programs key remained after uninstall."
Assert-True (-not (Get-ItemProperty -Path $runKey -Name MercMapperGui -ErrorAction SilentlyContinue)) "Startup Run value remained after uninstall."
Assert-True (-not (Get-Process MercKeyboardMapper, MercKeyboardMapperEngine -ErrorAction SilentlyContinue)) "Mapper process remained after uninstall."

"Merc mapper release smoke passed for $PackageDir -> $InstallDir"
