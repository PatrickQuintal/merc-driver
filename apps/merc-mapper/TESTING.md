# Testing

## Unit Tests

Run from the repo root:

```bash
dotnet test apps/merc-mapper/tests/Merc.Mapper.Tests/Merc.Mapper.Tests.csproj -c Debug
```

The unit tests exercise production code directly:

- `MapperOptions.Parse`
- `WindowsStartupRegistration.BuildCommand`
- `MercMappingPolicy`
- `MappedKeyState`
- `KeyMappingCatalog`
- packaging script/project validation

## No-Hardware Behavior Checks

Clean console/core build without native hook outputs:

```bash
tmp=$(mktemp -d)
rsync -a --exclude .git --exclude bin --exclude obj ./ "$tmp"/
rm -rf "$tmp/apps/merc-mapper/src/Merc.Mapper/native"
dotnet build "$tmp/apps/merc-mapper/src/Merc.Mapper/Merc.Mapper.csproj" -c Debug
```

Publish validation must fail without native hook outputs:

```bash
set +e
dotnet publish "$tmp/apps/merc-mapper/src/Merc.Mapper/Merc.Mapper.csproj" -c Release -r win-x64 --self-contained true -o "$tmp/publish-test" > "$tmp/publish.log" 2>&1
test $? -ne 0
rg "Native shell hook binaries are missing" "$tmp/publish.log"
```

Windows release packaging:

```cmd
apps\merc-mapper\package-release.cmd %TEMP%\merc-mapper-run-test
```

The package output must contain:

- `MercKeyboardMapper.exe`
- `MercKeyboardMapperEngine.exe`
- `MercShellHook64.dll`
- `MercShellHook32.dll`
- `MercShellHookHost32.exe`
- `MercKeyboardMapperSetup.exe`
- `MercKeyboardMapperUninstall.exe`

The package output must not contain `Merc.Mapper.exe`, stale native build artifacts such as `.lib`, `.exp`, `obj`, unversioned `MercShellHook.dll`, or the old script installer files.

Installer release smoke test:

```powershell
.\apps\merc-mapper\test-release-smoke.ps1 -PackageDir "$env:USERPROFILE\Desktop\merc-mapper-run"
```

Default Program Files install smoke test, from an elevated PowerShell:

```powershell
.\apps\merc-mapper\test-release-smoke.ps1 -PackageDir "$env:USERPROFILE\Desktop\merc-mapper-run" -MachineInstall
```
