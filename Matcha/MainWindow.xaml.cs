using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Matcha
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{

		public MainWindow()
		{
			InitializeComponent();
			
		}

		private void Window_Initialized(object sender, EventArgs e)
		{
			node_ = new Node();
			PortNumber.Text = node_.PortNumber.ToString();
			node_.LogChanged += Node__LogChanged;
			node_.TransactionPoolChanged += Node__TransactionPoolChanged;
			node_.BlockChainChanged += Node__BlockChainChanged;
			SetBlockChain();
		}

		private void Node__BlockChainChanged(object sender, EventArgs e)
		{
			SetBlockChain();
		}

		private void Node__TransactionPoolChanged(object sender, EventArgs e)
		{
			TransactionPool.Dispatcher.Invoke(() => {
				TransactionPool.Text = node_.TransactionPool.Aggregate("", (s, t) => $"{s}{t.Timestamp}: {t.Message}{Environment.NewLine}");
			});
		}

		private void Node__LogChanged(object sender, EventArgs e)
		{
			Log.Dispatcher.Invoke(() => {
				Log.Text = node_.Log;
			});
		}

		private Node node_;

		private void Grid_Unloaded(object sender, RoutedEventArgs e)
		{
			node_.Dispose();
		}

		private async void GetNodeButton_Click(object sender, RoutedEventArgs e)
		{
			await node_.GetNodes();
			Nodes.Text = node_.Nodes.Count != 0
				? node_.Nodes.Select(n => n.AbsoluteUri).Aggregate("", (a, s) => a + s + Environment.NewLine)
				: "";
		}

		private void Send_Click(object sender, RoutedEventArgs e)
		{
			node_.AddTransaction(Message.Text);
			Message.Text = "";
		}

		private async void Sync_Click(object sender, RoutedEventArgs e)
		{
			await node_.Sync();
		}

		private async void CriateBlock_Click(object sender, RoutedEventArgs e)
		{
			await node_.CompleteBlock();
		}

		private void SetBlockChain()
		{
			BlockChain.Dispatcher.Invoke(() =>
				BlockChain.Text = node_.BlockChain.Aggregate("", (s, b) =>
					s + $"+++{b.Index} {b.Nonce}+++{Environment.NewLine}"
						+ $"timestamp: {b.TimeStamp}{Environment.NewLine}"
						+ $"prev hash: {b.PreviousHash}{Environment.NewLine}"
						+ b.Transactions.Aggregate("", (ss, t) => ss + $"{t.Timestamp}: {t.Message}{Environment.NewLine}")
						+ Environment.NewLine
					)
			);
		}
	}
}
