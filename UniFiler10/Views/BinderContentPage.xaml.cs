using System;
using System.ComponentModel;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Utilz;
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
			InitializeComponent();
		}
		protected override async Task OpenMayOverrideAsync()
		{
			_vm = new BinderContentVM(AudioRecorderView, CameraView);
			await _vm.OpenAsync();
			RaisePropertyChanged_UI(nameof(VM));
		}
		protected override async Task CloseMayOverrideAsync()
		{
			var vm = _vm;
			if (vm != null)
			{
				await vm.CloseAsync();
				vm.Dispose();
				VM = null;
			}

			var ar = AudioRecorderView;
			if (ar != null)
			{
				await ar.CloseAsync();
			}

			var cam = CameraView;
			if (cam != null)
			{
				await cam.CloseAsync();
			}
		}
		#endregion construct, open, close


		#region user actions
		private void OnFolderPreview_Click(object sender, ItemClickEventArgs e)
		{
			Task sss = _vm?.SetCurrentFolderAsync((e.ClickedItem as Folder)?.Id);
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

	public interface IRecorder : IOpenable
	{
		// Task<bool> StartAsync();
		Task<bool> StartAsync(StorageFile file);
		Task<bool> StopAsync();
	}
}