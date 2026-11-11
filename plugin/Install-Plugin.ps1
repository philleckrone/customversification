<#
.SYNOPSIS
  Installs, removes or checks the Custom Versification Editor plugin for Paratext 9.

.DESCRIPTION
  Copies the CustomVrsEditor folder next to this script (the plugin CustomVrsEditorPlugin.ptxplg and the
  program CustomVrsEditor.exe) into <Paratext folder>\plugins\CustomVrsEditor\ of EVERY Paratext 9
  installation on this computer (for example "Paratext 9" and "Paratext 9 Beta").

  Why the extra steps: Paratext loads plugins with .NET's Assembly.LoadFrom and silently
  skips any that fail. Files that came by download, e-mail or Dropbox carry Windows'
  "came from the internet" mark, and .NET refuses to load such a plugin, so it never
  appears in Paratext. The installer checks each package file against the SHA-256 hash
  written into this script when it was made, writes those same checked bytes (which
  carry no mark), and then checks that each installed copy really loads.

  Security: Paratext 9 makes its plugins folder writable by every user, so the plugin is
  copied there from this normal window, and nothing running as administrator writes there.
  An administrator window is opened only for folders that ordinary users cannot change (for
  example the plugins folder of Paratext 9 Beta, or Program Files for a program that runs
  as administrator); it also runs Paratext's UpdateParatextPluginCache.exe for those
  folders. The checksums and the load check then run again in this window. Started "as
  administrator", the script refuses to change a shared plugins folder.

  Paratext must be closed. Run it normally (not "as administrator"); it asks for
  administrator rights when it needs them.
  Easiest: double-click Install-Plugin.cmd, Uninstall-Plugin.cmd or Check-Plugin.cmd.
  From a PowerShell window, quote the path (folder names may contain spaces):

.PARAMETER Uninstall
  Remove the plugin from every Paratext installation that has it.
.PARAMETER Check
  Change nothing: list the Paratext installations and report, for each, whether the plugin
  is installed and whether Paratext will be able to load it. No administrator rights needed.
.PARAMETER ParatextDir
  Testing only, with -NoElevate or -Check in a NON-administrator window: use these
  Paratext folders instead of searching, separated by ";" (also the ParatextInstallDir
  variable). One string, because powershell -File cannot pass arrays. Ignored whenever
  the script runs as administrator.
.PARAMETER NoElevate
  For testing with -ParatextDir: do not ask for administrator rights.

.EXAMPLE
  & "C:\ClaudeCode Projects\Custom_Versification_Editor\plugin\Install-Plugin.ps1"
.EXAMPLE
  & "C:\ClaudeCode Projects\Custom_Versification_Editor\plugin\Install-Plugin.ps1" -Check
#>
param([switch]$Uninstall, [switch]$Check, [switch]$Elevated, [switch]$Pause,
      [string]$ParatextDir, [switch]$NoElevate)

$ErrorActionPreference = 'Stop'
$toolName   = 'Custom Versification Editor'
$folderName = 'CustomVrsEditor'
$pluginFile = 'CustomVrsEditorPlugin.ptxplg'
$exeFile    = 'CustomVrsEditor.exe'   # empty for a plugin without a program
$packageFiles = @($pluginFile)
if ($exeFile) { $packageFiles += $exeFile }
# True for a program that runs as administrator (Parallel Passages Editor): it goes into
# its own folder under Program Files, which only administrators can change, never into the
# Paratext plugins folder, which Paratext makes writable for every user.
$exeProtected = ('0' -eq '1') -and [bool]$exeFile
# The files that go into <Paratext>\plugins\<folder>.
$pluginFolderFiles = @($pluginFile)
if ($exeFile -and -not $exeProtected) { $pluginFolderFiles += $exeFile }
$sourceDir  = Join-Path $PSScriptRoot $folderName
# SHA-256 of each package file, written by make_installers.py.
$expectedHashes = @{ 'CustomVrsEditorPlugin.ptxplg' = 'ec75c8add4c3761413b945b70d997574148457009932e365a634a57b90d6dcbe'; 'CustomVrsEditor.exe' = '480624647923689d863c312ec7a43be3e2a1de978beb9c6ac63f753d019a8321' }
# Windows PowerShell by its real path, never through PATH or the current folder.
$powershellExe = Join-Path ([Environment]::SystemDirectory) 'WindowsPowerShell\v1.0\powershell.exe'

