using Matcha.Methods;
using Matcha.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Matcha
{
	public class Node : IDisposable
	{
		public List<Block> BlockChain { get; } = new List<Block>();
		public Block NewBlock;
		public SortedSet<Transaction> TransactionPool { get; } = new SortedSet<Transaction>();
		public BigInteger Target { get; set; } = BigInteger.Parse(
			"0001000000000000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
		public Encoding Encoding { get; set; } = Encoding.UTF8;
		public HashAlgorithm HashAlgorithm { get; set; } = new SHA256CryptoServiceProvider();
		public int PortNumber { get; set; } = 63844;
		public HashSet<Uri> Nodes { get; } = new HashSet<Uri>();
		public string ConfigFileName => "config.json";
		public string Log { get; private set; } = "";
		public string LastHash;

		public event EventHandler LogChanged;
		public event EventHandler TransactionPoolChanged;
		public event EventHandler BlockChainChanged;

		private void OnLogChanged()
		{
			LogChanged?.Invoke(this, new EventArgs());
		}

		private void OnTransactionPoolChanged()
		{
			TransactionPoolChanged?.Invoke(this, new EventArgs());
		}

		private void OnBlockChainChanged()
		{
			BlockChainChanged?.Invoke(this, new EventArgs());
		}

		public async void AddTransaction(string message)
		{
			var guid = Guid.NewGuid();
			var transaction = new Transaction { Timestamp = DateTime.UtcNow, Message = message };
			TransactionPool.Add(transaction);
			OnTransactionPoolChanged();
			await Task.Run(async () => {
				foreach (var node in Nodes) {
					try {
						(await httpClient_.PostAsync(node, new StringContent(JsonConvert.SerializeObject(
							new SendTransaction { PortNumber = PortNumber, Transaction = transaction })))).Dispose();
					} catch (HttpRequestException) {
					}
				}
			});
		}

		public Node()
		{
			if (File.Exists(ConfigFileName)) {
				string s;
				using (var sr = new StreamReader(ConfigFileName, Encoding)) {
					s = sr.ReadToEnd();
				}
				var config = JsonConvert.DeserializeObject<Config>(s);
				PortNumber = config.PortNumber;
				Nodes = config.Nodes ?? new HashSet<Uri>();
			}

			BlockChain.Add(GetGenesisBlock());
			LastHash = SerializeHash(GetHash(BlockChain.Last()));

			httpListener_.Prefixes.Add($"http://+:{PortNumber}/");
			httpListener_.Start();

			Task.Run(() => {
				while (true) {
					var context = httpListener_.GetContext();
					void respond()
					{
						context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
						context.Response.OutputStream.Close();
					}
					string requestString;
					using (var sr = new StreamReader(context.Request.InputStream)) {
						requestString = sr.ReadToEnd();
					}
					Log += $"request get: {requestString}{Environment.NewLine}";
					OnLogChanged();
					JObject requestJObject;
					try {
						requestJObject = JObject.Parse(requestString);
					} catch (JsonReaderException) {
						respond();
						continue;
					}
					if (!requestJObject.ContainsKey("Matcha") || requestJObject["Matcha"].Type != JTokenType.String
						|| requestJObject["Matcha"].Value<string>() != "Matcha") {
						respond();
						continue;
					}
					var client = new Uri($"http://{context.Request.RemoteEndPoint.Address}"
						+ $":{requestJObject["PortNumber"].Value<int>()}/");
					Nodes.Add(client);
					var response = context.Response;
					response.StatusCode = 200;
					using (var sw = new StreamWriter(response.OutputStream, Encoding)) {
						switch (requestJObject["Type"].Value<string>()) {
						case nameof(Methods.GetNodes): {
								var nodes = new HashSet<Uri>(Nodes);
								nodes.Remove(client);
								sw.Write(JsonConvert.SerializeObject(new GetNodesResponse { Nodes = nodes }));
								break;
							}
						case nameof(SendTransaction): {
								var request = requestJObject.ToObject<SendTransaction>();
								if (!TransactionPool.Contains(request.Transaction)) {
									TransactionPool.Add(request.Transaction);
									OnTransactionPoolChanged();
								}
								break;
							}
						case nameof(GetCurrentIndex): {
								sw.Write(JsonConvert.SerializeObject(new GetCurrentIndexResponse { CurrentIndex = BlockChain.Last().Index }));
								break;
							}
						case nameof(GetBlocks): {
								var request = requestJObject.ToObject<GetBlocks>();
								sw.Write(JsonConvert.SerializeObject(new GetBlocksResponse {
									Blocks = BlockChain.Skip(request.Index).Take(request.NumBlocks).ToList()
								}));
								break;
							}
						}
					}
				}
			});
		}

		~Node()
		{
			Dispose();
		}

		static Block GetGenesisBlock()
		{
			return new Block {
				PreviousHash = "0000000000000000000000000000000000000000000000000000000000000000",
				Transactions = new List<Transaction>(),
				Nonce = 35275970,
			};
		}

		public string Serialize(Block block)
		{
			return JsonConvert.SerializeObject(block);
		}

		public BigInteger GetHash(Block block)
		{
			return new BigInteger(new byte[1].Concat(HashAlgorithm.ComputeHash(Encoding.GetBytes(Serialize(block))))
				.Reverse()
				.ToArray());
		}

		public string SerializeHash(BigInteger hash)
		{
			return hash.ToString("X64");
		}

		public BigInteger DeserializeHash(string hash)
		{
			return BigInteger.Parse(hash, NumberStyles.HexNumber);
		}

		public void CreateBlock()
		{
			lock (lockObject_) {
				NewBlock = new Block {
					Index = BlockChain.Last().Index + 1,
					PreviousHash = SerializeHash(GetHash(BlockChain.Last())),
					TimeStamp = DateTime.UtcNow,
					Transactions = TransactionPool.ToList(),
				};
				TransactionPool.Clear();
			}
		}

		public async Task CompleteBlock()
		{
			CreateBlock();
			Log += $"block {NewBlock.Index} created." + Environment.NewLine;
			Log += "finding nonce..." + Environment.NewLine;
			OnLogChanged();
			await FindNonce();
			Log += $"yay! nonce: {BlockChain.Last().Nonce}" + Environment.NewLine;
			OnLogChanged();
		}

		public async Task FindNonce()
		{
			await Task.Run(() => {
				lock (lockObject_) {
					ulong nonce = 0;
					do {
						NewBlock.Nonce = nonce;
						var hash = GetHash(NewBlock);
						if (hash < Target) {
							BlockChain.Add(NewBlock);
							NewBlock = null;
							LastHash = SerializeHash(hash);
							OnBlockChainChanged();
							TransactionPool.Clear();
							OnTransactionPoolChanged();
							return;
						}
						++nonce;
					} while (nonce != ulong.MaxValue);
					throw new Exception("nonce not found.");
				}
			});
		}

		public async Task GetNodes()
		{
			await Task.Run(async () => {
				var newNodes = new HashSet<Uri>();
				foreach (var node in Nodes) {
					HttpResponseMessage response;
					try {
						response = await httpClient_.PostAsync(node, new StringContent(JsonConvert.SerializeObject(
							new GetNodes { PortNumber = PortNumber })));
					} catch (HttpRequestException) {
						continue;
					}
					if (response.IsSuccessStatusCode) {
						var res = JsonConvert.DeserializeObject<GetNodesResponse>(await response.Content.ReadAsStringAsync());
						foreach (var n in res.Nodes) {
							newNodes.Add(n);
						}
					}
				}
				Nodes.UnionWith(newNodes);
			});
		}

		public Task Sync()
		{
			return Task.Run(async () => {
				Log += "syncing..." + Environment.NewLine;
				OnLogChanged();
				while (true) {
					var (index, node) = Nodes.Aggregate((index: BlockChain.Last().Index, node: (Uri)null), (m, n) =>
						Task.Run(async () => {
							HttpResponseMessage response;
							try {
								response = await httpClient_.PostAsync(n, new StringContent(JsonConvert.SerializeObject(
									new GetCurrentIndex { PortNumber = PortNumber })));
							} catch (HttpRequestException) {
								return m;
							}
							if (!response.IsSuccessStatusCode) return m;
							var res = JsonConvert.DeserializeObject<GetCurrentIndexResponse>(await response.Content.ReadAsStringAsync());
							if (res.CurrentIndex <= m.index) return m;
							return (index: res.CurrentIndex, node: n);
						}).Result);
					if (node == null) break;
					if (index > BlockChain.Last().Index) {
						IList<Block> branch;
						var currentIndex = BlockChain.Last().Index;
						{
							HttpResponseMessage response;
							try {
								response = await httpClient_.PostAsync(node, new StringContent(JsonConvert.SerializeObject(
									new GetBlocks { Index = currentIndex + 1, NumBlocks = index - currentIndex })));
							} catch (HttpRequestException) {
								continue;
							}
							if (!response.IsSuccessStatusCode) continue;
							var res = JsonConvert.DeserializeObject<GetBlocksResponse>(await response.Content.ReadAsStringAsync());
							branch = res.Blocks;
						}
						if(branch.First().PreviousHash == LastHash) {
							foreach (var block in branch) {
								foreach (var transaction in block.Transactions) {
									if (TransactionPool.Contains(transaction)) {
										TransactionPool.Remove(transaction);
									}
								}
							}
							BlockChain.AddRange(branch);
							break;
						}
						while (true) {
							HttpResponseMessage response;
							try {
								response = await httpClient_.PostAsync(node, new StringContent(JsonConvert.SerializeObject(
									new GetBlocks { Index = currentIndex, NumBlocks = 1 })));
							} catch (HttpRequestException) {
								goto con;
							}
							if (!response.IsSuccessStatusCode) goto con;
							var res = JsonConvert.DeserializeObject<GetBlocksResponse>(await response.Content.ReadAsStringAsync());
							branch.Insert(0, res.Blocks.First());
							if (BlockChain[currentIndex].PreviousHash == branch.First().PreviousHash) break;
							--currentIndex;
						}
						BlockChain.RemoveRange(currentIndex, BlockChain.Count - currentIndex);
						foreach (var block in branch) {
							foreach (var transaction in block.Transactions) {
								if (TransactionPool.Contains(transaction)) {
									TransactionPool.Remove(transaction);
								}
							}
						}
						BlockChain.AddRange(branch);
						break;
					}
					con:;
				}
				LastHash = SerializeHash(GetHash(BlockChain.Last()));
				OnBlockChainChanged();
				Log += "sync completed." + Environment.NewLine;
				OnLogChanged();
			});
		}

		public void Dispose()
		{
			httpClient_.Dispose();
			httpListener_.Close();
		}

		private readonly HttpClient httpClient_ = new HttpClient();
		private readonly HttpListener httpListener_ = new HttpListener();
		private readonly object lockObject_ = new object();
	}
}
