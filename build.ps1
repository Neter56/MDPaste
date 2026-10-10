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
    Copy-Item .\licenses .\dist\licenses -Recurse -Force

    $smoke = Start-Process .\dist\MDPaste.exe -ArgumentList '--smoke-test' -PassThru -Wait
    Get-Content .\dist\smoke-test.txt
    if ($smoke.ExitCode -ne 0) { throw 'Standalone smoke test failed.' }
    Remove-Item .\dist\smoke-test.txt

    $items = @('.\dist\MDPaste.exe', '.\dist\README.md', '.\dist\SAMPLE.md',
        '.\dist\THIRD-PARTY-NOTICES.txt', '.\dist\licenses')
    Compress-Archive -Path $items -DestinationPath .\dist\MDPaste-Windows.zip -Force
    Get-FileHash .\dist\MDPaste.exe, .\dist\MDPaste-Windows.zip -Algorithm SHA256
}
finally {
    Pop-Location
}
