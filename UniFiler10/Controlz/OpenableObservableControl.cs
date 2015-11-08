using System;
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
    public class OpenableObservableControl : UserControl, INotifyPropertyChanged //, IDisposable
    {
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void ClearListeners()
        {
            PropertyChanged = null;
        }
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected void RaisePropertyChanged_UI([CallerMemberName] string propertyName = "")
        {
            try
            {
                if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                }
                else
                {
                    IAsyncAction ui = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
            }
        }
        #endregion INotifyPropertyChanged

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


        public bool OpenCloseWhenLoadedUnloaded
        {
            get { return (bool)GetValue(OpenCloseWhenLoadedUnloadedProperty); }
            set { SetValue(OpenCloseWhenLoadedUnloadedProperty, value); }
        }
        public static readonly DependencyProperty OpenCloseWhenLoadedUnloadedProperty =
            DependencyProperty.Register("OpenCloseWhenLoadedUnloaded", typeof(bool), typeof(OpenableObservableControl), new PropertyMetadata(true));


        #region load unload
        public OpenableObservableControl()
        {
            Application.Current.Suspending += OnApplication_Suspending; // LOLLO TODO check these event handlers
            Application.Current.Resuming += OnApplication_Resuming;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }
        private async void OnApplication_Suspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            await CloseAsync().ConfigureAwait(false);
            deferral.Complete();
        }

        private async void OnApplication_Resuming(object sender, object o)
        {
            if (_isLoaded) await TryOpenAsync().ConfigureAwait(false);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e) // LOLLO VM may not be available yet when OnLoaded fires, it is required though, hence the complexity
        {
            if (OpenCloseWhenLoadedUnloaded)
            {
                _isLoaded = true;
                await CloseAsync().ConfigureAwait(false);
                await TryOpenAsync().ConfigureAwait(false);
            }
        }

        private async void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (OpenCloseWhenLoadedUnloaded)
            {
                _isLoaded = false;
                await CloseAsync().ConfigureAwait(false);
            }
        }

        protected bool _isLoaded = false;
        #endregion load unload

        #region openable
        protected volatile SemaphoreSlimSafeRelease _isOpenSemaphore = null;

        protected volatile bool _isOpen = false;
        public bool IsOpen { get { return _isOpen; } protected set { if (_isOpen != value) { _isOpen = value; RaisePropertyChanged_UI(); } } }

        protected void SetIsEnabled(bool newValue)
        {
            if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
            {
                IsEnabled = newValue;
            }
            else
            {
                IAsyncAction ui = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
                {
                    IsEnabled = newValue;
                });
            }
        }

        public async Task<bool> TryOpenAsync(bool enable = true)
        {
            if (!_isOpen)
            {
                if (!SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore)) _isOpenSemaphore = new SemaphoreSlimSafeRelease(1, 1);
                try
                {
                    await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
                    if (!_isOpen)
                    {
                        if (await OpenMayOverrideAsync().ConfigureAwait(false))
                        {
                            IsOpen = true;
                            if (enable) SetIsEnabled(true);
                            return true;
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

        public async Task<bool> CloseAsync()
        {
            if (_isOpen)
            {
                try
                {
                    await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
                    if (_isOpen)
                    {
                        //ClearListeners();
                        SetIsEnabled(false);
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

        public async Task<bool> SetIsEnabledAsync(bool enable)
        {
            if (_isOpen && IsEnabled != enable)
            {
                try
                {
                    await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
                    if (_isOpen && IsEnabled != enable)
                    {
                        SetIsEnabled(enable);
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

        public async Task RunFunctionWhileOpenAsyncA(Action func)
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
        public async Task<bool> RunFunctionWhileOpenAsyncB(Func<bool> func)
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
        public async Task RunFunctionWhileOpenAsyncT(Func<Task> funcAsync)
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
        public async Task<bool> RunFunctionWhileOpenAsyncTB(Func<Task<bool>> funcAsync)
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
        #endregion openable
    }
}