function Test-IsAdmin {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}
$isAdmin = Test-IsAdmin
# Folders typed in by the user are trusted only for tests and reports that run WITHOUT
# administrator rights: an administrator run must never be steered into a folder that a
# non-administrator controls.
$useGivenFolders = ($NoElevate -or $Check) -and -not $isAdmin

# -- Finding Paratext installations --------------------------------------------------

function Get-ParatextCandidates {
    $list = New-Object System.Collections.Generic.List[string]
    if ($useGivenFolders) {
        $given = $ParatextDir
        if ([string]::IsNullOrEmpty($given)) { $given = $env:ParatextInstallDir }
        if (-not [string]::IsNullOrEmpty($given)) {
            foreach ($d in $given.Split(';')) { if ($d.Trim().Length -gt 0) { $list.Add($d.Trim()) } }
            return $list
        }
    }

    # Paratext records its program folders under HKLM\SOFTWARE\Paratext\8 (also for
    # Paratext 9), for example Paratext9_Full_Release_AppPath and
    # Program_Files_Directory_Ptw91 (Beta). Other values there, such as Settings_Directory
    # (the projects folder, writable by every user), are not program folders.
    $roots = New-Object System.Collections.Generic.List[string]
    foreach ($view in [Microsoft.Win32.RegistryView]::Registry32, [Microsoft.Win32.RegistryView]::Registry64) {
        try {
            $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $view)
            $key = $root.OpenSubKey('SOFTWARE\Paratext\8')
            if ($key) {
                foreach ($name in $key.GetValueNames()) {
                    if ($name -notmatch 'AppPath|^Program_Files_Directory') { continue }
                    $value = $key.GetValue($name)
                    if ($value -is [string] -and $value.Length -gt 3) { $list.Add($value) }
                }
                $key.Close()
            }
            # Program Files folders from the machine registry, not from environment
            # variables (a user can override those for their own account).
            $cv = $root.OpenSubKey('SOFTWARE\Microsoft\Windows\CurrentVersion')
            if ($cv) {
                foreach ($name in 'ProgramFilesDir', 'ProgramFilesDir (x86)', 'ProgramW6432Dir') {
                    $value = $cv.GetValue($name)
                    if ($value -is [string] -and $value.Length -gt 3) { $roots.Add($value) }
                }
                $cv.Close()
            }
            $root.Close()
        } catch { }
    }
    foreach ($pf in $roots) {
        if (-not (Test-Path -LiteralPath $pf)) { continue }
        Get-ChildItem -LiteralPath $pf -Directory -Filter 'Paratext*' -ErrorAction SilentlyContinue | ForEach-Object { $list.Add($_.FullName) }
    }
    return $list
}

Add-Type -Namespace PluginInstaller -Name Paths -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
public static extern uint GetLongPathName(string shortPath, System.Text.StringBuilder longPath, uint size);
'@

# Full path with 8.3 short names (like PROGRA~1) expanded, so two spellings of a folder compare equal.
function Get-LongPath([string]$path) {
    $full = [IO.Path]::GetFullPath($path).TrimEnd('\')
    $sb = New-Object System.Text.StringBuilder 1024
    $n = [PluginInstaller.Paths]::GetLongPathName($full, $sb, [uint32]$sb.Capacity)
    if ($n -gt 0 -and $n -lt $sb.Capacity) { return $sb.ToString().TrimEnd('\') }
    return $full
}

# Administrators, SYSTEM and TrustedInstaller.
$trustedSids = @('S-1-5-32-544', 'S-1-5-18', 'S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464')

# True when only administrators can change the folder: owned by a trusted account, no
# write-type permission for anyone else, and not a link. A Paratext program folder under
# Program Files passes; a folder ordinary users can write to (for example the projects
# folder, or a Paratext installed in C:\Paratext9) does not, because the administrator
# run would otherwise start programs that a non-administrator could have put there.
function Test-AdminOnlyFolder([string]$path) {
    try {
        $item = Get-Item -LiteralPath $path -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { return $false }
        $acl = Get-Acl -LiteralPath $path
        if ($trustedSids -notcontains $acl.GetOwner([Security.Principal.SecurityIdentifier]).Value) { return $false }
        # WriteData, AppendData, DeleteSubdirectoriesAndFiles, Delete, ChangePermissions,
        # TakeOwnership, GenericAll, GenericWrite.
        $writeMask = 0x2 -bor 0x4 -bor 0x40 -bor 0x10000 -bor 0x40000 -bor 0x80000 -bor 0x10000000 -bor 0x40000000
        foreach ($r in $acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])) {
            if ($r.AccessControlType -ne [Security.AccessControl.AccessControlType]::Allow) { continue }
            if (($r.PropagationFlags -band [Security.AccessControl.PropagationFlags]::InheritOnly) -ne 0) { continue }
            if (([long][int]$r.FileSystemRights -band $writeMask) -eq 0) { continue }
            if ($trustedSids -notcontains $r.IdentityReference.Value) { return $false }
        }
        return $true
    } catch { return $false }
}

function Find-ParatextInstalls {
    $seen = @{}
    $result = New-Object System.Collections.Generic.List[string]
    foreach ($c in (Get-ParatextCandidates)) {
        try { $full = Get-LongPath ($c.Trim().Trim('"')) } catch { continue }
        $key = $full.ToLowerInvariant()
        if ($seen.ContainsKey($key)) { continue }
        $seen[$key] = $true
        # Paratext 9 has plugin support (PluginInterfaces.dll); Paratext 7 and 8 do not.
        if (-not ((Test-Path -LiteralPath (Join-Path $full 'Paratext.exe')) -and (Test-Path -LiteralPath (Join-Path $full 'PluginInterfaces.dll')))) { continue }
        if (-not $useGivenFolders -and -not (Test-AdminOnlyFolder $full)) {
            Write-Host "Skipped $full : ordinary users can change this folder, so it is not treated as a Paratext installation." -ForegroundColor Yellow
            continue
        }
        $result.Add($full)
    }
    return $result
}

# -- Checks --------------------------------------------------------------------------

function Test-InternetMark([string]$path) {
    return [bool](Get-Item -LiteralPath $path -Stream Zone.Identifier -ErrorAction SilentlyContinue)
}

function Get-Sha256Hex([byte[]]$bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join '') }
    finally { $sha.Dispose() }
}

