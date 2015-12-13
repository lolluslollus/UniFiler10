using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Utilz;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class WalletView : UserControl
	{
		//public BinderContentVM VM
		//{
		//	get { return (BinderContentVM)GetValue(VMProperty); }
		//	set { SetValue(VMProperty, value); }
		//}
		//public static readonly DependencyProperty VMProperty =
		//	DependencyProperty.Register("VM", typeof(BinderContentVM), typeof(WalletView), new PropertyMetadata(null));

		public FolderVM VM
		{
			get { return (FolderVM)GetValue(VMProperty); }
			set { SetValue(VMProperty, value); }
		}
		public static readonly DependencyProperty VMProperty =
			DependencyProperty.Register("VM", typeof(FolderVM), typeof(WalletView), new PropertyMetadata(null));

		//public Folder Folder
		//{
		//	get { return (Folder)GetValue(FolderProperty); }
		//	set { SetValue(FolderProperty, value); }
		//}
		//public static readonly DependencyProperty FolderProperty =
		//	DependencyProperty.Register("Folder", typeof(Folder), typeof(WalletView), new PropertyMetadata(null));

		public WalletView()
		{
			InitializeComponent();
		}

		//private void OnAdd_Click(object sender, RoutedEventArgs e)
		//{
		//	Task add = VM?.AddEmptyDocumentToWalletAsync(DataContext as Wallet);
		//}

		private void OnItemDelete_Click(object sender, RoutedEventArgs e)
		{
			Task del = VM?.RemoveWalletFromFolderAsync(DataContext as Wallet);
		}

		private void OnShoot_Click(object sender, RoutedEventArgs e)
		{
			Task shoot = VM?.ShootAsync(DataContext as Wallet);
		}

		private void OnOpenFile_Click(object sender, RoutedEventArgs e)
		{
			Task openFile = VM?.LoadMediaFileAsync(DataContext as Wallet);
		}
	}
}
