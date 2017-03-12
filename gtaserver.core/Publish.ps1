# A script to generate release publishable builds.
Add-Type -AssemblyName "system.io.compression.filesystem"

# Remove old outputs
If (Test-Path publish/windows-Release) { Remove-Item publish/windows-Release -Recurse }
If (Test-Path publish/linux-Release) { Remove-Item publish/linux-Release -Recurse }
If (Test-Path publish/windows-Release.zip) { Remove-Item publish/windows-Release.zip }
If (Test-Path publish/linux-Release.zip) { Remove-Item publish/linux-Release.zip }
# Build
dotnet build -r win7-x64 -c Release
dotnet build -r ubuntu.14.04-x64 -c Release

# Publish
dotnet publish -r win7-x64 -c Release -o publish/windows-Release
dotnet publish -r ubuntu.14.04-x64 -c Release -o publish/linux-Release

# Zip
Compress-Archive -Path publish/windows-Release -DestinationPath publish/windows-Release.zip
Compress-Archive -Path publish/linux-Release -DestinationPath publish/linux-Release.zip