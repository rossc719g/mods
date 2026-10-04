param(
    [string]$GameExe = 'C:\Program Files (x86)\Steam\steamapps\common\Dave the Diver\DaveTheDiver.exe'
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $GameExe)) { throw 'DaveTheDiver.exe not found' }
$name = 'ClearWaters-HomeSubnet'
$options = @{
    Enabled = 'True'
    Direction = 'Inbound'
    Action = 'Allow'
    Profile = 'Any'
    Program = $GameExe
    Protocol = 'TCP'
    LocalPort = 18780
    RemoteAddress = 'LocalSubnet'
}
if (Get-NetFirewallRule -Name $name -ErrorAction SilentlyContinue) {
    Set-NetFirewallRule -Name $name @options
} else {
    New-NetFirewallRule -Name $name -DisplayName 'Clear Waters (home subnet)' @options | Out-Null
}
$rule = Get-NetFirewallRule -Name $name
[pscustomobject]@{
    Enabled = $rule.Enabled.ToString()
    Program = ($rule | Get-NetFirewallApplicationFilter).Program
    Port = ($rule | Get-NetFirewallPortFilter).LocalPort
    RemoteAddress = ($rule | Get-NetFirewallAddressFilter).RemoteAddress
} | ConvertTo-Json
