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
		private bool _isBackHandlersRegistered = false;

		private bool _isBackButtonAvailable = false;
		public bool IsBackButtonAvailable { get { return _isBackButtonAvailable; } private set { _isBackButtonAvailable = value; RaisePropertyChanged_UI(); } }
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
				if (!_isBackHandlersRegistered)
				{
					_isBackHandlersRegistered = true;

					if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
					{
						HardwareButtons.BackPressed += OnHardwareButtons_BackPressed;
						IsBackButtonAvailable = true;
					}
					SystemNavigationManager.GetForCurrentView().BackRequested += OnTabletSoftwareButton_BackPressed;
					IsBackButtonAvailable = SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility == AppViewBackButtonVisibility.Visible;
					//_systemMediaControls.PropertyChanged += OnSystemMediaControls_PropertyChanged;
				}
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

				_isBackHandlersRegistered = false;
			});
		}

		public void OnBackButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			e.Handled = true;
			Task back = RunFunctionWhileOpenAsyncA(GoBackMustOverride);
		}
		private void OnHardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
		{
			e.Handled = true;
			Task back = RunFunctionWhileOpenAsyncA(GoBackMustOverride);
		}
		private void OnTabletSoftwareButton_BackPressed(object sender, BackRequestedEventArgs e)
		{
			e.Handled = true; 
			// LOLLO TODO test what happens when back is pressed and a backable control is hosted in another backable control: 
			// which one responds first?
			Task back = RunFunctionWhileOpenAsyncA(GoBackMustOverride);
		}
		protected abstract void GoBackMustOverride();
	}
}
