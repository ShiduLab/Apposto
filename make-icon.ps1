$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$photoPath = Join-Path $PSScriptRoot 'assets\DeskSardTab.jpg'
$iconPath = Join-Path $PSScriptRoot 'assets\Apposto.ico'
$photo = [Drawing.Image]::FromFile($photoPath)
$images = @()
try {
 foreach ($size in @(16,24,32,48,64,128,256)) {
  $bitmap = [Drawing.Bitmap]::new($size,$size)
  $graphics = [Drawing.Graphics]::FromImage($bitmap)
  $stream = [IO.MemoryStream]::new()
  try {
   $graphics.Clear([Drawing.Color]::FromArgb(24,27,34))
   $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
   $ratio = [Math]::Min($size / $photo.Width,$size / $photo.Height)
   $width = [int][Math]::Round($photo.Width * $ratio)
   $height = [int][Math]::Round($photo.Height * $ratio)
   $rectangle = [Drawing.Rectangle]::new([int](($size-$width)/2),[int](($size-$height)/2),$width,$height)
   $graphics.DrawImage($photo,$rectangle)
   $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
   $images += [PSCustomObject]@{Size=$size;Bytes=$stream.ToArray()}
  } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
 }
 $file = [IO.File]::Create($iconPath)
 $writer = [IO.BinaryWriter]::new($file)
 try {
  $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$images.Count)
  $offset = 6 + 16 * $images.Count
  foreach ($item in $images) {
   $dimension = if ($item.Size -eq 256) { 0 } else { $item.Size }
   $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
   $writer.Write([byte]0); $writer.Write([byte]0)
   $writer.Write([UInt16]1); $writer.Write([UInt16]32)
   $writer.Write([UInt32]$item.Bytes.Length); $writer.Write([UInt32]$offset)
   $offset += $item.Bytes.Length
  }
  foreach ($item in $images) { $writer.Write([byte[]]$item.Bytes) }
 } finally { $writer.Dispose() }
} finally { $photo.Dispose() }
