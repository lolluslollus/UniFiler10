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
		protected volatile SemaphoreSlimSafeRelease _isOpenSemaphore = null;

        protected volatile bool _isOpen = false;
        [IgnoreDataMember]
        [Ignore]
        public bool IsOpen { get { return _isOpen; } protected set { if (_isOpen != value) { _isOpen = value; RaisePropertyChanged_UI(); } } }

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

        public virtual async Task<bool> OpenAsync()
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
		protected async Task<bool> RunFunctionWhileOpenAsyncA_MT(Action func)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen)
					{
						await Task.Run(func).ConfigureAwait(false);
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
    }
}