using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Utilz;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
	public sealed partial class WalletsView : UserControl
	{
		public BinderContentVM VM
		{
			get { return (BinderContentVM)GetValue(VMProperty); }
			set { SetValue(VMProperty, value); }
		}
		public static readonly DependencyProperty VMProperty =
			DependencyProperty.Register("VM", typeof(BinderContentVM), typeof(WalletsView), new PropertyMetadata(null));

		public WalletsView()
		{
			InitializeComponent();
		}

		private void OnAdd_Click(object sender, RoutedEventArgs e)
		{
			Task add = VM?.AddWalletToFolderAsync(DataContext as Folder);
		}

		private void OnShoot_Click(object sender, RoutedEventArgs e)
		{
			Task shoot = VM?.ShootAsync(DataContext as Folder);
		}

		private void OnOpenFile_Click(object sender, RoutedEventArgs e)
		{
			Task openFile = VM?.LoadMediaFileAsync(DataContext as Folder);
		}

		private void OnRecordSound_Click(object sender, RoutedEventArgs e)
		{
			Task record = VM?.RecordAudioAsync(DataContext as Folder);
		}
	}
}
