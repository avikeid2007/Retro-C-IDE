# download-tcc.ps1 — Downloads TCC compiler at build time so it's bundled with the app
$ErrorActionPreference = "Stop"
$tccDir = Join-Path $PSScriptRoot "BundledTcc"

if (Test-Path (Join-Path $tccDir "tcc.exe")) {
    Write-Host "TCC already present in BundledTcc\"
    exit 0
}

Write-Host "Downloading TCC 0.9.27 (win64)..."

$urls = @(
    "https://download.savannah.gnu.org/releases/tinycc/tcc-0.9.27-win64-bin.zip",
    "https://download.savannah.nongnu.org/releases/tinycc/tcc-0.9.27-win64-bin.zip",
    "http://download.savannah.gnu.org/releases/tinycc/tcc-0.9.27-win64-bin.zip"
)

$tempZip = Join-Path $env:TEMP "tcc-build-download.zip"
$tempDir = Join-Path $env:TEMP "tcc-build-extract"

$downloaded = $false
foreach ($url in $urls) {
    try {
        Write-Host "  Trying $url ..."
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
        $wc = New-Object System.Net.WebClient
        $wc.DownloadFile($url, $tempZip)
        $downloaded = $true
        Write-Host "  Downloaded OK."
        break
    } catch {
        Write-Host "  Failed: $_"
    }
}

if (-not $downloaded) {
    Write-Error "Could not download TCC from any mirror."
    exit 1
}

Write-Host "Extracting..."
Add-Type -Assembly System.IO.Compression.FileSystem
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
[System.IO.Compression.ZipFile]::ExtractToDirectory($tempZip, $tempDir)

# Find the folder containing tcc.exe
$tccExe = Get-ChildItem $tempDir -Recurse -Filter "tcc.exe" | Select-Object -First 1
if (-not $tccExe) {
    Write-Error "tcc.exe not found in downloaded archive."
    exit 1
}

$srcDir = $tccExe.DirectoryName
Write-Host "Found TCC at: $srcDir"

# Copy to BundledTcc
if (Test-Path $tccDir) { Remove-Item $tccDir -Recurse -Force }
Copy-Item $srcDir $tccDir -Recurse

# Verify libtcc.dll
$libtcc = Join-Path $tccDir "libtcc.dll"
if (-not (Test-Path $libtcc)) {
    # Search extracted archive for libtcc.dll and copy it
    $dll = Get-ChildItem $tempDir -Recurse -Filter "libtcc.dll" | Select-Object -First 1
    if ($dll) {
        Copy-Item $dll.FullName $libtcc
        Write-Host "Copied libtcc.dll from archive."
    } else {
        Write-Warning "libtcc.dll not found in archive!"
    }
}

# Cleanup
Remove-Item $tempZip -Force -ErrorAction SilentlyContinue
Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "TCC bundled successfully at: $tccDir"
Get-ChildItem $tccDir | ForEach-Object { Write-Host "  $($_.Name)  ($($_.Length) bytes)" }
