#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Provisions a local IIS site with its own Application Pool, HTTPS binding(s)
    (self-signed certificates), HTTP binding(s), and optional hosts file entries -
    all driven by a companion JSON config file.

.DESCRIPTION
    Run this script elevated with no arguments and it will look for a JSON file
    named "site-config.json" in the SAME FOLDER as the script and read every
    setting from it. You can also point it at a different JSON file with
    -ConfigPath.

    Expected JSON shape (extra/optional keys shown with their defaults):

    {
      "IIS-Site-Name": "Ctmdigitl.Web",
      "App-Pool-Name": "Ctmdigitl.Web",
      "IIS-App-Pool-Dot-Net-Version": "v4.0",
      "bindings": [ "ctmdigitl.localhost" ],

      "Physical-Path": "D:\\Projects\\...\\Ctmdigitl.Web",           // optional, defaults to the folder containing this JSON file
      "Http-Port": 80,                                          // optional, default 80
      "Https-Port": 443,                                        // optional, default 443
      "Add-Hosts-Entries": true,                                // optional, default true
      "Hosts-Section-Heading": "LocalApplications"              // optional, default shown - entries are grouped under "# <heading>"
    }

    "bindings" is a list of hostnames. Each hostname gets its own HTTP binding,
    its own self-signed HTTPS certificate + HTTPS binding (SNI), and (if
    Add-Hosts-Entries is true / omitted) its own 127.0.0.1 line in the hosts file.

    The script is idempotent: re-running it reuses existing App Pools, sites,
    certificates, bindings, and hosts entries instead of duplicating them.

.PARAMETER ConfigPath
    Path to the JSON config file. Defaults to "site-config.json" next to this
    script (i.e. $PSScriptRoot\site-config.json).

.EXAMPLE
    .\New-IisSslSite.ps1
    (reads .\site-config.json automatically)

.EXAMPLE
    .\New-IisSslSite.ps1 -ConfigPath "C:\configs\api-site.json"

.NOTES
    Run this in an elevated (Administrator) PowerShell session.
    Requires the IIS Web-Server role/feature; the script attempts to enable it
    if missing.
#>

[CmdletBinding()]
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot "site-config.json")
)

$ErrorActionPreference = "Stop"

function Write-Step($message) {
    Write-Host "==> $message" -ForegroundColor Cyan
}

function Write-Info($message) {
    Write-Host "    $message" -ForegroundColor DarkGray
}

function Add-HostsEntryUnderHeading {
    <#
        Adds "127.0.0.1  <HostName>" to the Windows hosts file, grouped under a
        "# <Heading>" comment section. If the heading doesn't exist yet, it's
        created (with the entry beneath it). If it exists, the entry is inserted
        at the end of that section (before the next blank line, next heading, or EOF).
        Does nothing if an entry for the hostname already exists anywhere in the file.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$HostName,
        [string]$Heading = "LocalApplications"
    )

    $hostsPath = "$env:WinDir\System32\drivers\etc\hosts"
    $headingLine = "# $Heading"
    $entryLine = "127.0.0.1`t$HostName"

    $lines = @(Get-Content -Path $hostsPath -ErrorAction SilentlyContinue)

    $alreadyPresent = $lines | Where-Object {
        $_ -match "^\s*127\.0\.0\.1\s+$([regex]::Escape($HostName))\s*$"
    }
    if ($alreadyPresent) {
        Write-Info "Hosts file already contains an entry for '$HostName'."
        return
    }

    $headingIndex = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -ieq $headingLine) {
            $headingIndex = $i
            break
        }
    }

    $newLines = New-Object System.Collections.Generic.List[string]

    if ($headingIndex -eq -1) {
        # Heading doesn't exist yet - append it (with a blank-line separator if needed) plus the entry.
        if ($lines.Count -gt 0) { $newLines.AddRange([string[]]$lines) }
        if ($newLines.Count -gt 0 -and $newLines[$newLines.Count - 1].Trim() -ne "") {
            $newLines.Add("")
        }
        $newLines.Add($headingLine)
        $newLines.Add($entryLine)
        Set-Content -Path $hostsPath -Value $newLines -Encoding ASCII
        Write-Info "Created '$headingLine' section and added '127.0.0.1  $HostName' to hosts file."
    }
    else {
        # Heading exists - find the end of its section (next blank line, next '#' heading, or EOF).
        $insertAt = $headingIndex + 1
        while ($insertAt -lt $lines.Count -and $lines[$insertAt].Trim() -ne "" -and -not $lines[$insertAt].Trim().StartsWith("#")) {
            $insertAt++
        }

        $newLines.AddRange([string[]]$lines[0..($insertAt - 1)])
        $newLines.Add($entryLine)
        if ($insertAt -le $lines.Count - 1) {
            $newLines.AddRange([string[]]$lines[$insertAt..($lines.Count - 1)])
        }
        Set-Content -Path $hostsPath -Value $newLines -Encoding ASCII
        Write-Info "Added '127.0.0.1  $HostName' to hosts file under '$headingLine'."
    }
}

