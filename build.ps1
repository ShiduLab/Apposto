$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $PSScriptRoot
try {
 & $compiler /nologo /target:winexe /optimize+ /out:Apposto.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll src\Engine.cs src\Profiles.cs src\Program.cs src\AssemblyInfo.cs
 if ($LASTEXITCODE -ne 0) { throw 'Compilazione fallita' }
 & $compiler /nologo /out:tests\Apposto.Tests.exe src\Engine.cs src\Profiles.cs tests\Tests.cs
 if ($LASTEXITCODE -ne 0) { throw 'Compilazione test fallita' }
 & .\tests\Apposto.Tests.exe
 if ($LASTEXITCODE -ne 0) { throw 'Test falliti' }
} finally { Pop-Location }
