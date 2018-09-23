using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Matcha
{
	public interface IMethod
	{
		string Matcha { get; }
		string Type { get; }
		int PortNumber { get; set; }
	}
}
