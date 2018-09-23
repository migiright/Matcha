using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Matcha.Responses
{
	public class GetNodesResponse
	{
		public ICollection<Uri> Nodes { get; set; }
	}
}
