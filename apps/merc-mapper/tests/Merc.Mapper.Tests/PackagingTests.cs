namespace Merc.Mapper.Tests;

public sealed class PackagingTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void PackageScriptCleansOutputPublishesHiddenEngineAndCopiesNativeWrapper()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot, "apps/merc-mapper/package-release.cmd"));

        Assert.Contains("rmdir /s /q", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("taskkill /im MercKeyboardMapper.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("taskkill /im MercKeyboardMapperEngine.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Merc.Mapper.csproj", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercKeyboardMapper.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercKeyboardMapperEngine.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("build-release.cmd", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Compress-Archive", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Validating package payload", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Validating release output", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Forbidden production artifact", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Forbidden release artifact", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercKeyboardMapperSetup.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercKeyboardMapperUninstall.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Merc.Mapper.csproj\" --no-restore -c Release -r win-x64 --self-contained false", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ProductionEngine=true", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Merc.Mapper.Gui.csproj", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PublishSingleFile=false", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercSetup.cpp", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercSetup.rc", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rc.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gdi32.lib", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NativeBuildScriptUsesVswhereAndVerifiesRuntimeOutputs()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot, "apps/merc-mapper/native/MercShellHook/build-release.cmd"));

        Assert.Contains("vswhere.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Microsoft.VisualStudio.Component.VC.Tools.x86.x64", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercShellHook64.dll", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercShellHook32.dll", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercShellHookHost32.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercKeyboardMapper.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercKeyboardMapperUninstall.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercKeyboardMapper.cpp", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercUninstall.cpp", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shell32.lib", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProjectPublishFailsWhenNativeHookBinariesAreMissing()
    {
        var consoleProject = File.ReadAllText(Path.Combine(RepoRoot, "apps/merc-mapper/src/Merc.Mapper/Merc.Mapper.csproj"));

        Assert.Contains("ValidateNativeShellHookBinaries", consoleProject);
        Assert.Contains("Native shell hook binaries are missing", consoleProject);
        Assert.Contains("ProductionEngine", consoleProject);
        Assert.Contains("WinExe", consoleProject);
        Assert.Contains("MercKeyboardMapperEngine", consoleProject);

        var consoleProgram = File.ReadAllText(Path.Combine(RepoRoot, "apps/merc-mapper/src/Merc.Mapper/Program.cs"));
        Assert.Contains("--stop-event", consoleProgram);
        Assert.Contains("EventWaitHandle.OpenExisting", consoleProgram);
    }

    [Fact]
    public void DocsMentionVisibleGuiStartupPreservedFlags()
    {
        var guiStartup = File.ReadAllText(Path.Combine(RepoRoot, "apps/merc-mapper/GUI-STARTUP.md"));

        Assert.Contains("--no-q", guiStartup);
        Assert.Contains("--repeat", guiStartup);
        Assert.Contains("--repeat-delay-ms", guiStartup);
        Assert.Contains("--repeat-rate-ms", guiStartup);
    }

    [Fact]
    public void NativeWrapperSupportsTrayOwnedMapperLifecycle()
    {
        var wrapper = File.ReadAllText(Path.Combine(RepoRoot, "apps/merc-mapper/native/MercShellHook/MercKeyboardMapper.cpp"));

        Assert.Contains("NOTIFYICONDATAW", wrapper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Shell_NotifyIconW", wrapper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TaskbarCreated", wrapper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercKeyboardMapperSingleInstance", wrapper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--stop-event", wrapper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ShowWindow(window, SW_HIDE)", wrapper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CreateProcessW", wrapper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RestartMapper", wrapper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("g_mapperGeneration", wrapper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercKeyboardMapperEngine.exe", wrapper, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Keypad cluster mappings", wrapper, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("g_exitButton", wrapper, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Merc.Mapper.exe", wrapper, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Merc.Mapper.Gui.exe", wrapper, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetupProjectEmbedsPublishedPayload()
    {
        var setupSource = File.ReadAllText(Path.Combine(RepoRoot, "apps/merc-mapper/native/MercShellHook/MercSetup.cpp"));
        var setupResource = File.ReadAllText(Path.Combine(RepoRoot, "apps/merc-mapper/native/MercShellHook/MercSetup.rc"));

        Assert.Contains("RT_RCDATA", setupSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PayloadResourceId", setupSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Expand-Archive", setupSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PowerShellSingleQuoteAppend", setupSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HasRequiredPayloadFiles", setupSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CopyExtractedPayload", setupSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercShellHook32.dll','MercShellHook64.dll", setupSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("merc-mapper-payload.zip", setupResource, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetupWizardRegistersMachineApplicationAndUninstaller()
    {
        var setupProgram = File.ReadAllText(Path.Combine(RepoRoot, "apps/merc-mapper/native/MercShellHook/MercSetup.cpp"));

        Assert.Contains("MercKeyboardMapperUninstall.exe", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("StopRunningMapperProcesses", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercKeyboardMapperEngine.exe", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Merc.Mapper.exe", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--start-with-windows", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RegisterStartupForInstalledApp", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RelaunchElevatedAndWait", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--quiet-install", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--install-dir", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--skip-runtime-check", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HasRequiredDotNetRuntime", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet-runtime-win-x64.exe", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Microsoft.NETCore.App", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Microsoft.WindowsDesktop.App", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ProgramFiles", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IsRunningAsAdmin", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RelaunchElevated", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("runas", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HKEY_LOCAL_MACHINE", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CSIDL_COMMON_PROGRAMS", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CurrentVersion\\\\Uninstall\\\\MercKeyboardMapper", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Merc Keyboard Mapper.lnk", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UninstallString", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("QuietUninstallString", setupProgram, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--quiet-uninstall", setupProgram, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReleaseSmokeScriptChecksInstallerSideEffects()
    {
        var smoke = File.ReadAllText(Path.Combine(RepoRoot, "apps/merc-mapper/test-release-smoke.ps1"));

        Assert.Contains("MercKeyboardMapperSetup.exe", smoke, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercKeyboardMapperEngine.exe", smoke, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Merc.Mapper.exe", smoke, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UninstallString", smoke, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("QuietUninstallString", smoke, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MercMapperGui", smoke, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Launch-after-install", smoke, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Add/Remove Programs", smoke, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "merc-driver.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
