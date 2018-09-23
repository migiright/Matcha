using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Matcha
{
	public class Method : IMethod
	{
		public string Matcha => "Matcha";
		public string Type => GetType().Name;
		public int PortNumber { get; set; }
	}
}