# ----------------------------------------------------------------------------
# 0a. Relaunch under Windows PowerShell 5.1 if running under PowerShell 7+ (Core)
# ----------------------------------------------------------------------------
# The WebAdministration module has no PS7-native build. PS7 loads it through a
# compatibility session that proxies cmdlets to a hidden Windows PowerShell 5.1
# process - but PSDrives (like IIS:\) cannot be proxied that way, so any
# "IIS:\..." path fails with "Cannot find drive" even though the module loads.
# Simplest fix: hop over to real Windows PowerShell 5.1 automatically.
if ($PSVersionTable.PSEdition -eq "Core") {
    Write-Host "==> Detected PowerShell $($PSVersionTable.PSVersion) (Core). The IIS:\ drive doesn't work reliably under PowerShell 7 - relaunching under Windows PowerShell 5.1..." -ForegroundColor Yellow

    $winPsPath = Join-Path $env:WinDir "System32\WindowsPowerShell\v1.0\powershell.exe"
    if (-not (Test-Path $winPsPath)) {
        throw "Windows PowerShell 5.1 (powershell.exe) was not found at '$winPsPath'. Please run this script manually from Windows PowerShell 5.1 instead of PowerShell 7."
    }

    & $winPsPath -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -ConfigPath $ConfigPath
    exit $LASTEXITCODE
}

# ----------------------------------------------------------------------------
# 0b. Verify prerequisites: elevation + IIS management module
# ----------------------------------------------------------------------------
Write-Step "Checking prerequisites"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    throw "This script must be run from an elevated (Administrator) PowerShell session."
}

if (-not (Get-Module -ListAvailable -Name WebAdministration)) {
    Write-Info "WebAdministration module not found. Attempting to enable IIS management tools..."

    $iisFeatures = @(
        "IIS-WebServerRole", "IIS-WebServer", "IIS-CommonHttpFeatures", "IIS-ManagementConsole",
        "IIS-ManagementScriptingTools", "IIS-HttpErrors", "IIS-Security", "IIS-RequestFiltering",
        "IIS-StaticContent", "IIS-DefaultDocument", "IIS-ASPNET45", "IIS-NetFxExtensibility45",
        "IIS-ISAPIExtensions", "IIS-ISAPIFilter"
    )

    $enabledOk = $false
    try {
        Enable-WindowsOptionalFeature -Online -FeatureName $iisFeatures -All -NoRestart -ErrorAction Stop | Out-Null
        $enabledOk = $true
    }
    catch {
        Write-Info "Enable-WindowsOptionalFeature cmdlet failed ($($_.Exception.Message)). This is a known issue under PowerShell 7 - falling back to dism.exe directly..."

        foreach ($feature in $iisFeatures) {
            $dismArgs = @("/online", "/enable-feature", "/featurename:$feature", "/all", "/norestart", "/quiet")
            $proc = Start-Process -FilePath "dism.exe" -ArgumentList $dismArgs -Wait -NoNewWindow -PassThru

            # DISM exit codes: 0 = success, 3010 = success but reboot required. Anything else is a real failure.
            if ($proc.ExitCode -ne 0 -and $proc.ExitCode -ne 3010) {
                throw "dism.exe failed to enable feature '$feature' (exit code $($proc.ExitCode)). Try running this script from an elevated Windows PowerShell 5.1 session instead of PowerShell 7, or enable IIS manually via 'Turn Windows features on or off'."
            }
        }
        $enabledOk = $true
    }

    if (-not $enabledOk) {
        throw "IIS / WebAdministration module is not available and could not be enabled automatically. Install the 'Web Server (IIS)' role/feature first."
    }

    Write-Info "IIS features enabled. You may need to close and reopen this PowerShell session for the WebAdministration module to be detected."
}

Import-Module WebAdministration -ErrorAction Stop

# ----------------------------------------------------------------------------
# 1. Load and validate JSON config
# ----------------------------------------------------------------------------
Write-Step "Loading configuration"

