using System;
using System.Collections.Generic;
using System.Text;

namespace SharpSocksServerCore.SocksServer.Classes.Protocol
{
	public static class Socks5Headers
	{
		public static readonly byte Version = 0x5;
		public enum Socks5RequestAuthType: byte	
		{
			/* https://tools.ietf.org/html/rfc1928
			o X'00' NO AUTHENTICATION REQUIRED
			o X'01' GSSAPI
			o X'02' USERNAME / PASSWORD
			o X'03' to X'7F' IANA ASSIGNED
			o X'80' to X'FE' RESERVED FOR PRIVATE METHODS
			o X'FF' NO ACCEPTABLE METHODS
			*/
			NoneRequired = 0x0,
			GSSAPI = 0x1,
			UsernamePassword = 0x2,
			NoneAcceptable = 0xFF,
		}

		public enum Socks5RequestCommand : byte
		{
			/*https://tools.ietf.org/html/rfc1928
			 * o CONNECT X'01'
             o BIND X'02'
             o UDP ASSOCIATE X'03'
			 */
			CONNECT = 0x1,
			BIND = 0x2,
			UDPASSOCIATE = 0x3
		}

		public enum Socks5RequestAddressType : byte
		{
			/*https://tools.ietf.org/html/rfc1928
			 * o  IP V4 address: X'01'
             o  DOMAINNAME: X'03'
             o  IP V6 address: X'04'
			 */
			IPV4 = 0x1,
			DOMAINNAME = 0x3,
			IPV6 = 0x4
		}

		public enum Socks5ResponseReplyStatus : byte
		{
			/* https://tools.ietf.org/html/rfc1928
			 * REP    Reply field:
				 o  X'00' succeeded
				 o  X'01' general SOCKS server failure
				 o  X'02' connection not allowed by ruleset
				 o  X'03' Network unreachable
				 o  X'04' Host unreachable
				 o  X'05' Connection refused
				 o  X'06' TTL expired
				 o  X'07' Command not supported
				 o  X'08' Address type not supported
				 o  X'09' to X'FF' unassigned
			*/
			succeeded = 0x0,
			generalFailure = 0x1,
			connectionNotAllowed = 0x02,
			networkUnreachable = 0x3,
			hostUnreachable = 0x4,
			connectionRefused = 0x5,
			ttlExpired = 0x6,
			commandNotSuppported = 0x7,
			addressNotSupported = 0x8
		}
	}
}
