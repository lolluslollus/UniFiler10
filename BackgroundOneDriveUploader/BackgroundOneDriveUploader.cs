using System;
using UniFiler10.Data.Model;
using Utilz;
using Windows.ApplicationModel.Background;


namespace BackgroundTasks
{
	// check this: http://msdn.microsoft.com/en-us/library/windowsphone/develop/hh202942(v=vs.105).aspx
	//
	// A background task always implements the IBackgroundTask interface. 
	// It also requires its own project, compiled as a windows runtime component.
	// Reference it in the main project and set CopyLocal = true
	// Add BackgroundTasks.BackgroundOneDriveUploader (ie NamespaceName.className) to the "declarations" section of the appmanifest.
	// Throughout the background task, calls to CoreApplication.MainView.CoreWindow.Dispatcher result in errors.
	// Debugging may be easier on x86.

	public sealed class BackgroundOneDriveUploader : IBackgroundTask
	{
		private volatile SafeCancellationTokenSource _cts = null;
		private volatile BackgroundTaskDeferral _deferral = null;
		private volatile IBackgroundTaskInstance _taskInstance = null;

		/// <summary>
		/// The Run method is the entry point of a background task.
		/// </summary>
		/// <param name="taskInstance"></param>
		public async void Run(IBackgroundTaskInstance taskInstance)
		{
			try
			{
				// LOLLO Background tasks may take up max 40 MB. There is also a time limit I believe.
				// We should keep it as stupid as possible,
				// so we only add a line to the db without reading anything.

				// Query BackgroundWorkCost
				// Guidance: If BackgroundWorkCost is high, then perform only the minimum amount
				// of work in the background task and return immediately.

				_deferral = taskInstance.GetDeferral();

				_taskInstance = taskInstance;
				_taskInstance.Canceled += OnCanceled;

				_cts = new SafeCancellationTokenSource();
				var cancToken = _cts.Token;

				// LOLLO the following fails with an uncatchable exception "System.ArgumentException use of undefined keyword value 1 for event taskscheduled"
				// only in the background task and only if called before GetDeferral and only if awaited
				Logger.Add_TPL("BackgroundOneDriveUploader started", Logger.BackgroundLogFilename, Logger.Severity.Info, false);

				//if (GetLocBackgroundTaskSemaphoreManager.TryOpenExisting())
				//	return; // the app is running, it will catch the background task running: do nothing

				_taskInstance.Progress = 1;
				// we don't need this but we leave it in case we change something and we want to check when the bkg task starts.

				var bc = Briefcase.GetCreateInstance(true);
				await bc.OpenAsync().ConfigureAwait(false);
				if (bc.RuntimeData?.IsConnectionAvailable == true)
				{
					if (cancToken.IsCancellationRequested) return;
					await bc.MetaBriefcase.SaveLocalFileToOneDriveAsync(cancToken).ConfigureAwait(false);
				}
			}
			catch (ObjectDisposedException) // comes from the cts
			{
				Logger.Add_TPL("ObjectDisposedException", Logger.BackgroundLogFilename, Logger.Severity.Info, false);
			}
			catch (OperationCanceledException) // comes from the cts
			{
				Logger.Add_TPL("OperationCanceledException", Logger.BackgroundLogFilename, Logger.Severity.Info, false);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.BackgroundLogFilename).ConfigureAwait(false);
			}
			finally
			{
				_cts?.Dispose();
				_cts = null;
				if (_taskInstance != null) _taskInstance.Canceled -= OnCanceled;
				Logger.Add_TPL("BackgroundOneDriveUploader ended", Logger.BackgroundLogFilename, Logger.Severity.Info, false);
				_deferral?.Complete();
			}
		}

		private async void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
		{
			_cts?.CancelSafe(true);
			await Logger.AddAsync(
				"Ending method BackgroundOneDriveUploader.OnCanceledAsync() with reason = " + reason,
				Logger.BackgroundCancelledLogFilename,
				Logger.Severity.Info,
				false).ConfigureAwait(false);
		}
	}
}