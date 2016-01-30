﻿using SQLite;
using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;


namespace Utilz.Data
{
	[DataContract]
	public abstract class OpenableObservableData : ObservableData
	{
		#region properties
		protected volatile SemaphoreSlimSafeRelease _isOpenSemaphore = null;

		protected volatile bool _isOpen = false;
		[IgnoreDataMember]
		[Ignore]
		public bool IsOpen { get { return _isOpen; } protected set { if (_isOpen != value) { _isOpen = value; RaisePropertyChanged_UI(); } } }

		private volatile SafeCancellationTokenSource _cts = null;
		protected SafeCancellationTokenSource Cts { get { return _cts; } }
		protected CancellationToken CancellationTokenSafe
		{
			get
			{
				try
				{
					var cts = _cts;
					if (cts != null) return cts.TokenSafe;
					else return new CancellationToken(false); // we must be optimistic, or the methods running in separate tasks will always crap out
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
					return new CancellationToken(true);
				}
			}
		}
		#endregion properties


		#region open close
		public async Task<bool> OpenAsync()
		{
			if (!_isOpen)
			{
				if (!SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore)) _isOpenSemaphore = new SemaphoreSlimSafeRelease(1, 1);
				try
				{
					await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
					if (!_isOpen)
					{
						_cts?.Dispose();
						_cts = new SafeCancellationTokenSource();

						await OpenMayOverrideAsync().ConfigureAwait(false);

						IsOpen = true;
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}

		protected virtual Task OpenMayOverrideAsync()
		{
			return Task.CompletedTask;
		}

		public virtual async Task<bool> CloseAsync()
		{
			if (_isOpen)
			{
				_cts?.CancelSafe(true);

				try
				{
					await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
					if (_isOpen)
					{
						_cts?.Dispose();
						_cts = null;

						IsOpen = false;
						await CloseMayOverrideAsync().ConfigureAwait(false);
					
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryDispose(_isOpenSemaphore);
					_isOpenSemaphore = null;
				}
			}
			return false;
		}

		protected virtual Task CloseMayOverrideAsync()
		{
			return Task.CompletedTask;
		}
		#endregion open close


		#region while open
		protected async Task<bool> RunFunctionIfOpenAsyncA(Action func)
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
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		protected async Task<bool> RunFunctionIfOpenAsyncB(Func<bool> func)
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
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		protected async Task<bool> RunFunctionIfOpenAsyncT(Func<Task> funcAsync)
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
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		protected async Task<bool> RunFunctionIfOpenAsyncTB(Func<Task<bool>> funcAsync)
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
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}

		protected async Task<bool> RunFunctionIfOpenAsyncA_MT(Action func)
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
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}

		public enum BoolWhenOpen { Yes, No, ObjectClosed, Error };

		protected async Task<BoolWhenOpen> RunFunctionIfOpenThreeStateAsyncB(Func<bool> func)
		{
			BoolWhenOpen result = BoolWhenOpen.ObjectClosed;
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen)
					{
						if (func()) result = BoolWhenOpen.Yes;
						else result = BoolWhenOpen.No;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
					{
						result = BoolWhenOpen.Error;
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
					}
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return result;
		}

		protected async Task<BoolWhenOpen> RunFunctionIfOpenThreeStateAsyncT(Func<Task> funcAsync)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen)
					{
						await funcAsync().ConfigureAwait(false);
						return BoolWhenOpen.Yes;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
					{
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
						return BoolWhenOpen.Error;
					}
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}

			return BoolWhenOpen.ObjectClosed;
		}
		#endregion while open
	}

	public interface IOpenable
	{
		Task<bool> OpenAsync();
		Task<bool> CloseAsync();
		bool IsOpen { get; }
	}
}