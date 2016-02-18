using System;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Utilz.Controlz;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UniFiler10.Views
{
	public sealed partial class SettingsPage : OpenableObservablePage
	{
		#region properties
		private SettingsVM _vm = null;
		public SettingsVM VM { get { return _vm; } set { _vm = value; RaisePropertyChanged_UI(); } }

		private readonly AnimationStarter _animationStarter = null;
		#endregion properties


		#region construct dispose open close
		public SettingsPage()
		{
			LastNavigatedPageRegKey = App.LAST_NAVIGATED_PAGE_REG_KEY;
			NavigationCacheMode = NavigationCacheMode.Enabled; // LOLLO TODO test NavigationCacheMode.Required too
			InitializeComponent();
			// LOLLO NOTE with x:Bind set on a nullable bool property, such as ToggleButton.IsChecked, FallbackValue=True and FallbackValue=False cause errors.
			// Instead, use Binding ElementName=me, Path=....
			MBView.DataContext = null; // otherwise, it will try something and run into binding errors. I am going to set its binding later.
			_animationStarter = AnimationsControl.AnimationStarter;
		}
		protected override async Task OpenMayOverrideAsync()
		{
			var briefcase = Briefcase.GetCreateInstance();
			await briefcase.OpenAsync();

			await AnimationsControl.OpenAsync();

			_vm = new SettingsVM(briefcase.MetaBriefcase, _animationStarter);
			await _vm.OpenAsync();
			_vm.MetadataChanged += OnVm_MetadataChanged;
			RaisePropertyChanged_UI(nameof(VM));

			//LayoutRoot.DataContext = VM;
			Task ccc = RunInUiThreadAsync(() => MBView.DataContext = VM.MetaBriefcase);
		}


		protected override async Task CloseMayOverrideAsync()
		{
			var vm = _vm;
			if (vm != null)
			{
				vm.MetadataChanged -= OnVm_MetadataChanged;
				await vm.CloseAsync().ConfigureAwait(false);
				vm.Dispose();
			}
			_vm = null;

			await AnimationsControl.CloseAsync().ConfigureAwait(false);
		}
		#endregion construct dispose open close


		#region user actions
		private void OnGoBack_Tapped(object sender, TappedRoutedEventArgs e)
		{
			if (Frame.CanGoBack) Frame.GoBack();
			else Frame.Navigate(typeof(BriefcasePage));
		}

		private void OnToggleElevated_Tapped(object sender, TappedRoutedEventArgs e)
		{
			MBView.DataContext = null;
			MBView.DataContext = VM.MetaBriefcase;
		}

		private void OnExport_Tapped(object sender, TappedRoutedEventArgs e)
		{
			_vm?.StartExport();
		}

		private void OnImport_Tapped(object sender, TappedRoutedEventArgs e)
		{
			_vm?.StartImport();
		}

		private void OnVm_MetadataChanged(object sender, EventArgs e)
		{
			Task upd = RunInUiThreadAsync(delegate
			{
				MBView.DataContext = null;
				MBView.DataContext = VM.MetaBriefcase;
			});
		}

		private void OnAbout_Tapped(object sender, TappedRoutedEventArgs e)
		{
			AboutFlyout.ShowAt(this);
		}
		#endregion user actions
	}
}