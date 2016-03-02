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
			VM?.StartShoot(Wallet);
		}

		private void OnRecordSound_Click(object sender, RoutedEventArgs e)
		{
			Task record = VM?.RecordAudioAsync(Wallet);
		}

		private void OnOpenFile_Click(object sender, RoutedEventArgs e)
		{
			VM?.StartLoadMediaFile(Wallet);
		}

		private void OnDocumentView_DocumentClicked(object sender, DocumentView.DocumentClickedArgs e)
		{
			Task open = VM?.OpenDocument(e?.Document);
		}

		private void OnDocuments_ItemClick(object sender, ItemClickEventArgs e)
		{
			Task open = VM?.OpenDocument(e?.ClickedItem as Document);
		}

		private void OnDocumentView_DeleteClicked(object sender, DocumentView.DocumentClickedArgs e)
		{
			Task del = VM?.RemoveDocumentFromWalletAsync(e?.Wallet, e?.Document);
		}

		private void OnDocumentView_OcrClicked(object sender, DocumentView.DocumentClickedArgs e)
		{
			Task del = VM?.OcrDocumentAsync(e?.Wallet, e?.Document);
		}
	}
}