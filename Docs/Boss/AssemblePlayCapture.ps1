param([string]$CaptureFolder = "$PSScriptRoot\CompletionValidation\DR_Sweep", [string]$Output = "$PSScriptRoot\CompletionValidation\DR_Sweep_PlayMode.gif")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
$encoder = New-Object System.Windows.Media.Imaging.GifBitmapEncoder
foreach ($file in Get-ChildItem -LiteralPath $CaptureFolder -Filter 'Frame_*.png' | Sort-Object Name) {
    $image = New-Object System.Windows.Media.Imaging.BitmapImage
    $image.BeginInit()
    $image.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
    $image.UriSource = [Uri]::new($file.FullName)
    $image.DecodePixelWidth = 960
    $image.EndInit()
    $image.Freeze()
    $metadata = New-Object System.Windows.Media.Imaging.BitmapMetadata('gif')
    $metadata.SetQuery('/grctlext/Delay', [UInt16]8)
    $metadata.SetQuery('/grctlext/Disposal', [byte]2)
    $frame = [System.Windows.Media.Imaging.BitmapFrame]::Create($image, $null, $metadata, $null)
    $encoder.Frames.Add($frame)
}
$stream = [IO.File]::Create($Output)
try { $encoder.Save($stream) } finally { $stream.Dispose() }
Get-Item -LiteralPath $Output | Select-Object FullName,Length
