using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Constants;
using UniFiler10.Data.Metadata;
using Utilz;
using Utilz.Data;
using Windows.ApplicationModel.Background;

namespace UniFiler10.Services
{
	public class BackgroundTaskHelper : OpenableObservableData
	{
		#region properties
		private Tuple<bool, string> _uploadToOneDriveStatus = Tuple.Create(false, "");
		public Tuple<bool, string> UploadToOneDriveStatus { get { return _uploadToOneDriveStatus; } private set { _uploadToOneDriveStatus = value; RaisePropertyChanged_UI(); } }

		private IBackgroundTaskRegistration _oduBkgTaskReg = null;
		private ApplicationTrigger _updateOneDriveTrigger = null;
		#endregion properties


		#region lifecycle
		private static BackgroundTaskHelper _instance = null;
		public static BackgroundTaskHelper Instance
		{
			get
			{
				lock (_instanceLocker)
				{
					return _instance;
				}
			}
		}

		private static readonly object _instanceLocker = new object();
		public static BackgroundTaskHelper GetInstance()
		{
			lock (_instanceLocker)
			{
				if (_instance == null)
				{
					_instance = new BackgroundTaskHelper();
				}
				return _instance;
			}
		}

		private BackgroundTaskHelper() { }

		protected override async Task OpenMayOverrideAsync()
		{
			UploadToOneDriveStatus = await TryRegisterUploadToOneDriveBackgroundTaskAsync().ConfigureAwait(false);
			MetaBriefcase.UpdateOneDriveMetaBriefcaseRequested += OnMetaBriefcase_UpdateOneDriveMetaBriefcaseRequested;
		}
		protected override Task CloseMayOverrideAsync()
		{
			MetaBriefcase.UpdateOneDriveMetaBriefcaseRequested -= OnMetaBriefcase_UpdateOneDriveMetaBriefcaseRequested;
			return Task.CompletedTask;
		}
		#endregion lifecycle


		#region services
		private static void UnregisterTaskIfAlreadyRegistered()
		{
			//return (from cur in BackgroundTaskRegistration.AllTasks
			//		where cur.Value.Name == ConstantData.GET_LOCATION_BACKGROUND_TASK_NAME
			//		select cur.Value).FirstOrDefault();
			var tasks = new List<IBackgroundTaskRegistration>();
			foreach (var cur in BackgroundTaskRegistration.AllTasks.Where(task => task.Value.Name == ConstantData.ODU_BACKGROUND_TASK_NAME))
			{
				tasks.Add(cur.Value);
			}
			foreach (var task in tasks)
			{
				task.Unregister(false);
			}
		}

		private async Task<Tuple<bool, string>> TryRegisterUploadToOneDriveBackgroundTaskAsync()
		{
			bool isOk = false;
			string msg = string.Empty;

			string errorMsg = string.Empty;
			BackgroundAccessStatus backgroundAccessStatus = BackgroundAccessStatus.Unspecified;

			_oduBkgTaskReg = null;
			UnregisterTaskIfAlreadyRegistered(); // I need to unregister and register anew coz I need to obtain the trigger

			try
			{
				// Get permission for a background task from the user. If the user has already answered once,
				// this does nothing and the user must manually update their preference via PC Settings.
				Task<BackgroundAccessStatus> requestAccess = null;
				await RunInUiThreadIdleAsync(() => requestAccess = BackgroundExecutionManager.RequestAccessAsync().AsTask()).ConfigureAwait(false);
				backgroundAccessStatus = await requestAccess.ConfigureAwait(false);

				// Regardless of the answer, register the background task. If the user later adds this application
				// to the lock screen, the background task will be ready to run.
				// Create a new background task builder
				BackgroundTaskBuilder bkgTaskBuilder = new BackgroundTaskBuilder
				{
					Name = ConstantData.ODU_BACKGROUND_TASK_NAME,
					TaskEntryPoint = ConstantData.ODU_BACKGROUND_TASK_ENTRY_POINT
				};

				_updateOneDriveTrigger = new ApplicationTrigger();
				bkgTaskBuilder.SetTrigger(_updateOneDriveTrigger);

				// Register the background task
				_oduBkgTaskReg = bkgTaskBuilder.Register();
			}
			catch (Exception ex)
			{
				errorMsg = ex.ToString();
				backgroundAccessStatus = BackgroundAccessStatus.Denied;
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}

			switch (backgroundAccessStatus)
			{
				case BackgroundAccessStatus.Unspecified:
					msg = "Cannot run in background, enable it in the \"Battery Saver\" app";
					break;
				case BackgroundAccessStatus.Denied:
					msg = string.IsNullOrWhiteSpace(errorMsg) ? "Cannot run in background, enable it in Settings - Privacy - Background apps" : errorMsg;
					break;
				default:
					msg = "Background task on";
					isOk = true;
					break;
			}

			return Tuple.Create(isOk, msg);
		}

		private void OnMetaBriefcase_UpdateOneDriveMetaBriefcaseRequested(object sender, EventArgs e)
		{
			var req = _updateOneDriveTrigger?.RequestAsync();
		}
		#endregion services
	}
}