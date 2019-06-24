using SharpSocksImplant.Interfaces;
using System;
using System.Threading;

namespace SharpSocksImplant.Classes.Target
{
	public class CmdTarget : IExitableTarget
	{
		public String TargetId { get; set; }
		public bool Exit { get { return Token.IsCancellationRequested; } set { return; } }
		public CancellationToken Token { get; set; }
	}
}
