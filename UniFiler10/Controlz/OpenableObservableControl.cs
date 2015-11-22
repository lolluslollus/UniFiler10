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
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace UniFiler10.Controlz
{
	public abstract class OpenableObservableControl : ObservableControl //, IDisposable
	{
		#region events
		public event EventHandler Opened;
		public event EventHandler Closing;
		#endregion events


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
		/// <summary>
		/// If the parent is made invisible, the child is not necessarily made invisible, so CloseAsync() may not fire.
		/// Use this property on a child to make it open and close, whenever the parent does.
		/// Leave it blank if there is no OpenableControl parent or if you have special needs.
		/// </summary>
		//public OpenableObservableControl OpenableObservableParent
		//{
		//	get { return (OpenableObservableControl)GetValue(OpenableObservableParentProperty); }
		//	set { SetValue(OpenableObservableParentProperty, value); }
		//}
		//public static readonly DependencyProperty OpenableObservableParentProperty =
		//	DependencyProperty.Register("OpenableObservableParent", typeof(OpenableObservableControl), typeof(OpenableObservableControl), new PropertyMetadata(null, OnParentChanged));
		//private static void OnParentChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		//{
		//	var instance = obj as OpenableObservableControl;
		//	if (args?.OldValue is OpenableObservableControl) instance?.UnregisterParentHandlers(args.OldValue as OpenableObservableControl);
		//	instance?.RegisterParentHandlers();
		//}

		private OpenableObservableControl _openableObservableParent = null;

		private bool _isLoaded = false;
		private bool _isOpenBeforeSuspending = false;
		#endregion properties


		#region construct and dispose
		private long _visibilityChangedToken = default(long);
		public OpenableObservableControl()
		{
			RegisterApplicationHandlers();
			Loaded += OnLoaded;
			Unloaded += OnUnloaded;

			//RegisterParentHandlers();
			_visibilityChangedToken = RegisterPropertyChangedCallback(VisibilityProperty, OnVisibilityChanged);
		}
		// LOLLO NOTE check the interaction behaviour at http://stackoverflow.com/questions/502761/disposing-wpf-user-controls if you really want to make this disposable.
		// LOLLO NOTE also this is interesting: http://joeduffyblog.com/2005/04/08/dg-update-dispose-finalization-and-resource-management/
		// LOLLO TODO you may want to check VisualTreeHelper.DisconnectChildrenRecursive()

		//protected volatile bool _isDisposed = false;
		//public bool IsDisposed { get { return _isDisposed; } protected set { if (_isDisposed != value) { _isDisposed = value; } } }
		//public void Dispose()
		//{
		//	Dispose(true);

		//	GC.SuppressFinalize(this);
		//}
		//protected virtual void Dispose(bool isDisposing)
		//{
		//	if (_isDisposed) return;
		//	_isDisposed = true;

		//	UnregisterApplicationHandlers();
		//	Loaded -= OnLoaded;
		//	Unloaded -= OnUnloaded;

		//	UnregisterParentHandlers(OpenableObservableParent);
		//	UnregisterPropertyChangedCallback(VisibilityProperty, _visibilityChangedToken);

		//	CloseAsync().Wait();
		//	ClearListeners();
		//}
		//~OpenableObservableControl()
		//{
		//	Dispose(false);
		//}
		#endregion construct and dispose


		#region event helpers
		private bool _isApplicationHandlersRegistered = false;
		private void RegisterApplicationHandlers()
		{
			if (!_isApplicationHandlersRegistered)
			{
				_isApplicationHandlersRegistered = true;
				Application.Current.Suspending += OnSuspending; // LOLLO TODO check these event handlers. Do they fire enough? Do they fire too much? Do the handlers stick around too long, preventing GC?
				Application.Current.Resuming += OnResuming;
			}
		}
		private void UnregisterApplicationHandlers() // lollo todo the application will always hold an instance of this, even if it is not used anymore...
		{
			Application.Current.Suspending -= OnSuspending;
			Application.Current.Resuming -= OnResuming;
			_isApplicationHandlersRegistered = false;
		}

		private bool _isParentHandlersRegistered = false;
		private void RegisterParentHandlers()
		{
			if (!_isParentHandlersRegistered && _openableObservableParent != null)
			{
				_isParentHandlersRegistered = true;
				// OpenableObservableParent.RegisterPropertyChangedCallback(VisibilityProperty, this.OnParentVisibilityChanged);
				_openableObservableParent.Opened += OnOpenableObservableParent_Opened;
				_openableObservableParent.Closing += OnOpenableObservableParent_Closing;
			}
		}
		private void UnregisterParentHandlers(OpenableObservableControl parent)
		{
			if (parent != null)
			{
				// parent.UnregisterPropertyChangedCallback(VisibilityProperty, this.OnParentVisibilityChanged);
				parent.Opened -= OnOpenableObservableParent_Opened;
				parent.Closing -= OnOpenableObservableParent_Closing;
				_isParentHandlersRegistered = false;
			}
		}
		#endregion event helpers


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

		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			_isLoaded = true;
			if (OpenCloseWhenLoadedUnloaded) { Task open = TryOpenAsync(); }
			_openableObservableParent = GetParent();
			RegisterParentHandlers();
		}

		private OpenableObservableControl GetParent()
		{
			var parent0 = VisualTreeHelper.GetParent(this);
			while (parent0 != null && !(parent0 is OpenableObservableControl))
			{
				var parent1 = VisualTreeHelper.GetParent(parent0);
				parent0 = parent1;
			}
			return parent0 as OpenableObservableControl;
		}

		private void OnUnloaded(object sender, RoutedEventArgs e)
		{
			_isLoaded = false;
			if (OpenCloseWhenLoadedUnloaded) { Task close = CloseAsync(); }
			UnregisterParentHandlers(_openableObservableParent);
		}

		private void OnOpenableObservableParent_Closing(object sender, EventArgs e)
		{
			Task close = CloseAsync();
		}

		private void OnOpenableObservableParent_Opened(object sender, EventArgs e)
		{
			Task open = TryOpenAsync();
		}

		///// <summary>
		///// If the parent is made invisible, the child is not necessarily made invisible, so it could stay open.
		///// This method makes the child open and close, if it must, whenever the parent becomes visible or invisible.
		///// </summary>
		///// <param name="obj"></param>
		///// <param name="prop"></param>
		//private void OnParentVisibilityChanged(DependencyObject obj, DependencyProperty prop)
		//{
		//	OpenableObservableControl parent = obj as OpenableObservableControl;
		//	if (OpenCloseWhenVisibleCollapsed)
		//	{
		//		if (parent.Visibility == Visibility.Collapsed)
		//		{
		//			Task close = CloseAsync();
		//		}
		//		else if (parent.Visibility == Visibility.Visible)
		//		{
		//			Task open = TryOpenAsync();
		//		}
		//	}
		//}
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
						if ((Visibility == Visibility.Visible || !OpenCloseWhenVisibleCollapsed) && (_isLoaded || !OpenCloseWhenLoadedUnloaded))
						{
							if (await OpenMayOverrideAsync().ConfigureAwait(false))
							{
								IsOpen = true;
								if (enable) RunInUiThread(delegate { IsEnabled = true; });
								Opened?.Invoke(this, EventArgs.Empty);
								return true;
							}
						}
						else
						{
							if (GetType() != typeof(AudioRecorderView))
							{
								// await Logger.AddAsync("TryOpenAsync() called when the control is collapsed or unloaded", Logger.ForegroundLogFilename).ConfigureAwait(false);
							}
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
						Closing?.Invoke(this, EventArgs.Empty);
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

		protected async Task<bool> RunFunctionWhileOpenAsyncA(Action func)
		{
			if (_isOpen && IsEnabled)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen && IsEnabled)
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
		protected async Task<bool> RunFunctionWhileOpenAsyncT(Func<Task> funcAsync)
		{
			if (_isOpen && IsEnabled)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen && IsEnabled)
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
