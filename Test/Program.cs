using Matcha;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Globalization;

namespace Test
{
	class Program
	{
		static void Main(string[] args)
		{
			var block = new Block { PreviousHash = "0000000000000000000000000000000000000000000000000000000000000000", Transactions = new List<Transaction>() };
			ulong n = 0;
			var sbf = JsonConvert.SerializeObject(block);
			Console.WriteLine("serialized block: ");
			Console.WriteLine(sbf);
			Console.WriteLine();
			var crypto = new SHA256CryptoServiceProvider();
			var encoding = Encoding.GetEncoding("UTF-8");
			var target = BigInteger.Parse("0000000100000000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
			Console.WriteLine($"target: {target.ToString("X64")}");
			do {
				block.Nonce = n;
				var sb = JsonConvert.SerializeObject(block);
				var eb = encoding.GetBytes(sb);
				var hash = crypto.ComputeHash(eb);
				var intHash = new BigInteger(new byte[1].Concat(hash).Reverse().ToArray());
				if (intHash < target) {
					Console.WriteLine($"hash:   {intHash.ToString("X64")}");
					Console.WriteLine($"nonce: {n}");
					Console.WriteLine($"sb: {sb}");
					Console.WriteLine("yay!");
					break;
				}
				++n;
			} while (n != ulong.MaxValue);
		}
	}
}
