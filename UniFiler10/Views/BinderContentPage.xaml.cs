using System;
using System.ComponentModel;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Utilz;
using Utilz.Controlz;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class BinderContentPage : OpenableObservablePage
	{
		private BinderContentVM _vm = null;
		public BinderContentVM VM { get { return _vm; } private set { _vm = value; RaisePropertyChanged_UI(); } }


		#region construct, open, close
		public BinderContentPage()
		{
			LastNavigatedPageRegKey = App.LAST_NAVIGATED_PAGE_REG_KEY;
			NavigationCacheMode = NavigationCacheMode.Enabled;
			InitializeComponent();
		}
		protected override async Task OpenMayOverrideAsync()
		{
			//_vm = new BinderContentVM(AudioRecorderView, CameraView);
			_vm = new BinderContentVM();
			await _vm.OpenAsync();
			RaisePropertyChanged_UI(nameof(VM));

			await MyFolderView.OpenAsync();
		}
		protected override async Task CloseMayOverrideAsync()
		{
			await MyFolderView.CloseAsync();

			var vm = _vm;
			if (vm != null)
			{
				await vm.CloseAsync();
				vm.Dispose();
				VM = null;
			}
		}
		#endregion construct, open, close


		#region user actions
		private void OnFolderPreview_Click(object sender, ItemClickEventArgs e)
		{
			Task setCurFol = _vm?.SetCurrentFolderAsync((e.ClickedItem as Folder)?.Id);
		}

		private void OnOpenCover_Click(object sender, RoutedEventArgs e)
		{
			Frame.Navigate(typeof(BriefcaseContentPage));
		}
		protected override bool GoBackMayOverride()
		{
			Frame.Navigate(typeof(BriefcaseContentPage));
			return true;
		}
		#endregion user actions
	}
}