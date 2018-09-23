using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Matcha
{
	public class Transaction : IComparable<Transaction>
	{
		public DateTime Timestamp { get; set; }
		public string Message { get; set; }

		public int CompareTo(Transaction other)
		{
			var r = Timestamp.CompareTo(other.Timestamp);
			if(r == 0) {
				r = Message.CompareTo(other.Message);
			}
			return r;
		}

		public override bool Equals(object obj)
		{
			return obj is Transaction transaction &&
			 Timestamp == transaction.Timestamp && Message == transaction.Message;
		}

		public override int GetHashCode()
		{
			var hashCode = -1147354945;
			hashCode=hashCode*-1521134295+Timestamp.GetHashCode();
			hashCode=hashCode*-1521134295+EqualityComparer<string>.Default.GetHashCode(Message);
			return hashCode;
		}

		public IList<byte> Serialize()
		{
			var m = Encoding.UTF8.GetBytes(Message);
			return BitConverter.GetBytes(m.Length + 8)
				.Concat(BitConverter.GetBytes(Timestamp.Ticks))
				.Concat(m)
				.ToList();
		}
	}
}
