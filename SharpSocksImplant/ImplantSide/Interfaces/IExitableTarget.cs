using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSocksImplant.Interfaces
{
	public interface IExitableTarget
	{
		String TargetId { get; set; }
		bool Exit { get; set; }
	}
}
