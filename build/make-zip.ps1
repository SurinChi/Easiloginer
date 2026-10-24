param(
    [Parameter(Mandatory = $true)][string]$Stage,
    [Parameter(Mandatory = $true)][string]$Zip
)

# Build a spec-compliant ZIP: forward slashes, keep the top-level folder,
# include empty directories (data/, resources/, vendors/).

Add-Type -AssemblyName System.IO.Compression.FileSystem

if (Test-Path $Zip) {
    Remove-Item $Zip -Force
}

$stageRoot = (Resolve-Path -LiteralPath $Stage).Path.TrimEnd('\')
$root = Split-Path $stageRoot -Leaf
$archive = [System.IO.Compression.ZipFile]::Open($Zip, 'Create')

try {
    $files = Get-ChildItem -Path $stageRoot -Recurse -File
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($stageRoot.Length + 1).Replace('\', '/')
        $entryName = "$root/$relative"
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $file.FullName,
            $entryName,
            [System.IO.Compression.CompressionLevel]::Optimal)
    }

    $directories = Get-ChildItem -Path $stageRoot -Recurse -Directory
    foreach ($directory in $directories) {
        if (Get-ChildItem -Path $directory.FullName -Recurse -File -ErrorAction SilentlyContinue) {
            continue
        }

        $relative = $directory.FullName.Substring($stageRoot.Length + 1).Replace('\', '/')
        [void]$archive.CreateEntry("$root/$relative/")
    }

    # Always keep the empty root folders even if they contain nothing
    foreach ($name in @('data', 'resources', 'vendors')) {
        $path = Join-Path $stageRoot $name
        if (-not (Test-Path $path)) {
            continue
        }

        if (-not (Get-ChildItem -Path $path -Recurse -File -ErrorAction SilentlyContinue)) {
            [void]$archive.CreateEntry("$root/$name/")
        }
    }
}
finally {
    $archive.Dispose()
}

Write-Output "ZIP written: $Zip"
