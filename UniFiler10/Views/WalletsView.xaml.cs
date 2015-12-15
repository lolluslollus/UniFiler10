using System.Threading.Tasks;
using UniFiler10.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class WalletsView : UserControl
	{
		public FolderVM VM
		{
			get { return (FolderVM)GetValue(VMProperty); }
			set { SetValue(VMProperty, value); }
		}
		public static readonly DependencyProperty VMProperty =
			DependencyProperty.Register("VM", typeof(FolderVM), typeof(WalletsView), new PropertyMetadata(null));

		public WalletsView()
		{
			InitializeComponent();
		}

		private void OnShoot_Click(object sender, RoutedEventArgs e)
		{
			Task shoot = VM?.ShootAsync(true, null);
		}

		private void OnOpenFile_Click(object sender, RoutedEventArgs e)
		{
			Task openFile = VM?.LoadMediaFileAsync();
		}

		private void OnRecordSound_Click(object sender, RoutedEventArgs e)
		{
			Task record = VM?.RecordAudioAsync();
		}
	}
}
