using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Matcha
{
	public class Config
	{
		public int PortNumber { get; set; }
		public HashSet<Uri> Nodes { get; set; }
	}
}
