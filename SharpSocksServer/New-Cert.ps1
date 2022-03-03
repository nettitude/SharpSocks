$cert = New-SelfSignedCertificate -CertStoreLocation cert:\currentuser\my -DNSName windows
$pwd = Read-Host -Prompt "Enter the password to use for the cert: " -AsSecureString
$path = 'cert:\currentuser\my\' + $cert.thumbprint
Export-PfxCertificate -cert $path -FilePath SharpSocks.pfx -Password $pwd
Write-Host "Pfx created, you will need to update the password for the SharpSocks cert"