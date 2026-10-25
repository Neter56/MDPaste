$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet build .\MDPaste.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
    dotnet build .\Tests\MDPaste.Tests.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & .\Tests\bin\Release\net48\MDPaste.Tests.exe
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

    New-Item -ItemType Directory -Path .\dist -Force | Out-Null
    Copy-Item .\bin\Release\net48\MDPaste.exe .\dist\MDPaste.exe -Force
    Copy-Item .\README.md, .\SAMPLE.md, .\THIRD-PARTY-NOTICES.txt .\dist -Force
    Remove-Item .\dist\licenses -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item .\licenses .\dist\licenses -Recurse -Force

    $smoke = Start-Process .\dist\MDPaste.exe -ArgumentList '--smoke-test' -PassThru -Wait
    Get-Content .\dist\smoke-test.txt
    if ($smoke.ExitCode -ne 0) { throw 'Standalone smoke test failed.' }
    Remove-Item .\dist\smoke-test.txt

    $items = @('.\dist\MDPaste.exe', '.\dist\README.md', '.\dist\SAMPLE.md',
        '.\dist\THIRD-PARTY-NOTICES.txt', '.\dist\licenses')
    Compress-Archive -Path $items -DestinationPath .\dist\MDPaste-Windows.zip -Force

    $version = ([xml](Get-Content .\MDPaste.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    $iscc = @(
        (Get-Command iscc -ErrorAction SilentlyContinue).Source,
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $iscc) { throw 'Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup' }
    & $iscc /Qp "/DAppVersion=$version" .\installer\MDPaste.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }

    Get-FileHash .\dist\MDPaste.exe, .\dist\MDPaste-Windows.zip, ".\dist\MDPaste-Setup-$version.exe" -Algorithm SHA256
}
finally {
    Pop-Location
}
