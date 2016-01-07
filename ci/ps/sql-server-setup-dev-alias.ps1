[reflection.assembly]::LoadWithPartialName("Microsoft.SqlServer.Smo") | Out-Null
[reflection.assembly]::LoadWithPartialName("Microsoft.SqlServer.SqlWmiManagement") | Out-Null

$server = $env:COMPUTERNAME
$instance = 'SQL2014'
$smo = 'Microsoft.SqlServer.Management.Smo.'
$wmi = new-object ($smo + 'Wmi.ManagedComputer')

# Enable TCP/IP
$uri = "ManagedComputer[@Name='$server']/ServerInstance[@Name='$instance']/ServerProtocol[@Name='Tcp']"
$tcp = $wmi.GetSmoObject($uri)
$tcp.IsEnabled = $true
$tcp.alter()


# Set Alias
New-Item HKLM:\SOFTWARE\Microsoft\MSSQLServer\Client -Name ConnectTo | Out-Null
Set-ItemProperty -Path HKLM:\SOFTWARE\Microsoft\MSSQLServer\Client\ConnectTo -Name 'DEV' -Value "DBMSSOCN,$server\$instance" | Out-Null

# Start services
Set-Service SQLBrowser -StartupType Manual
Start-Service SQLBrowser
Start-Service "MSSQL`$$instance"