if (-not (Test-Path $ConfigPath)) {
    throw "Config file not found at '$ConfigPath'. Place a JSON file next to this script (default name: site-config.json) or pass -ConfigPath."
}

Write-Info "Reading '$ConfigPath'"

try {
    $config = Get-Content -Path $ConfigPath -Raw | ConvertFrom-Json -ErrorAction Stop
}
catch {
    throw "Failed to parse JSON in '$ConfigPath'. Check for syntax errors (trailing commas, missing quotes, etc). Original error: $($_.Exception.Message)"
}

# Required fields
$requiredFields = @("IIS-Site-Name", "App-Pool-Name", "IIS-App-Pool-Dot-Net-Version", "bindings")
foreach ($field in $requiredFields) {
    if (-not ($config.PSObject.Properties.Name -contains $field)) {
        throw "Config file is missing required field '$field'."
    }
}

$SiteName      = $config.'IIS-Site-Name'
$AppPoolName   = $config.'App-Pool-Name'
$DotNetVersion = $config.'IIS-App-Pool-Dot-Net-Version'
$Bindings      = @($config.bindings)

if ([string]::IsNullOrWhiteSpace($SiteName))    { throw "'IIS-Site-Name' cannot be empty in the config file." }
if ([string]::IsNullOrWhiteSpace($AppPoolName)) { throw "'App-Pool-Name' cannot be empty in the config file." }
if ($Bindings.Count -eq 0)                      { throw "'bindings' must contain at least one hostname." }
if ($DotNetVersion -notin @("v4.0", "v2.0", "")) {
    throw "'IIS-App-Pool-Dot-Net-Version' must be 'v4.0', 'v2.0', or '' (No Managed Code). Got: '$DotNetVersion'"
}

# Optional fields with sensible defaults
$configDir = Split-Path -Parent (Resolve-Path $ConfigPath)

$PhysicalPath = if ($config.PSObject.Properties.Name -contains "Physical-Path" -and $config.'Physical-Path') {
    $config.'Physical-Path'
} else {
    $configDir
}

$HttpPort = if ($config.PSObject.Properties.Name -contains "Http-Port" -and $config.'Http-Port') {
    [int]$config.'Http-Port'
} else {
    80
}

$HttpsPort = if ($config.PSObject.Properties.Name -contains "Https-Port" -and $config.'Https-Port') {
    [int]$config.'Https-Port'
} else {
    443
}

$AddHostsEntries = if ($config.PSObject.Properties.Name -contains "Add-Hosts-Entries") {
    [bool]$config.'Add-Hosts-Entries'
} else {
    $true
}

$HostsSectionHeading = if ($config.PSObject.Properties.Name -contains "Hosts-Section-Heading" -and $config.'Hosts-Section-Heading') {
    $config.'Hosts-Section-Heading'
} else {
    "LocalApplications"
}

Write-Info "Site name:      $SiteName"
Write-Info "App pool:       $AppPoolName"
$dotNetVersionDisplay = if ($DotNetVersion -eq '') { '(No Managed Code)' } else { $DotNetVersion }
Write-Info ".NET version:   $dotNetVersionDisplay"
Write-Info "Bindings:       $($Bindings -join ', ')"
Write-Info "Physical path:  $PhysicalPath"
Write-Info "HTTP port:      $HttpPort"
Write-Info "HTTPS port:     $HttpsPort"
Write-Info "Add hosts entries: $AddHostsEntries"

# ----------------------------------------------------------------------------
# 2. Application Pool
# ----------------------------------------------------------------------------
Write-Step "Configuring Application Pool '$AppPoolName'"

if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName | Out-Null
    Write-Info "Created App Pool '$AppPoolName'."
}
else {
    Write-Info "App Pool '$AppPoolName' already exists. Reusing it."
}

Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value $DotNetVersion
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name startMode -Value "AlwaysRunning"
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.idleTimeout -Value ([TimeSpan]::FromMinutes(0))
$runtimeDisplay = if ($DotNetVersion -eq '') { 'No Managed Code' } else { $DotNetVersion }
Write-Info "Runtime version set to '$runtimeDisplay', AlwaysRunning enabled."

# ----------------------------------------------------------------------------
# 3. Physical path + placeholder content
# ----------------------------------------------------------------------------
Write-Step "Preparing physical path '$PhysicalPath'"

if (-not (Test-Path $PhysicalPath)) {
    New-Item -ItemType Directory -Path $PhysicalPath -Force | Out-Null
    Write-Info "Created folder."
}

