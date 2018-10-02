## SkySync Connector PowerShell Host

### Build

1. Open PowerShellHost.sln and build solution
2. If building from command line, navigate to src/PSSkySync directory
* dotnet restore
* dotnet build

### Using in PowerShell

1. Navigate to build output directory
2. Import-Module PSSkySync
* Note that while the module is imported into PowerShell, you will not be able to rebuild the solution above. You will need to close and restart PowerShell before attempting to rebuild.
3. Create `%TEMP%/SkySyncConnections.json` with a list of connections i.e.
```json
{
  "fs": {
    "temp": {
      "uri": "%TEMP%"
    }
  }
}
```

### Commands

* New-SkySync
* Close-SkySync
* Get-SkySyncConnections
* Get-SkySyncItem
* Get-SkySyncAccounts
* Get-SkySyncGroups
* Get-SkySyncItemAcl
* Get-SkySyncContent
* Set-SkySyncContent
* New-SkySyncContainer
* Rename-SkySyncItem
* Remove-SkySyncItem