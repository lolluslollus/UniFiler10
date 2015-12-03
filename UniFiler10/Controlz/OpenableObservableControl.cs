using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Views;
using Utilz;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace UniFiler10.Controlz
{
	/// <summary>
	/// This is a smarter UserControl that can be opened and closed, asynchronously. 
	/// It will stay disabled as long as it is closed.
	/// Do not bind to IsEnabled, but to IsEnabledOverride instead.
	/// </summary>
	public abstract class OpenableObservableControl : ObservableControl
	{
		#region properties
		protected volatile SemaphoreSlimSafeRelease _isOpenSemaphore = null;

		protected volatile bool _isOpen = false;
		public bool IsOpen { get { return _isOpen; } protected set { if (_isOpen != value) { _isOpen = value; RaisePropertyChanged_UI(); } } }

		protected volatile bool _isEnabledAllowed = false;
		public bool IsEnabledAllowed
		{
			get { return _isEnabledAllowed; }
			protected set
			{
				if (_isEnabledAllowed != value)
				{
					_isEnabledAllowed = value; RaisePropertyChanged_UI();
					Task upd = UpdateIsEnabledAsync();
				}
			}
		}

		public bool IsEnabledOverride
		{
			get { return (bool)GetValue(IsEnabledOverrideProperty); }
			set { SetValue(IsEnabledOverrideProperty, value); }
		}
		public static readonly DependencyProperty IsEnabledOverrideProperty =
			DependencyProperty.Register("IsEnabledOverride", typeof(bool), typeof(OpenableObservableControl), new PropertyMetadata(true, OnIsEnabledOverrideChanged));
		private static void OnIsEnabledOverrideChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			Task upd = (obj as OpenableObservableControl)?.UpdateIsEnabledAsync();
		}
		private Task UpdateIsEnabledAsync()
		{
			return RunInUiThreadAsync(delegate
			{
				IsEnabled = IsEnabledAllowed && IsEnabledOverride;
			});
		}
		#endregion properties


		#region ctor
		public OpenableObservableControl() : base()
		{
			Task upd = UpdateIsEnabledAsync();
		}
		#endregion ctor


		#region open close
		public async Task<bool> OpenAsync(bool enable = true)
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
						if (enable) await RunInUiThreadAsync(delegate
						{
							IsEnabledAllowed = true;
						}).ConfigureAwait(false);
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

		public async Task<bool> CloseAsync()
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
					if (_isOpen)
					{
						IsEnabledAllowed = false;
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
						IsEnabledAllowed = enable;
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
	}
}