# Reads a package file and checks it against $expectedHashes. Returns the checked bytes,
# so the caller installs exactly what was checked (nothing can swap the file in between).
function Read-CheckedPackageFile([string]$name) {
    if (-not $expectedHashes.ContainsKey($name)) { throw "No checksum is recorded for $name. The package is incomplete or was changed." }
    $bytes = [IO.File]::ReadAllBytes((Join-Path $sourceDir $name))
    if ((Get-Sha256Hex $bytes) -ne $expectedHashes[$name]) {
        throw "$name does not match its checksum: the package was changed or damaged. Download it again; nothing was installed."
    }
    return ,$bytes
}

# Junctions and symbolic links could redirect an administrator copy or delete elsewhere.
function Assert-NotReparsePoint([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return }
    if (((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to change $path : it is a link (junction or symbolic link) to another place."
    }
}

# Runs in a separate powershell.exe so the loaded assemblies do not stay locked in this one.
# The two paths arrive base64-encoded, so no folder name can break the quoting.
$loadCheckScript = @'
param([string]$PluginB64, [string]$ParatextDirB64)
$ErrorActionPreference = 'Stop'
$Plugin = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($PluginB64))
$ParatextDir = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($ParatextDirB64))
try {
    $installed = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $ParatextDir 'PluginInterfaces.dll')).Version
    $refs = [Reflection.Assembly]::ReflectionOnlyLoadFrom($Plugin).GetReferencedAssemblies() | Where-Object { $_.Name -eq 'PluginInterfaces' }
    foreach ($r in $refs) {
        if ($r.Version -gt $installed) {
            "FAIL This Paratext is too old for the plugin: it needs PluginInterfaces $($r.Version), this Paratext has $installed."
            exit
        }
    }
    [AppDomain]::CurrentDomain.add_AssemblyResolve({
        param($s, $e)
        $p = Join-Path $ParatextDir ((New-Object Reflection.AssemblyName($e.Name)).Name + '.dll')
        if (Test-Path -LiteralPath $p) { return [Reflection.Assembly]::LoadFrom($p) }
        return $null
    })
    # The same two calls Paratext's plugin manager makes.
    $asm = [Reflection.Assembly]::LoadFrom($Plugin)
    $types = @($asm.GetExportedTypes() | Where-Object {
        -not $_.IsAbstract -and -not $_.IsInterface -and
        (@($_.GetInterfaces() | Where-Object { $_.Name -eq 'IParatextPlugin' }).Count -gt 0) -and
        ($_.GetConstructor([Type]::EmptyTypes) -ne $null)
    })
    if ($types.Count -gt 0) { "OK " + (($types | ForEach-Object { $_.FullName }) -join ', ') }
    else { "FAIL The file loads, but contains no Paratext plugin class." }
} catch {
    $ex = $_.Exception
    while ($ex.InnerException) { $ex = $ex.InnerException }
    "FAIL " + $ex.GetType().Name + ": " + $ex.Message
}
'@

