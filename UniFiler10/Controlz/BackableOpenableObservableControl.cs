using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.Phone.UI.Input;
using Windows.UI.Core;
using Windows.UI.Xaml.Input;

namespace UniFiler10.Controlz
{
	public abstract class BackableOpenableObservableControl : OpenableObservableControl
	{
		protected override async Task<bool> OpenMayOverrideAsync()
		{
			RegisterBackEventHandlers();
			await Task.CompletedTask;
			return true;
		}
		protected override Task CloseMayOverrideAsync()
		{
			UnregisterBackEventHandlers();
			return Task.CompletedTask;
		}
		/// <summary>
		/// Registers event handlers for hardware buttons and orientation sensors, and performs an initial update of the UI rotation
		/// </summary>
		protected void RegisterBackEventHandlers()
		{
			RunInUiThread(delegate 
			{
				if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
				{
					HardwareButtons.BackPressed += OnHardwareButtons_BackPressed;
				}
				SystemNavigationManager.GetForCurrentView().BackRequested += OnTabletSoftwareButton_BackPressed;

				//_systemMediaControls.PropertyChanged += OnSystemMediaControls_PropertyChanged;
			});
		}

		/// <summary>
		/// Unregisters event handlers for hardware buttons and orientation sensors
		/// </summary>
		protected void UnregisterBackEventHandlers()
		{
			RunInUiThread(delegate
			{

				if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
				{
					HardwareButtons.BackPressed -= OnHardwareButtons_BackPressed;
				}
				SystemNavigationManager.GetForCurrentView().BackRequested -= OnTabletSoftwareButton_BackPressed;

				//_systemMediaControls.PropertyChanged -= OnSystemMediaControls_PropertyChanged;
			});
		}

		public void OnBackButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task back = RunFunctionWhileOpenAsyncA(GoBack);
		}
		private void OnHardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
		{
			Task back = RunFunctionWhileOpenAsyncA(GoBack);
		}
		private void OnTabletSoftwareButton_BackPressed(object sender, BackRequestedEventArgs e)
		{
			Task back = RunFunctionWhileOpenAsyncA(GoBack);
		}
		protected abstract void GoBack();
	}
}