$indexFile = Join-Path $PhysicalPath "index.html"
$folderHasContent = (Get-ChildItem -Path $PhysicalPath -Force -ErrorAction SilentlyContinue | Select-Object -First 1)

if (-not $folderHasContent -and -not (Test-Path $indexFile)) {
    @"
<!DOCTYPE html>
<html>
<head><title>$SiteName</title></head>
<body>
    <h1>$SiteName is running.</h1>
    <p>Served from $PhysicalPath</p>
</body>
</html>
"@ | Out-File -FilePath $indexFile -Encoding utf8
    Write-Info "Folder was empty - created placeholder index.html."
}
else {
    Write-Info "Folder already has content - leaving it as-is."
}

# ----------------------------------------------------------------------------
# 4. Site (created with the first binding as its initial host header)
# ----------------------------------------------------------------------------
Write-Step "Configuring site '$SiteName'"

$primaryHost = $Bindings[0]

if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    New-Website -Name $SiteName -PhysicalPath $PhysicalPath -ApplicationPool $AppPoolName `
        -Port $HttpPort -HostHeader $primaryHost -Force | Out-Null
    Write-Info "Created site '$SiteName'."
}
else {
    Write-Info "Site '$SiteName' already exists. Reusing it."
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
}

# ----------------------------------------------------------------------------
# 5. Per-hostname: certificate + HTTP/HTTPS bindings + hosts entry
# ----------------------------------------------------------------------------
foreach ($HostName in $Bindings) {

    Write-Step "Configuring bindings for '$HostName'"

    # --- HTTP binding ---
    $existingHttp = Get-WebBinding -Name $SiteName -Protocol "http" | Where-Object {
        $_.bindingInformation -eq "*:$($HttpPort):$HostName"
    }
    if (-not $existingHttp) {
        New-WebBinding -Name $SiteName -Protocol "http" -Port $HttpPort -HostHeader $HostName
        Write-Info "Added HTTP binding on port $HttpPort."
    }
    else {
        Write-Info "HTTP binding on port $HttpPort already present."
    }

    # --- Self-signed certificate ---
    $cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object {
        $_.Subject -eq "CN=$HostName" -and $_.NotAfter -gt (Get-Date)
    } | Sort-Object NotAfter -Descending | Select-Object -First 1

    if (-not $cert) {
        $cert = New-SelfSignedCertificate -DnsName $HostName -CertStoreLocation "Cert:\LocalMachine\My" `
            -FriendlyName "$SiteName self-signed ($HostName)" -NotAfter (Get-Date).AddYears(5)
        Write-Info "Created new self-signed certificate (thumbprint $($cert.Thumbprint))."

        $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "LocalMachine")
        $rootStore.Open("ReadWrite")
        $rootStore.Add($cert)
        $rootStore.Close()
        Write-Info "Added certificate to LocalMachine Trusted Root store."
    }
    else {
        Write-Info "Reusing existing valid certificate (thumbprint $($cert.Thumbprint))."
    }

    # --- HTTPS binding ---
    $existingHttps = Get-WebBinding -Name $SiteName -Protocol "https" | Where-Object {
        $_.bindingInformation -eq "*:$($HttpsPort):$HostName"
    }
    if (-not $existingHttps) {
        New-WebBinding -Name $SiteName -Protocol "https" -Port $HttpsPort -HostHeader $HostName -SslFlags 1
        Write-Info "Added HTTPS binding on port $HttpsPort (SNI enabled)."
    }
    else {
        Write-Info "HTTPS binding on port $HttpsPort already present."
    }

    $sslBinding = Get-WebBinding -Name $SiteName -Protocol "https" | Where-Object {
        $_.bindingInformation -eq "*:$($HttpsPort):$HostName"
    }
    $sslBinding.AddSslCertificate($cert.Thumbprint, "my")
    Write-Info "Bound certificate $($cert.Thumbprint) to https://$($HostName):$HttpsPort"

    # --- Hosts file entry ---
    if ($AddHostsEntries) {
        Add-HostsEntryUnderHeading -HostName $HostName -Heading $HostsSectionHeading
    }
}

# ----------------------------------------------------------------------------
# Done
# ----------------------------------------------------------------------------
Write-Step "Done"
Write-Host ""
Write-Host "Site:          $SiteName"
Write-Host "App Pool:      $AppPoolName"
Write-Host "Physical path: $PhysicalPath"
foreach ($HostName in $Bindings) {
    Write-Host "  http://$($HostName):$HttpPort"
    Write-Host "  https://$($HostName):$HttpsPort"
}
Write-Host ""