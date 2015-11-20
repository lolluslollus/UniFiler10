﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace UniFiler10.Controlz
{
	public abstract class OpenableObservableControl : ObservableControl //, IDisposable
	{
		//#region IDisposable
		//protected volatile bool _isDisposed = false;
		//public bool IsDisposed { get { return _isDisposed; } protected set { if (_isDisposed != value) { _isDisposed = value; } } }
		//public void Dispose()
		//{
		//    Dispose(true);
		//}
		//protected virtual void Dispose(bool isDisposing)
		//{
		//    _isDisposed = true;
		//    CloseAsync().Wait();
		//    ClearListeners();
		//}
		//#endregion IDisposable

		#region properties
		public bool OpenCloseWhenLoadedUnloaded
		{
			get { return (bool)GetValue(OpenCloseWhenLoadedUnloadedProperty); }
			set { SetValue(OpenCloseWhenLoadedUnloadedProperty, value); }
		}
		public static readonly DependencyProperty OpenCloseWhenLoadedUnloadedProperty =
			DependencyProperty.Register("OpenCloseWhenLoadedUnloaded", typeof(bool), typeof(OpenableObservableControl), new PropertyMetadata(true));
		public bool OpenCloseWhenVisibleCollapsed
		{
			get { return (bool)GetValue(OpenCloseWhenVisibleCollapsedProperty); }
			set { SetValue(OpenCloseWhenVisibleCollapsedProperty, value); }
		}
		public static readonly DependencyProperty OpenCloseWhenVisibleCollapsedProperty =
			DependencyProperty.Register("OpenCloseWhenVisibleCollapsed", typeof(bool), typeof(OpenableObservableControl), new PropertyMetadata(true));

		//protected bool _isLoaded = false;
		private bool _isOpenBeforeSuspending = false;
		#endregion properties


		#region construct
		public OpenableObservableControl()
		{
			Application.Current.Suspending += OnSuspending; // LOLLO TODO check these event handlers. Do they fire enough? Do they fire too much?
			Application.Current.Resuming += OnResuming;
			Loaded += OnLoaded;
			Unloaded += OnUnloaded;
			//this.LayoutUpdated+=
			RegisterPropertyChangedCallback(VisibilityProperty, OnVisibilityChanged);
		}
		#endregion construct


		#region event handlers
		private async void OnSuspending(object sender, SuspendingEventArgs e)
		{
			var deferral = e.SuspendingOperation.GetDeferral();
			_isOpenBeforeSuspending = _isOpen;
			await CloseAsync().ConfigureAwait(false);
			deferral.Complete();
		}

		private async void OnResuming(object sender, object o)
		{
			//if (_isLoaded) await TryOpenAsync().ConfigureAwait(false);
			if (_isOpenBeforeSuspending) await TryOpenAsync().ConfigureAwait(false);
		}

		private void OnLoaded(object sender, RoutedEventArgs e) // LOLLO VM may not be available yet when OnLoaded fires, it is required though, hence the complexity
		{
			//_isLoaded = true;
			if (OpenCloseWhenLoadedUnloaded)
			{
				//await CloseAsync().ConfigureAwait(false); LOLLO TODO why did I have this?
				Task open = TryOpenAsync();
			}
		}

		private void OnUnloaded(object sender, RoutedEventArgs e)
		{
			//_isLoaded = false;
			if (OpenCloseWhenLoadedUnloaded)
			{
				Task close = CloseAsync();
			}
		}

		private static void OnVisibilityChanged(DependencyObject obj, DependencyProperty prop)
		{
			OpenableObservableControl instance = obj as OpenableObservableControl;
			if (instance != null && instance.OpenCloseWhenVisibleCollapsed)
			{
				if (instance.Visibility == Visibility.Collapsed)
				{
					Task close = instance.CloseAsync();
				}
				else if (instance.Visibility == Visibility.Visible)
				{
					Task open = instance.TryOpenAsync();
				}
			}
		}
		#endregion event handlers


		#region open close
		protected volatile SemaphoreSlimSafeRelease _isOpenSemaphore = null;

		protected volatile bool _isOpen = false;
		public bool IsOpen { get { return _isOpen; } protected set { if (_isOpen != value) { _isOpen = value; RaisePropertyChanged_UI(); } } }

		protected async Task<bool> TryOpenAsync(bool enable = true)
		{
			if (!_isOpen)
			{
				if (!SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore)) _isOpenSemaphore = new SemaphoreSlimSafeRelease(1, 1);
				try
				{
					await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
					if (!_isOpen)
					{
						if (Visibility == Visibility.Visible || !OpenCloseWhenVisibleCollapsed)
						{
							if (await OpenMayOverrideAsync().ConfigureAwait(false))
							{
								IsOpen = true;
								if (enable) RunInUiThread(delegate { IsEnabled = true; });
								return true;
							}
						}
						else
						{
							await Logger.AddAsync("TryOpenAsync() called when the control is collapsed", Logger.ForegroundLogFilename);
						}
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

		protected virtual async Task<bool> OpenMayOverrideAsync()
		{
			await Task.CompletedTask; // avoid warning
			return true;
		}

		protected async Task<bool> CloseAsync()
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
					if (_isOpen)
					{
						//ClearListeners();
						RunInUiThread(delegate { IsEnabled = false; });
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
		protected async Task<bool> SetIsEnabledAsync(bool enable)
		{
			if (_isOpen && IsEnabled != enable)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
					if (_isOpen && IsEnabled != enable)
					{
						RunInUiThread(delegate { IsEnabled = enable; });
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

		protected async Task RunFunctionWhileOpenAsyncA(Action func)
		{
			if (_isOpen && IsEnabled)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen && IsEnabled) func();
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
		}
		protected async Task<bool> RunFunctionWhileOpenAsyncB(Func<bool> func)
		{
			if (_isOpen && IsEnabled)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen && IsEnabled) return func();
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
		protected async Task RunFunctionWhileOpenAsyncT(Func<Task> funcAsync)
		{
			if (_isOpen && IsEnabled)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen && IsEnabled) await funcAsync().ConfigureAwait(false);
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
		}
		protected async Task<bool> RunFunctionWhileOpenAsyncTB(Func<Task<bool>> funcAsync)
		{
			if (_isOpen && IsEnabled)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen && IsEnabled) return await funcAsync().ConfigureAwait(false);
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
