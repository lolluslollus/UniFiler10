﻿using System;
using System.Threading.Tasks;
using Windows.Media.Capture;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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
			//MBView.DataContext = null; // otherwise, it will try something and run into binding errors. I am going to set its binding later.
			_animationStarter = AnimationsControl.AnimationStarter;
		}
		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			var briefcase = Briefcase.GetCreateInstance();
			await briefcase.OpenAsync();

			await AnimationsControl.OpenAsync();

			_vm = new SettingsVM(briefcase, _animationStarter);
			await _vm.OpenAsync();
			RaisePropertyChanged_UI(nameof(VM));

			MBView.Refresh();
		}


		protected override async Task CloseMayOverrideAsync()
		{
			var vm = _vm;
			if (vm != null)
			{
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
			MBView.Refresh();
		}

		private void OnExport_Tapped(object sender, TappedRoutedEventArgs e)
		{
			_vm?.StartExport();
		}

		private void OnImport_Tapped(object sender, TappedRoutedEventArgs e)
		{
			_vm?.StartImport();
		}

		private void OnAbout_Tapped(object sender, TappedRoutedEventArgs e)
		{
			AboutFlyout.ShowAt(this);
		}

		private void OnCameraResChanged(object sender, RoutedEventArgs e)
		{
			var ss = sender as FrameworkElement;
			try
			{
				if (ss == null) return;
				var tag = int.Parse(ss.Tag.ToString());
				VM.Briefcase.CameraCaptureResolution = (CameraCaptureUIMaxPhotoResolution)tag;
			}
			catch { }
		}

		private void OnIsWantToUseOneDrive_Toggled(object sender, RoutedEventArgs e)
		{
			// Only allow this method firing when the toggle event comes from user interaction.
			var ts = sender as ToggleSwitch;
			if (ts == null || !_isWantToUseOneDrive_PointerJustRelleased) return;

			_isWantToUseOneDrive_PointerJustRelleased = false;
			VM?.SetIsWantToUseOneDriveAsync(ts.IsOn);
		}
		private bool _isWantToUseOneDrive_PointerJustRelleased = false;
		private void OnIsWantToUseOneDrive_PointerReleased(object sender, PointerRoutedEventArgs e)
		{
			var ts = sender as ToggleSwitch;
			if (ts == null) return;
			_isWantToUseOneDrive_PointerJustRelleased = true;
		}

		private void OnRetry_Tapped(object sender, TappedRoutedEventArgs e)
		{
			VM?.RetrySyncFromOneDriveAsync();
		}
		#endregion user actions
	}
}