# A private folder for the load check. Under an administrator it lives in the Windows
# temp folder with permissions for Administrators and SYSTEM only, so nobody else can
# swap the file that the (administrator) check loads.
function New-PrivateFolder {
    $name = 'PluginCheck_' + [Guid]::NewGuid().ToString('N')
    if ($isAdmin) {
        $sec = New-Object Security.AccessControl.DirectorySecurity
        $sec.SetAccessRuleProtection($true, $false)
        foreach ($sid in 'S-1-5-32-544', 'S-1-5-18') {
            $id = New-Object Security.Principal.SecurityIdentifier $sid
            $sec.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($id, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
        }
        $dir = Join-Path (Join-Path ([Environment]::GetFolderPath('Windows')) 'Temp') $name
        [IO.Directory]::CreateDirectory($dir, $sec) | Out-Null
        return $dir
    }
    $dir = Join-Path ([IO.Path]::GetTempPath()) $name
    [IO.Directory]::CreateDirectory($dir) | Out-Null
    return $dir
}

# Checks a copy of the plugin made in a private folder (the installed copy sits in the
# Paratext plugins folder, which every user can write to).
function Invoke-LoadCheck([byte[]]$pluginBytes, [string]$paratext) {
    $ErrorActionPreference = 'Continue'   # a stray stderr line from the child must not stop the installer
    $dir = $null
    try {
        $dir = New-PrivateFolder
        $copy = Join-Path $dir $pluginFile
        [IO.File]::WriteAllBytes($copy, $pluginBytes)
        $b64 = { param($s) [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($s)) }
        # Passed in memory (-EncodedCommand), never through a script file that another
        # process could replace before the child runs it.
        $command = '& {' + $loadCheckScript + '} -PluginB64 ''' + (& $b64 $copy) + ''' -ParatextDirB64 ''' + (& $b64 $paratext) + ''''
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
        $out = & $powershellExe -NoProfile -NonInteractive -EncodedCommand $encoded 2>&1
        $line = @($out | ForEach-Object { "$_" } | Where-Object { $_ -match '^(OK|FAIL) ' }) | Select-Object -Last 1
        if (-not $line) { $line = 'FAIL The check did not run: ' + (($out | ForEach-Object { "$_" }) -join ' ') }
        return $line
    } catch {
        return 'FAIL The check did not run: ' + $_.Exception.Message
    } finally {
        if ($dir) { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

function Write-CheckResult([string]$label, [string]$result) {
    if ($result -like 'OK *') { Write-Host ("  " + $label + ": Paratext can load it (" + $result.Substring(3) + ")") -ForegroundColor Green }
    else { Write-Host ("  " + $label + ": " + $result.Substring(5)) -ForegroundColor Red }
}

function Update-PluginCache([string]$paratext) {
    # Paratext keeps a plugin cache (plugins\AddIns.store); its own tool rebuilds it.
    # Its output always lists "Problems occurred ... Could not find a valid add-in" for every
    # plugin folder, Paratext's own plugins included (it looks for old-style System.AddIn
    # add-ins, which Paratext 9 plugins are not), so it is shown only if the tool fails.
    # Output is read in memory, not through files in a temp folder.
    # The tool's manifest requires administrator rights, so this runs only in an
    # administrator process, and only for plugins folders that ordinary users cannot change:
    # it rewrites AddIns.store, which a user could otherwise redirect to another file with a
    # link. Paratext 9 does not need it (its plugin manager loads plugins\*\*.ptxplg
    # directly, and the cache it writes lists no Paratext 9 plugin), so shared folders lose
    # nothing. A failure here never stops the rest of the installer.
    $updater = Join-Path $paratext 'UpdateParatextPluginCache.exe'
    if (-not (Test-Path -LiteralPath $updater)) { return }
    if (-not $isAdmin) {
        Write-Host '  Paratext''s plugin list was not updated (that needs administrator rights).' -ForegroundColor Yellow
        return
    }
    $psi = New-Object Diagnostics.ProcessStartInfo $updater
    $psi.WorkingDirectory = $paratext
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    try { $p = [Diagnostics.Process]::Start($psi) }
    catch {
        Write-Host ('  Could not update Paratext''s plugin list: ' + $_.Exception.Message) -ForegroundColor Yellow
        return
    }
    $errTask = $p.StandardError.ReadToEndAsync()
    $stdout = $p.StandardOutput.ReadToEnd()
    $p.WaitForExit()
    if ($p.ExitCode -ne 0) {
        Write-Host ("  UpdateParatextPluginCache.exe reported exit code " + $p.ExitCode + ":") -ForegroundColor Yellow
        foreach ($text in $stdout, $errTask.Result) {
            foreach ($l in ($text -split "`r?`n")) { if ($l.Trim()) { Write-Host ("    " + $l) } }
        }
    } else {
        Write-Host '  Updated Paratext''s plugin list.'
    }
}

function Get-RunningParatext($installs) {
    $running = New-Object System.Collections.Generic.List[string]
    foreach ($proc in @(Get-Process -Name Paratext -ErrorAction SilentlyContinue)) {
        $path = $null
        try { $path = $proc.Path } catch { }
        if ([string]::IsNullOrEmpty($path)) { $running.Add('(a Paratext window)'); continue }
        $dir = Get-LongPath ([IO.Path]::GetDirectoryName($path))
        foreach ($i in $installs) { if ([string]::Equals($dir, $i, [StringComparison]::OrdinalIgnoreCase)) { $running.Add($i) } }
    }
    return $running
}

function Assert-ParatextClosed($installs) {
    $running = @(Get-RunningParatext $installs)
    if ($running.Count -gt 0) {
        throw ('Paratext is running (' + (($running | Select-Object -Unique) -join ', ') + '). Close Paratext, then run this again.')
    }
}

# A mapped network drive (Z:) is not visible to the administrator window that Windows
# opens, so the installer would silently fail to start there.
function Assert-NotMappedDrive {
    $root = [IO.Path]::GetPathRoot($PSCommandPath)
    if ($root -match '^[A-Za-z]:\\$') {
        $drive = New-Object IO.DriveInfo $root
        if ($drive.DriveType -eq [IO.DriveType]::Network) {
            throw "This package is on the network drive $root. Copy or extract it to a folder on this computer (for example Downloads), then run it again from there."
        }
    }
}

# -- Where files go ----------------------------------------------------------------------

# Program Files from the machine registry (the 64-bit folder on 64-bit Windows), not from
# environment variables, which a user can set for their own account.
function Get-ProgramFilesFolder {
    foreach ($view in [Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32) {
        try {
            $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $view)
            $cv = $root.OpenSubKey('SOFTWARE\Microsoft\Windows\CurrentVersion')
            if ($cv) {
                foreach ($name in 'ProgramW6432Dir', 'ProgramFilesDir') {
                    $value = $cv.GetValue($name)
                    if ($value -is [string] -and (Test-Path -LiteralPath $value)) { $cv.Close(); $root.Close(); return $value.TrimEnd('\') }
                }
                $cv.Close()
            }
            $root.Close()
        } catch { }
    }
    throw 'The Program Files folder was not found in the registry.'
}

# Paratext's own installer gives every user Full Control of <Paratext 9>\plugins. Such an
# install is "shared": its plugin files are written from the normal window, because an
# administrator process writing into a folder that users control could be redirected
# elsewhere by a junction swapped in at the right moment. Installs whose plugins folder
# only administrators can change (or that have none yet) are handled as administrator.
function Test-SharedPluginsFolder([string]$install) {
    $plugins = Join-Path $install 'plugins'
    if (-not (Test-Path -LiteralPath $plugins)) { return $false }
    return -not (Test-AdminOnlyFolder $plugins)
}

# Deletes a file of this plugin (link-checked), so a new one can be created in its place.
function Remove-OwnFile([string]$folder, [string]$name) {
    $file = Join-Path $folder $name
    if (-not (Test-Path -LiteralPath $file)) { return $false }
    Assert-NotReparsePoint $folder
    Assert-NotReparsePoint $file
    Remove-Item -LiteralPath $file -Force
    return $true
}

# Writes checked bytes as a NEW file: an existing entry is deleted first, since it could be
# a link or a hard link to some other file. The new file carries no "from the internet"
# mark, which Paratext would refuse.
function Write-OwnFile([string]$folder, [string]$name, [byte[]]$bytes) {
    Remove-OwnFile $folder $name | Out-Null
    Assert-NotReparsePoint $folder
    $dest = Join-Path $folder $name
    $fs = New-Object IO.FileStream($dest, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $fs.Write($bytes, 0, $bytes.Length) } finally { $fs.Dispose() }
    if (Test-InternetMark $dest) { Unblock-File -LiteralPath $dest }
    if (Test-InternetMark $dest) { throw "Could not remove the 'from the internet' mark from $dest" }
    Write-Host ('  Installed ' + $dest)
}

function Install-PluginFiles($installs, $packageBytes) {
    foreach ($i in $installs) {
        Write-Host $i
        $plugins = Join-Path $i 'plugins'
        $target = Join-Path $plugins $folderName
        foreach ($p in $i, $plugins, $target) { Assert-NotReparsePoint $p }
        if (-not (Test-Path -LiteralPath $target)) { New-Item -ItemType Directory -Path $target | Out-Null }
        foreach ($f in $pluginFolderFiles) { Write-OwnFile $target $f $packageBytes[$f] }
        # A protected program left in the plugins folder by an older version is removed.
        if ($exeProtected -and (Remove-OwnFile $target $exeFile)) { Write-Host "  Removed the old copy of $exeFile from the plugins folder." }
    }
}

# Only this plugin's own files are deleted, one by one: never a recursive delete.
function Remove-PluginFiles($installs) {
    foreach ($i in $installs) {
        $plugins = Join-Path $i 'plugins'
        $target = Join-Path $plugins $folderName
        if (-not (Test-Path -LiteralPath $target)) { Write-Host "Not installed in $i"; continue }
        foreach ($p in $i, $plugins, $target) { Assert-NotReparsePoint $p }
        foreach ($f in $packageFiles) { Remove-OwnFile $target $f | Out-Null }
        $left = @(Get-ChildItem -LiteralPath $target -Force)
        if ($left.Count -eq 0) {
            Assert-NotReparsePoint $target
            Remove-Item -LiteralPath $target -Force -Confirm:$false
            Write-Host "Removed from $i" -ForegroundColor Green
        } else {
            Write-Host ("Removed from $i (the folder $target was kept: it also holds " + (($left | ForEach-Object { $_.Name }) -join ', ') + ')') -ForegroundColor Green
        }
    }
}

# The protected program's folder must be one that only administrators can change, under a
# parent that only administrators can change (Program Files), so nobody can swap the file
# or the folder. Test runs (-NoElevate with given folders) use a scratch folder instead.
function Install-ProtectedExe([byte[]]$bytes) {
    $parent = Split-Path $protectedExeDir
    if (-not $useGivenFolders -and -not (Test-AdminOnlyFolder $parent)) { throw "$parent can be changed by ordinary users; $exeFile is not installed there." }
    Assert-NotReparsePoint $protectedExeDir
    if (-not (Test-Path -LiteralPath $protectedExeDir)) { New-Item -ItemType Directory -Path $protectedExeDir | Out-Null }
    if (-not $useGivenFolders -and -not (Test-AdminOnlyFolder $protectedExeDir)) { throw "$protectedExeDir can be changed by ordinary users; $exeFile is not installed there." }
    Write-OwnFile $protectedExeDir $exeFile $bytes
}

function Remove-ProtectedExe {
    if (-not (Test-Path -LiteralPath $protectedExeDir)) { return }
    Remove-OwnFile $protectedExeDir $exeFile | Out-Null
    if (@(Get-ChildItem -LiteralPath $protectedExeDir -Force).Count -eq 0) {
        Assert-NotReparsePoint $protectedExeDir
        Remove-Item -LiteralPath $protectedExeDir -Force -Confirm:$false
    }
    Write-Host "Removed $protectedExeDir" -ForegroundColor Green
}

# -- After the files are in place: checksums and the load check -------------------------

function Test-InstalledFile([string]$path, [string]$name) {
    if (-not (Test-Path -LiteralPath $path)) { Write-Host "  $name was not installed." -ForegroundColor Red; return $false }
    if ((Get-Sha256Hex ([IO.File]::ReadAllBytes($path))) -ne $expectedHashes[$name]) {
        Write-Host "  The installed $name differs from the package." -ForegroundColor Red; return $false
    }
    return $true
}

function Complete-Install($installs, $packageBytes) {
    $failed = 0
    if ($exeProtected) {
        Write-Host $protectedExeDir
        if (Test-InstalledFile (Join-Path $protectedExeDir $exeFile) $exeFile) { Write-Host "  $exeFile is in place." -ForegroundColor Green } else { $failed++ }
    }
    foreach ($i in $installs) {
        Write-Host $i
        $target = Join-Path (Join-Path $i 'plugins') $folderName
        $ok = $true
        foreach ($f in $pluginFolderFiles) { if (-not (Test-InstalledFile (Join-Path $target $f) $f)) { $ok = $false } }
        if (-not $ok) { $failed++; continue }
        $result = Invoke-LoadCheck $packageBytes[$pluginFile] $i
        Write-CheckResult 'Check' $result
        if ($result -notlike 'OK *') { $failed++ }
    }
    Write-Host ''
    if ($failed -eq 0) {
        Write-Host ("Installed in " + $installs.Count + " Paratext installation(s).") -ForegroundColor Green
        Write-Host "Start Paratext, open a project and look for $toolName... on the Tools menu."
    } else {
        Write-Host ("Something is missing or wrong in " + $failed + " place(s): see the messages above.") -ForegroundColor Red
        $script:exitCode = 1
    }
}

# -- Main ------------------------------------------------------------------------------

$exitCode = 0
$keepOpen = $Pause
try {
    $installs = @(Find-ParatextInstalls)
    if ($installs.Count -eq 0) {
        throw 'No Paratext 9 installation was found (looked in the Paratext registry entries and in Program Files). Reinstalling Paratext 9 records its folder again.'
    }
    $protectedExeDir = $null
    if ($exeProtected) {
        if ($useGivenFolders) { $protectedExeDir = Join-Path (Join-Path (Split-Path $installs[0]) 'ProgramFiles') $folderName }
        else { $protectedExeDir = Join-Path (Get-ProgramFilesFolder) $folderName }
    }
    $shared = @($installs | Where-Object { Test-SharedPluginsFolder $_ })
    $adminOnly = @($installs | Where-Object { $shared -notcontains $_ })
    if (-not $Elevated) {
        Write-Host "$toolName plugin - Paratext installations found:"
        foreach ($i in $installs) { Write-Host "  $i" }
        Write-Host ''
    }

    if ($Check) {
        $packageOk = @{}
        foreach ($f in $packageFiles) {
            $src = Join-Path $sourceDir $f
            if (Test-Path -LiteralPath $src) {
                try { $packageOk[$f] = Read-CheckedPackageFile $f; Write-Host "Package file $f matches its checksum." }
                catch { Write-Host $_.Exception.Message -ForegroundColor Red }
                if (Test-InternetMark $src) { Write-Host "Package file $f carries the 'from the internet' mark (the installed copy will not)." -ForegroundColor Yellow }
            } else { Write-Host "Package file missing: $src" -ForegroundColor Yellow }
        }
        if ($exeProtected) {
            $exe = Join-Path $protectedExeDir $exeFile
            Write-Host $protectedExeDir
            if (-not (Test-Path -LiteralPath $exe)) { Write-Host "  $exeFile is not installed here." -ForegroundColor Red }
            else {
                if (-not $useGivenFolders -and -not (Test-AdminOnlyFolder $protectedExeDir)) { Write-Host '  WARNING: ordinary users can change this folder. Run Install-Plugin.cmd again.' -ForegroundColor Red }
                if ((Get-Sha256Hex ([IO.File]::ReadAllBytes($exe))) -ne $expectedHashes[$exeFile]) { Write-Host "  The installed $exeFile differs from this package (another version, or changed)." -ForegroundColor Yellow }
                else { Write-Host "  $exeFile is installed and matches this package." }
            }
        }
        foreach ($i in $installs) {
            Write-Host $i
            $dest = Join-Path (Join-Path (Join-Path $i 'plugins') $folderName) $pluginFile
            if (-not (Test-Path -LiteralPath $dest)) {
                Write-Host '  Not installed here.'
                # Only a package copy that passed its checksum is load-checked.
                if ($packageOk.ContainsKey($pluginFile)) { Write-CheckResult 'Package copy' (Invoke-LoadCheck $packageOk[$pluginFile] $i) }
                continue
            }
            Write-Host '  Installed.'
            $installedBytes = [IO.File]::ReadAllBytes($dest)
            if ((Get-Sha256Hex $installedBytes) -ne $expectedHashes[$pluginFile]) {
                Write-Host '  The installed plugin differs from this package (another version, or changed).' -ForegroundColor Yellow
            }
            if (Test-InternetMark $dest) {
                Write-Host "  BLOCKED: the installed plugin carries the 'from the internet' mark, so Paratext silently skips it. Run Install-Plugin.cmd again to fix it." -ForegroundColor Red
            } else {
                Write-CheckResult 'Installed copy' (Invoke-LoadCheck $installedBytes $i)
            }
            if ($exeFile -and -not $exeProtected) {
                $exe = Join-Path (Split-Path $dest) $exeFile
                if (-not (Test-Path -LiteralPath $exe)) { Write-Host "  Missing $exeFile next to the plugin." -ForegroundColor Red }
                elseif (Test-InternetMark $exe) { Write-Host "  $exeFile carries the 'from the internet' mark (Windows may warn when it starts)." -ForegroundColor Yellow }
            }
        }
    } else {
        Assert-ParatextClosed $installs
        $packageBytes = @{}
        if (-not $Uninstall) {
            # Only the listed package files are installed, each checked against its checksum first.
            foreach ($f in $packageFiles) {
                if (-not (Test-Path -LiteralPath (Join-Path $sourceDir $f))) { throw "Missing $f in $sourceDir" }
                $packageBytes[$f] = Read-CheckedPackageFile $f
            }
        }

        if ($Elevated) {
            # The administrator window: only folders that ordinary users cannot change, then
            # Paratext's plugin-list tool (its manifest requires administrator rights).
            if ($Uninstall) {
                Remove-PluginFiles $adminOnly
                if ($exeProtected) { Remove-ProtectedExe }
            } else {
                Install-PluginFiles $adminOnly $packageBytes
                if ($exeProtected) { Install-ProtectedExe $packageBytes[$exeFile] }
            }
            foreach ($i in $adminOnly) { Write-Host $i; Update-PluginCache $i }
        } elseif ($isAdmin -or $NoElevate) {
            # Already an administrator (or a test run): everything in this window. An
            # administrator must not write into a plugins folder that ordinary users control
            # (a link swapped in at the right moment would redirect the write or delete).
            if ($isAdmin -and $shared.Count -gt 0) {
                throw ('This window runs as administrator, and every user can change the plugins folder of ' + ($shared -join ', ') +
                    ', so the plugin must be copied there without administrator rights. Close this window and double-click ' +
                    $(if ($Uninstall) { 'Uninstall-Plugin.cmd' } else { 'Install-Plugin.cmd' }) +
                    ' (do not choose "Run as administrator"); it asks for administrator rights only where they are needed. Nothing was changed.')
            }
            if ($Uninstall) {
                Remove-PluginFiles $installs
                if ($exeProtected) { Remove-ProtectedExe }
                foreach ($i in $adminOnly) { Write-Host $i; Update-PluginCache $i }
            } else {
                Install-PluginFiles $installs $packageBytes
                if ($exeProtected) { Install-ProtectedExe $packageBytes[$exeFile] }
                foreach ($i in $adminOnly) { Write-Host $i; Update-PluginCache $i }
                Write-Host ''
                Complete-Install $installs $packageBytes
            }
        } elseif ($adminOnly.Count -eq 0 -and -not $exeProtected) {
            # Only shared plugins folders: no administrator rights needed at all.
            if ($Uninstall) {
                Remove-PluginFiles $shared
                Write-Host ''
                Write-Host ("Removed from " + $installs.Count + " Paratext installation(s).") -ForegroundColor Green
            } else {
                Install-PluginFiles $shared $packageBytes
                Write-Host ''
                Complete-Install $installs $packageBytes
            }
        } else {
            Assert-NotMappedDrive
            # 1. This (normal) window: the shared plugins folders.
            if ($Uninstall) { Remove-PluginFiles $shared } else { Install-PluginFiles $shared $packageBytes }
            # 2. The administrator window: folders only administrators can change.
            Write-Host ''
            Write-Host 'Administrator rights are needed for folders that only administrators can change. Asking Windows...'
            $argLine = '-NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath + '" -Elevated'
            if ($Uninstall) { $argLine += ' -Uninstall' }
            $child = $null
            try { $child = Start-Process -FilePath $powershellExe -Verb RunAs -ArgumentList $argLine -Wait -PassThru }
            catch {
                if ($Uninstall) { throw 'Administrator permission was not given: the plugin was removed from the shared plugins folder(s), but not from the folders that only administrators can change. Run Uninstall-Plugin.cmd again.' }
                throw 'Administrator permission was not given: the plugin is not completely installed. Run Install-Plugin.cmd again.'
            }
            if ($child.ExitCode -ne 0) { throw 'The administrator window reported a problem (see its messages); the plugin may not be completely installed or removed.' }
            Write-Host 'The administrator window finished.'
            Write-Host ''
            # 3. This window again: checksums of what is installed, then the load check.
            if ($Uninstall) { Write-Host ("Removed from " + $installs.Count + " Paratext installation(s).") -ForegroundColor Green }
            else { Complete-Install $installs $packageBytes }
        }
    }
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    $exitCode = 1
    if ($Elevated) { $keepOpen = $true }   # keep the administrator window open to read the error
}
if ($keepOpen) { Read-Host 'Press Enter to close' | Out-Null }
exit $exitCode
