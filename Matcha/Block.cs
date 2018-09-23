using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Matcha
{
	public class Block
	{
		public int Index { get; set; }
		public DateTime TimeStamp { get; set; }
		public string PreviousHash { get; set; }
		public ulong Nonce { get; set; }
		public IList<Transaction> Transactions { get; set; }
	}
}
