# SharpSocks

![SharpSocksServer](https://github.com/nettitude/SharpSocks/actions/workflows/sharpsocks-server.yml/badge.svg)

Tunnellable HTTP/HTTPS socks4a proxy written in C#.

## Usage

### Server

.NET Core Project with builds for Windows, Linux, and Docker support.
Once the implant side connects and establishes the tunnel, the SOCKS server open on the socks port (43334 by default.)

```
Usage:  [options]

Options:
  -?|-h|--help         Show help information.
  -s|--socksserveruri  IP:Port for SOCKS to listen on, default is *:43334
  -c|--cmdid           Command Channel Identifier, needs to be shared with the server
  -l|--httpserveruri   Uri to listen on, default is http://127.0.0.1:8081
  -k|--encryptionkey   The encryption key used to secure comms
  -sc|--sessioncookie  The name of the cookie to pass the session identifier
  -pc|--payloadcookie  The name of the cookie to pass smaller requests through
  -st|--socketTimeout  How long should SOCKS sockets be held open for, default is 30s
  -v|--verbose         Verbose error logging
  -p|--pfxpassword     Password to the PFX certificate if using HTTPS
```

### TLS

If using HTTPS you will want to replace SharpSocks.pfx with a new certificate, you do this manually or using `New-Cert.ps1`.
This will prompt you for a password for the PFX file which will need to be passed to SharpSocks with the `-p` option.

### Client (Implant side)

.NET 4.0 project for running on the target (such as in memory in a PoshC2 implant) which tunnels the traffic to the server.

```
SharpSocks Proxy Client
=======================

      --use-proxy            Use proxy server (for system proxy set this and
                               leave -m blank)
  -m, --proxy=VALUE          Proxy Url in format http://<server>:<port> (use-
                               proxy is implied)
  -u, --username=VALUE       Web proxy username
  -d, --domain=VALUE         Web proxy domain
  -p, --password=VALUE       Web proxy password
  -k, --encryption-key=VALUE The encryption key, leave blank to be asked
  -c, --cmd-id=VALUE         Command Channel Id (required)
  -b, --beacon=VALUE         Beacon time in (ms)
  -s, --server-uri=VALUE     Uri of the server, default is http://127.0.-
                               0.1:8081
      --url1=VALUE           pages/2019/stats.php
      --url2=VALUE           web/v10/2/admin.asp
      --session-cookie=VALUE The name of the cookie to pass the session
                               identifier
      --payload-cookie=VALUE The name of the cookie to pass smaller requests
                               through
      --user-agent=VALUE     The User Agent to be sent in any web request
      --df=VALUE             The actual Host header to be sent if using
                               domain fronting
  -h, -?, --help
  -v, --verbose
  -r, --read-time=VALUE      The time between SOCKS proxy reads, default 500ms
  -a, --standalone           Standalone mode, do not return on the main thread
```

### Apache Rewrite Rule (C2 proxy)

If using a C2 proxy you can achieve TLS termination and route the traffic for the SOCKS URLs to the server running locally.
```
Define SharpSocks 127.0.0.1:49031
RewriteRule ^/sharpsocks1/(.*) http://${SharpSocks} [NC,L,P]
RewriteRule ^/sharpsocks2/(.*) http://${SharpSocks} [NC,L,P]
```
