using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilz;
using Windows.Foundation.Metadata;
using Windows.Phone.UI.Input;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace UniFiler10.Controlz
{
	public abstract class BackableOpenableObservableControl : OpenableObservableControl
	{
		// LOLLO NOTE test what happens when back is pressed and a backable control is hosted in another backable control: 
		// which one responds first? The host! So a backable control is designed to ignore back pressed, if any backable children are registered. 
		// LOLLO TODO check the hardware back button as well!

		private bool _isBackHandlersRegistered = false;

		private bool _isBackButtonAvailable = false;
		public bool IsBackButtonAvailable { get { return _isBackButtonAvailable; } private set { _isBackButtonAvailable = value; RaisePropertyChanged_UI(); } }

		private static SemaphoreSlimSafeRelease _backHandlerSemaphore = new SemaphoreSlimSafeRelease(1, 1);

		private BackableOpenableObservableControl _backableParent = null;
		private List<BackableOpenableObservableControl> _backableChildren = new List<BackableOpenableObservableControl>();
		public void AddBackableChild(BackableOpenableObservableControl child)
		{
			if (!_backableChildren.Contains(child)) _backableChildren.Add(child);
		}
		public void RemoveBackableChild(BackableOpenableObservableControl child)
		{
			_backableChildren.Remove(child);
		}


		#region construct open close
		public BackableOpenableObservableControl()
		{
			Loaded += OnLoaded;
			Unloaded += OnUnloaded;
		}
		protected override async Task<bool> TryOpenMayOverrideAsync()
		{
			if (await base.TryOpenMayOverrideAsync().ConfigureAwait(false))
			{
				RegisterBackEventHandlers();
				return true;
			}
			else
			{
				return false;
			}
		}
		protected override async Task CloseMayOverrideAsync()
		{
			await base.CloseMayOverrideAsync().ConfigureAwait(false);
			UnregisterBackEventHandlers();
		}
		#endregion construct open close


		#region event helpers
		private void RegisterBackEventHandlers()
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
					if (!_isBackButtonAvailable) IsBackButtonAvailable = SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility == AppViewBackButtonVisibility.Visible;
				}
			});
		}
		private void UnregisterBackEventHandlers()
		{
			RunInUiThread(delegate
			{

				if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
				{
					HardwareButtons.BackPressed -= OnHardwareButtons_BackPressed;
				}
				SystemNavigationManager.GetForCurrentView().BackRequested -= OnTabletSoftwareButton_BackPressed;

				_isBackHandlersRegistered = false;
			});
		}
		#endregion event helpers


		#region event handlers
		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			_backableParent = GetParent();
			_backableParent?.AddBackableChild(this);
		}
		private BackableOpenableObservableControl GetParent()
		{
			var parent0 = VisualTreeHelper.GetParent(this);
			while (parent0 != null && !(parent0 is BackableOpenableObservableControl))
			{
				var parent1 = VisualTreeHelper.GetParent(parent0);
				parent0 = parent1;
			}
			return parent0 as BackableOpenableObservableControl;
		}
		private void OnUnloaded(object sender, RoutedEventArgs e)
		{
			_backableParent?.RemoveBackableChild(this);
		}

		public async void OnOwnBackButton_Tapped(object sender, TappedRoutedEventArgs e) // this method is public so XAML can see it
		{
			try
			{
				await _backHandlerSemaphore.WaitAsync();
				if (!e.Handled) e.Handled = await GoBack();
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_backHandlerSemaphore);
			}
		}
		private async void OnHardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
		{
			try
			{
				await _backHandlerSemaphore.WaitAsync();
				if (!e.Handled) e.Handled = await GoBack();
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_backHandlerSemaphore);
			}
		}
		private async void OnTabletSoftwareButton_BackPressed(object sender, BackRequestedEventArgs e)
		{
			try
			{
				await _backHandlerSemaphore.WaitAsync();
				if (!e.Handled) e.Handled = await GoBack();
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_backHandlerSemaphore);
			}
		}
		#endregion event handlers

		
		#region go back
		/// <summary>
		/// Returns true if it goes back or a child went back
		/// </summary>
		/// <returns></returns>
		protected async Task<bool> GoBack()
		{
			if (_backableChildren.Count <= 0)
			{
				return await RunFunctionWhileOpenAsyncA(GoBackMustOverride).ConfigureAwait(false);
			}
			else
			{
				foreach (var child in _backableChildren)
				{
					if (await child.GoBack()) return true;
				}
				return await RunFunctionWhileOpenAsyncA(GoBackMustOverride).ConfigureAwait(false);
			}
		}
		protected abstract void GoBackMustOverride();
		#endregion go back
	}
}
