using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Utilz;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class WalletView : UserControl
	{
		public FolderVM VM
		{
			get { return (FolderVM)GetValue(VMProperty); }
			set { SetValue(VMProperty, value); }
		}
		public static readonly DependencyProperty VMProperty =
			DependencyProperty.Register("VM", typeof(FolderVM), typeof(WalletView), new PropertyMetadata(null));

		public Wallet Wallet
		{
			get { return (Wallet)GetValue(WalletProperty); }
			set { SetValue(WalletProperty, value); }
		}
		public static readonly DependencyProperty WalletProperty =
			DependencyProperty.Register("Wallet", typeof(Wallet), typeof(WalletView), new PropertyMetadata(null));

		public WalletView()
		{
			InitializeComponent();
		}

		private void OnItemDelete_Click(object sender, RoutedEventArgs e)
		{
			Task del = VM?.RemoveWalletFromFolderAsync(Wallet);
		}

		private void OnShoot_Click(object sender, RoutedEventArgs e)
		{
			Task shoot = VM?.ShootAsync(false, Wallet);
		}

		private void OnOpenFile_Click(object sender, RoutedEventArgs e)
		{
			Task openFile = VM?.LoadMediaFileAsync(Wallet);
		}

		private async void OnDocumentView_DocumentClicked(object sender, DocumentView.DocumentClickedArgs e)
		{
			var doc = e?.Document;
			if (doc != null && !string.IsNullOrWhiteSpace(doc.Uri0))
			{
				var file = await StorageFile.GetFileFromPathAsync(doc.GetFullUri0()).AsTask(); //.ConfigureAwait(false);
				if (file != null)
				{
					bool isOk = false;
					try
					{
						//isOk = await Launcher.LaunchFileAsync(file, new LauncherOptions() { DisplayApplicationPicker = true }).AsTask().ConfigureAwait(false);
						isOk = await Launcher.LaunchFileAsync(file).AsTask().ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						Debugger.Break();
						await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
					}
				}
			}
		}

		private async void OnDocumentView_DeleteClicked(object sender, DocumentView.DocumentClickedArgs e)
		{
			bool isOk = false;
			var vm = VM;
			if (vm != null && e != null)
			{
				if (await vm.RemoveDocumentFromWalletAsync(e.Wallet, e.Document).ConfigureAwait(false))
				{
					// if there are no more documents in the wallet, delete the wallet, too
					isOk = await vm.RemoveWalletFromFolderAsync(e.Wallet).ConfigureAwait(false);
				}
			}
		}
	}
}
