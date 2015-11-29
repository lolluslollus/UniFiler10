using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Views;
using Utilz;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Phone.UI.Input;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace UniFiler10.Controlz
{
	public abstract class OpenableObservablePage : ObservablePage
	{
		#region properties
		private bool _isOpenWhenSuspending = false;
		private bool _isOnMe = false;

		protected volatile SemaphoreSlimSafeRelease _isOpenSemaphore = null;

		protected volatile bool _isOpen = false;
		public bool IsOpen { get { return _isOpen; } protected set { if (_isOpen != value) { _isOpen = value; RaisePropertyChanged_UI(); } } }
		#endregion properties


		#region ctor
		public OpenableObservablePage()
		{
			Application.Current.Resuming += OnResuming;
			Application.Current.Suspending += OnSuspending;
		}
		#endregion ctor


		#region event handlers
		private async void OnSuspending(object sender, SuspendingEventArgs e)
		{
			var deferral = e.SuspendingOperation.GetDeferral();

			_isOpenWhenSuspending = _isOnMe;
			if (_isOnMe) RegistryAccess.SetValue(App.LAST_NAVIGATED_PAGE_REG_KEY, GetType().Name);
			await CloseAsync().ConfigureAwait(false);

			deferral.Complete();
		}

		private async void OnResuming(object sender, object e)
		{
			if (_isOpenWhenSuspending) await OpenAsync().ConfigureAwait(false);
		}

		protected override async void OnNavigatedTo(NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);
			_isOnMe = true;
			await OpenAsync().ConfigureAwait(false);
		}

		protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
		{
			base.OnNavigatingFrom(e);
			_isOnMe = false;
			await CloseAsync().ConfigureAwait(false);
		}
		#endregion event handlers


		#region open close
		private async Task<bool> OpenAsync(bool enable = true)
		{
			if (!_isOpen)
			{
				if (!SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore)) _isOpenSemaphore = new SemaphoreSlimSafeRelease(1, 1);
				try
				{
					await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
					if (!_isOpen)
					{
						await OpenMayOverrideAsync().ConfigureAwait(false);
						
						IsOpen = true;
						if (enable) await RunInUiThreadAsync(delegate { IsEnabled = true; }).ConfigureAwait(false);

						await RegisterBackEventHandlersAsync().ConfigureAwait(false);

						return true;
					}
				}
				catch (Exception exc)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						Logger.Add_TPL(exc.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			if (_isOpen && enable) await SetIsEnabledAsync(true).ConfigureAwait(false);
			return false;
		}

		protected virtual Task OpenMayOverrideAsync()
		{
			return Task.CompletedTask; // avoid warning
		}

		private async Task<bool> CloseAsync()
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
					if (_isOpen)
					{
						await UnregisterBackEventHandlersAsync();

						await RunInUiThreadAsync(delegate { IsEnabled = false; });
						IsOpen = false;

						await CloseMayOverrideAsync().ConfigureAwait(false);

						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryDispose(_isOpenSemaphore);
					_isOpenSemaphore = null;
				}
			}
			return false;
		}
#pragma warning disable 1998
		protected virtual async Task CloseMayOverrideAsync() { } // LOLLO return null dumps
#pragma warning restore 1998
		#endregion open close


		#region while open
		private async Task<bool> SetIsEnabledAsync(bool enable)
		{
			if (_isOpen && IsEnabled != enable)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
					if (_isOpen && IsEnabled != enable)
					{
						await RunInUiThreadAsync(delegate { IsEnabled = enable; }).ConfigureAwait(false);
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}

		protected async Task<bool> RunFunctionWhileOpenAsyncA(Action func)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen)
					{
						func();
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		protected async Task<bool> RunFunctionWhileOpenAsyncB(Func<bool> func)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen) return func();
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		protected async Task<bool> RunFunctionWhileOpenAsyncT(Func<Task> funcAsync)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen)
					{
						await funcAsync().ConfigureAwait(false);
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		protected async Task<bool> RunFunctionWhileOpenAsyncTB(Func<Task<bool>> funcAsync)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen) return await funcAsync().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		#endregion while open


		#region back
		private bool _isBackHandlersRegistered = false;
		private Task RegisterBackEventHandlersAsync()
		{
			return RunInUiThreadAsync(delegate
			{
				if (!_isBackHandlersRegistered)
				{
					_isBackHandlersRegistered = true;

					if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
					{
						HardwareButtons.BackPressed += OnHardwareButtons_BackPressed;
					}
					SystemNavigationManager.GetForCurrentView().BackRequested += OnTabletSoftwareButton_BackPressed;
				}
			});
		}
		private Task UnregisterBackEventHandlersAsync()
		{
			return RunInUiThreadAsync(delegate
			{

				if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
				{
					HardwareButtons.BackPressed -= OnHardwareButtons_BackPressed;
				}
				SystemNavigationManager.GetForCurrentView().BackRequested -= OnTabletSoftwareButton_BackPressed;

				_isBackHandlersRegistered = false;
			});
		}

		private void OnHardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
		{
			if (!e.Handled) e.Handled = GoBackMayOverride();
		}
		private void OnTabletSoftwareButton_BackPressed(object sender, BackRequestedEventArgs e)
		{
			if (!e.Handled) e.Handled = GoBackMayOverride();
		}
		/// <summary>
		/// Deals with the back requested event and returns true if the event has been dealt with
		/// </summary>
		/// <returns></returns>
		protected virtual bool GoBackMayOverride()
		{
			return false;
		}
		#endregion back
	}
}
