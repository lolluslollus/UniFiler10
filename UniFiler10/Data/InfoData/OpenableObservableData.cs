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
	public abstract class OpenableObservableData : ObservableData, IDisposable, IOpenable
	{
		protected volatile SemaphoreSlimSafeRelease _isOpenSemaphore = null;

		protected volatile bool _isOpen = false;
		[IgnoreDataMember]
		[Ignore]
		public bool IsOpen { get { return _isOpen; } protected set { if (_isOpen != value) { _isOpen = value; RaisePropertyChanged_UI(); } } }

		protected volatile bool _isOpenOrOpening = false;
		[IgnoreDataMember]
		[Ignore]
		public bool IsOpenOrOpening { get { return _isOpenOrOpening; } protected set { if (_isOpenOrOpening != value) { _isOpenOrOpening = value; RaisePropertyChanged_UI(); } } }

		protected volatile bool _isDisposed = false;
		[IgnoreDataMember]
		[Ignore]
		public bool IsDisposed { get { return _isDisposed; } protected set { if (_isDisposed != value) { _isDisposed = value; } } }

		//[IgnoreDataMember]
		//[Ignore]
		//protected bool IsRunningUnderSemaphore { get { return SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore) && _isOpenSemaphore.CurrentCount > 0; } }

		//protected List<Func<Task>> _runAsSoonAsOpens = new List<Func<Task>>();

		public void Dispose()
		{
			Dispose(true);
		}
		protected virtual void Dispose(bool isDisposing)
		{
			_isDisposed = true;
			CloseAsync().Wait();
			ClearListeners();

			//if (_runAsSoonAsOpens.Count > 0) Logger.Add_TPL("disposed, _runAsSoonAsOpens not cleared", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
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
						IsOpenOrOpening = true;

						await OpenMayOverrideAsync().ConfigureAwait(false);

						IsOpen = true;
						return true;
					}
				}
				catch (Exception exc)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + exc.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);

					//if (_runAsSoonAsOpens.Count > 0)
					//{
					//	Logger.Add_TPL("_runAsSoonAsOpens about to be started", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
					//	Logger.Add_TPL("IsOpen = " + _isOpen, Logger.AppEventsLogFilename, Logger.Severity.Info, false);
					//}
					//if (_isOpen)
					//{
					//	foreach (var funcAsync in _runAsSoonAsOpens)
					//	{
					//		Logger.Add_TPL("_runAsSoonAsOpens task about to start", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
					//		await RunFunctionIfOpenAsyncT(funcAsync);
					//		Logger.Add_TPL("_runAsSoonAsOpens task completed", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
					//	}
					//	if (_runAsSoonAsOpens.Count > 0) Logger.Add_TPL("_runAsSoonAsOpens about to be cleared", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
					//	_runAsSoonAsOpens.Clear();						
					//}
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
						IsOpen = IsOpenOrOpening = false;

						//_runAsSoonAsOpens.Clear();
						//Logger.Add_TPL("_runAsSoonAsOpens cleared", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
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
#pragma warning disable 1998
		protected virtual async Task CloseMayOverrideAsync() { } // LOLLO return null dumps
#pragma warning restore 1998

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
		protected async Task<bool> RunFunctionIfOpenAsyncB_MT(Func<bool> func)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen)
					{
						bool isOk = await Task.Run(func).ConfigureAwait(false);
						return isOk;
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

		//protected async Task<bool> RunFunctionIfOpenAsyncT(Func<Task> funcAsync, bool scheduleIfClosed = false)
		//{
		//	if (_isOpen)
		//	{
		//		try
		//		{
		//			await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
		//			if (_isOpen)
		//			{
		//				await funcAsync().ConfigureAwait(false);
		//				return true;
		//			}
		//			else if (scheduleIfClosed)
		//			{
		//				_runAsSoonAsOpens.Add(funcAsync);
		//				Logger.Add_TPL("record added to _runAsSoonAsOpens within semaphore", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		//			}
		//		}
		//		catch (Exception ex)
		//		{
		//			if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
		//				await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
		//		}
		//		finally
		//		{
		//			SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
		//		}
		//	}
		//	else if (scheduleIfClosed)
		//	{
		//		_runAsSoonAsOpens.Add(funcAsync);
		//		Logger.Add_TPL("record added to _runAsSoonAsOpens outside semaphore", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		//		//try
		//		//{
		//		//	await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
		//		//	_runAsSoonAsOpens.Add(funcAsync);
		//		//	Logger.Add_TPL("record added to _runAsSoonAsOpens", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		//		//}
		//		//catch (Exception ex)
		//		//{
		//		//	if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
		//		//		await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
		//		//}
		//		//finally
		//		//{
		//		//	SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
		//		//}
		//	}

		//	return false;
		//}

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

		//protected async Task<BoolWhenOpen> RunFunctionIfOpenThreeStateAsyncT(Func<Task> funcAsync, bool scheduleIfClosed = false)
		//{
		//	if (_isOpen)
		//	{
		//		try
		//		{
		//			await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
		//			if (_isOpen)
		//			{
		//				await funcAsync().ConfigureAwait(false);
		//				return BoolWhenOpen.Yes;
		//			}
		//			else if (scheduleIfClosed)
		//			{
		//				_runAsSoonAsOpens.Add(funcAsync);
		//				Logger.Add_TPL("record added to _runAsSoonAsOpens within semaphore", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		//			}
		//		}
		//		catch (Exception ex)
		//		{
		//			if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
		//				await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
		//		}
		//		finally
		//		{
		//			SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
		//		}
		//	}
		//	else if (scheduleIfClosed)
		//	{
		//		_runAsSoonAsOpens.Add(funcAsync);
		//		Logger.Add_TPL("record added to _runAsSoonAsOpens outside semaphore", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		//		//try
		//		//{
		//		//	await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
		//		//	_runAsSoonAsOpens.Add(funcAsync);
		//		//	Logger.Add_TPL("record added to _runAsSoonAsOpens", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		//		//}
		//		//catch (Exception ex)
		//		//{
		//		//	if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
		//		//		await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
		//		//}
		//		//finally
		//		//{
		//		//	SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
		//		//}
		//	}

		//	return false;
		//}

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
	}

	public interface IOpenable
	{
		Task<bool> OpenAsync();
		Task<bool> CloseAsync();
		bool IsOpen { get; }
	}
}