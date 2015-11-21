using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Utilz;

namespace UniFiler10.Data.Model
{
    [DataContract]
    public abstract class OpenableObservableData : ObservableData, IDisposable
    {
		protected Func<Task> _runAsSoonAsOpen = null;
		protected volatile SemaphoreSlimSafeRelease _isOpenSemaphore = null;

        protected volatile bool _isOpen = false;
        [IgnoreDataMember]
        [Ignore]
        public bool IsOpen { get { return _isOpen; } protected set { if (_isOpen != value) { _isOpen = value; RaisePropertyChanged_UI(); } } }

        protected volatile bool _isEnabled = false;
        [IgnoreDataMember]
        [Ignore]
        public bool IsEnabled { get { return _isEnabled; } protected set { if (_isEnabled != value) { _isEnabled = value; RaisePropertyChanged_UI(); } } }

        protected volatile bool _isDisposed = false;
        [IgnoreDataMember]
        [Ignore]
        public bool IsDisposed { get { return _isDisposed; } protected set { if (_isDisposed != value) { _isDisposed = value; } } }

        public void Dispose()
        {
            Dispose(true);
        }
        protected virtual void Dispose(bool isDisposing)
        {
            _isDisposed = true;
            CloseAsync().Wait();
            ClearListeners();
        }

        public virtual async Task<bool> OpenAsync(bool enable = true)
        {
            if (!_isOpen)
            {
				bool isJustOpen = false;
                if (!SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore)) _isOpenSemaphore = new SemaphoreSlimSafeRelease(1, 1);
                try
                {
                    await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
                    if (!_isOpen)
                    {
                        await OpenMayOverrideAsync().ConfigureAwait(false);

                        IsOpen = true;
						if (enable) IsEnabled = true;
						isJustOpen = true;						
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
					if (isJustOpen && _runAsSoonAsOpen != null) await _runAsSoonAsOpen();
				}
            }
            if (_isOpen && enable) await SetIsEnabledAsync(true).ConfigureAwait(false);
            return false;
        }
#pragma warning disable 1998
        protected virtual async Task OpenMayOverrideAsync() { } // LOLLO return null; dumps, so we live with the warning
#pragma warning restore 1998
        public virtual async Task<bool> CloseAsync()
        {
            if (_isOpen)
            {
                try
                {
                    await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
                    if (_isOpen)
                    {
                        IsEnabled = false;
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

        public virtual async Task<bool> SetIsEnabledAsync(bool enable)
        {
            if (_isOpen && _isEnabled != enable)
            {
                try
                {
                    await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
                    if (_isOpen && _isEnabled != enable)
                    {
                        IsEnabled = enable;
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
            if (_isOpen && _isEnabled)
            {
                try
                {
                    await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
                    if (_isOpen && _isEnabled) func();
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
		protected async Task RunFunctionWhileOpenAsyncA_MT(Action func)
		{
			if (_isOpen && _isEnabled)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen && _isEnabled) await Task.Run(func).ConfigureAwait(false);
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
            if (_isOpen && _isEnabled)
            {
                try
                {
                    await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
                    if (_isOpen && _isEnabled) return func();
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
            if (_isOpen && _isEnabled)
            {
                try
                {
                    await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
                    if (_isOpen && _isEnabled) await funcAsync().ConfigureAwait(false);
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
            if (_isOpen && _isEnabled)
            {
                try
                {
                    await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
                    if (_isOpen && _isEnabled) return await funcAsync().ConfigureAwait(false);
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
    }
}
