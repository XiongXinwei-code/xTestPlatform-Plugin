[CmdletBinding()]
param([string]$OutputRoot = 'artifacts/Release/2026-10-24-remediation-ao')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\', '/')
$delivery = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
if (-not $delivery.StartsWith((Join-Path $repoRoot 'artifacts') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw '只核对仓库 artifacts 内的交付，不读取或替换实际平台 DLL。'
}
$packageRoot = Join-Path $delivery 'Plugins'
$testRoot = Join-Path $delivery 'verification/test-bin'
Add-Type -AssemblyName System.Reflection.Metadata
$folders = @(Get-ChildItem -LiteralPath $packageRoot -Directory) + @(Get-ChildItem -LiteralPath $testRoot -Directory)
$records = foreach ($folder in $folders) {
    $isTest = $folder.FullName.StartsWith($testRoot, [StringComparison]::OrdinalIgnoreCase)
    $pattern = if ($isTest) { '*Tests.dll' } else { '*StepPlugin*.dll' }
    foreach ($dll in Get-ChildItem -LiteralPath $folder.FullName -File -Filter $pattern) {
        $pdbPath = [IO.Path]::ChangeExtension($dll.FullName, '.pdb')
        if (!(Test-Path -LiteralPath $pdbPath)) { throw "Missing PDB: $pdbPath" }
        $peStream = [IO.File]::OpenRead($dll.FullName)
        $pe = [Reflection.PortableExecutable.PEReader]::new($peStream)
        $pdbStream = [IO.File]::OpenRead($pdbPath)
        $provider = [Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($pdbStream)
        try {
            $reader = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
            $pdb = $provider.GetMetadataReader()
            $pdbId = [byte[]]@($pdb.DebugMetadataHeader.Id)
            $guid = [Guid]::new([byte[]]$pdbId[0..15]); $stamp = [BitConverter]::ToUInt32($pdbId, 16)
            $paired = $false
            foreach ($entry in $pe.ReadDebugDirectory() | Where-Object Type -eq 'CodeView') {
                $cv = $pe.ReadCodeViewDebugDirectoryData($entry)
                if ($cv.Guid -eq $guid -and $entry.Stamp -eq $stamp) { $paired = $true }
            }
            if (!$paired) { throw "PDB/PE pair mismatch: $($dll.FullName)" }
            $documents = @(
                foreach ($handle in $pdb.Documents) {
                    $document = $pdb.GetDocument($handle)
                    $name = $pdb.GetString($document.Name).Replace('/', '\')
                    if (!$name.StartsWith($repoRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { continue }
                    $relative = $name.Substring($repoRoot.Length + 1)
                    if ($relative -match '[\\/](bin|obj|artifacts)[\\/]') { continue }
                    if (!(Test-Path -LiteralPath $name)) { throw "PDB source missing: $name" }
                    if ($pdb.GetGuid($document.HashAlgorithm).ToString() -ne '8829d00f-11b8-4213-878b-770e8597ac16') { throw "Unexpected source hash algorithm: $name" }
                    $recorded = [Convert]::ToHexString($pdb.GetBlobBytes($document.Hash)).ToLowerInvariant()
                    $actual = (Get-FileHash -LiteralPath $name -Algorithm SHA256).Hash.ToLowerInvariant()
                    if ($recorded -ne $actual) { throw "Current source differs from delivery: $relative" }
                    [pscustomobject]@{ path = $relative.Replace('\', '/'); sha256 = $actual }
                }
            )
            if ($documents.Count -eq 0) { throw "No local source documents found: $($dll.FullName)" }
            [pscustomobject]@{
                module = $folder.Name; isTest = $isTest; path = [IO.Path]::GetRelativePath($delivery, $dll.FullName).Replace('\', '/')
                assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($dll.FullName).Version.ToString()
                fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll.FullName).FileVersion
                mvid = $reader.GetGuid($reader.GetModuleDefinition().Mvid).ToString()
                sha256 = (Get-FileHash -LiteralPath $dll.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                pdbMatchesPe = $paired; sourceDocumentsMatched = $documents.Count; documents = $documents
            }
        }
        finally { $provider.Dispose(); $pdbStream.Dispose(); $pe.Dispose(); $peStream.Dispose() }
    }
}
$receipt = [ordered]@{ capturedAt = [DateTimeOffset]::Now.ToString('O'); nativeAssembliesLoaded = $false
    binaryCount = @($records).Count; currentSourceDocumentsMatched = ($records | Measure-Object sourceDocumentsMatched -Sum).Sum
    boundary = 'PE/PDB pairing and non-generated local C# SHA256 only; not current XAML, SDK ABI, real host loaded version or hardware acceptance.'
    results = @($records) }
$receipt | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $delivery 'verification/delivery-source-receipt.json') -Encoding utf8NoBOM
Write-Output "PE/PDB source match: $($receipt.binaryCount) binaries; $($receipt.currentSourceDocumentsMatched) document occurrences."
