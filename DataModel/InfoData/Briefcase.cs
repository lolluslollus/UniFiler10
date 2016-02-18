﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Runtime;
using Utilz;
using Utilz.Data;
using Windows.Storage;
using UniFiler10.Data.Constants;
using Windows.ApplicationModel.Background;
using Windows.Foundation;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public sealed class Briefcase : OpenableObservableDisposableData
	{
		#region lifecycle
		private static readonly object _instanceLock = new object();
		public static Briefcase GetCreateInstance()
		{
			lock (_instanceLock)
			{
				if (_instance == null || _instance._isDisposed)
				{
					_instance = new Briefcase();
				}
				return _instance;
			}
		}
		public static Briefcase GetCurrentInstance()
		{
			lock (_instanceLock)
			{
				return _instance;
			}
		}
		private Briefcase() { }

		public async Task OpenLightAsync()
		{
			await LoadAsync().ConfigureAwait(false);
			_runtimeData = RuntimeData.GetInstance(this, true);
			await _runtimeData.OpenAsync().ConfigureAwait(false);
		}

		protected override async Task OpenMayOverrideAsync()
		{
			await GetCreateBindersDirectoryAsync(); //.ConfigureAwait(false);
			await LoadAsync(); //.ConfigureAwait(false);
			await LoadDbNames(); //.ConfigureAwait(false);

			_runtimeData = RuntimeData.GetInstance(this, false);
			await _runtimeData.OpenAsync(); //.ConfigureAwait(false);
			RaisePropertyChanged_UI(nameof(RuntimeData)); // notify the UI once the data has been loaded

			_metaBriefcase = MetaBriefcase.GetInstance(_runtimeData);
			await _metaBriefcase.OpenAsync().ConfigureAwait(false);
			RaisePropertyChanged_UI(nameof(MetaBriefcase)); // notify the UI once the data has been loaded

			await TryOpenGetLocBackgroundTaskAsync().ConfigureAwait(false);

			await UpdateCurrentBinder2Async(false).ConfigureAwait(false);
		}
		protected override async Task CloseMayOverrideAsync()
		{
			await SaveAsync(/*true*/).ConfigureAwait(false);
			var bkgUploadToOneDrive = _trigger?.RequestAsync();
			await CloseCurrentBinder2Async().ConfigureAwait(false);

			var rd = _runtimeData;
			if (rd != null)
			{
				await rd.CloseAsync();
				rd.Dispose();
				RuntimeData = null;
			}

			var mb = _metaBriefcase;
			if (mb != null)
			{
				await mb.CloseAsync().ConfigureAwait(false);
				mb.Dispose();
				MetaBriefcase = null;
			}			
		}
		protected override void Dispose(bool isDisposing)
		{
			_dbNames?.Dispose();
			//_dbNames = null;
			base.Dispose(isDisposing);
		}
		#endregion lifecycle


		#region properties
		public const string BINDERS_DIRECTORY_NAME = "Binders";
		private static StorageFolder _bindersDirectory = null;
		[IgnoreDataMember]
		public static StorageFolder BindersDirectory { get { return _bindersDirectory; } }

		private static Briefcase _instance = null;

		private MetaBriefcase _metaBriefcase = null;
		[IgnoreDataMember]
		public MetaBriefcase MetaBriefcase { get { return _metaBriefcase; } private set { if (_metaBriefcase != value) { _metaBriefcase = value; RaisePropertyChanged_UI(); } } }

		private RuntimeData _runtimeData = null;
		[IgnoreDataMember]
		public RuntimeData RuntimeData { get { return _runtimeData; } private set { _runtimeData = value; RaisePropertyChanged_UI(); } }

		private bool _isAllowMeteredConnection = false;
		[DataMember]
		public bool IsAllowMeteredConnection { get { return _isAllowMeteredConnection; } set { if (_isAllowMeteredConnection != value) { _isAllowMeteredConnection = value; RaisePropertyChanged_UI(); } } }

		private volatile string _currentBinderName = string.Empty;
		/// <summary>
		/// This property is only for the serialiser! If you set it, call UpdateCurrentBinderAsync() after.
		/// </summary>
		[DataMember]
		public string CurrentBinderName
		{
			get { return _currentBinderName; }
			private set
			{
				if (_currentBinderName != value) // this property is only for the serialiser! If you set it, call UpdateCurrentBinderAsync() after.
				{
					_currentBinderName = value;
					RaisePropertyChanged_UI();
				}
			}
		}

		private volatile Binder _currentBinder = null;
		[IgnoreDataMember]
		public Binder CurrentBinder { get { return _currentBinder; } }

		private readonly SwitchableObservableDisposableCollection<string> _dbNames = new SwitchableObservableDisposableCollection<string>();
		[IgnoreDataMember]
		public SwitchableObservableDisposableCollection<string> DbNames { get { return _dbNames; } }

		private string _newDbName = string.Empty;
		[DataMember]
		public string NewDbName { get { return _newDbName; } set { if (_newDbName != value) { _newDbName = value; RaisePropertyChanged_UI(); } } }

		//private static readonly SemaphoreSlimSafeRelease _loadSaveSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		//private IOneDriveClient _oneDriveClient = null;
		//private AccountSession _oneDriveAccessToken = null;
		//private readonly string[] _oneDriveScopes = { "onedrive.readwrite", "onedrive.appfolder", "wl.signin"/*, "wl.offline_access"*/ };
		//private const string _oneDriveAppRootUri = "https://api.onedrive.com/v1.0/drive/special/approot/";
		////private string _fileId = string.Empty;
		//private string _oneDriveFileUrl = null;
		#endregion properties


		#region while open methods
		private async Task<bool> UpdateCurrentBinder2Async(bool openTheBinder)
		{
			if (string.IsNullOrEmpty(_currentBinderName))
			{
				await CloseCurrentBinder2Async().ConfigureAwait(false);
				RaisePropertyChanged_UI(nameof(CurrentBinder));
				return false;
			}
			if ((_currentBinder == null && !string.IsNullOrEmpty(_currentBinderName))
				|| (_currentBinder != null && _currentBinder.DBName != _currentBinderName))
			{
				await CloseCurrentBinder2Async().ConfigureAwait(false);

				_currentBinder = Binder.GetCreateInstance(_currentBinderName);
				if (openTheBinder) await _currentBinder.OpenAsync().ConfigureAwait(false);
				RaisePropertyChanged_UI(nameof(CurrentBinder)); // notify the UI once the data has been loaded
				return true;
			}
			if (_currentBinder != null)
			{
				if (openTheBinder) await _currentBinder.OpenAsync().ConfigureAwait(false);
				RaisePropertyChanged_UI(nameof(CurrentBinder));
				return true;
			}
			return false;
		}

		public Task<bool> SetCurrentBinderNameAsync(string dbName)
		{
			return RunFunctionIfOpenAsyncTB(delegate
			{
				CurrentBinderName = dbName;
				return UpdateCurrentBinder2Async(false);
			});
		}

		public Task<bool> OpenCurrentBinderAsync()
		{
			return RunFunctionIfOpenAsyncTB(() => UpdateCurrentBinder2Async(true));
		}

		public Task<bool> OpenBinderAsync(string dbName)
		{
			return RunFunctionIfOpenAsyncTB(delegate
			{
				CurrentBinderName = dbName;
				return UpdateCurrentBinder2Async(true);
			});
		}

		public Task<bool> AddBinderAsync(string dbName)
		{
			return RunFunctionIfOpenAsyncTB(() => AddBinder2Async(dbName));
		}
		private async Task<bool> AddBinder2Async(string dbName)
		{
			if (IsNewDbNameWrong2(dbName))
			{
				return false;
			}
			await RunInUiThreadAsync(delegate
			{
				_dbNames.Add(dbName);
			}).ConfigureAwait(false);
			return true;
		}
		public Task<bool> DeleteBinderAsync(string dbName)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (string.IsNullOrWhiteSpace(dbName) || !_dbNames.Contains(dbName)) return false;

				bool isDbNameRemoved = false;
				await RunInUiThreadAsync(() => isDbNameRemoved = _dbNames.Remove(dbName)).ConfigureAwait(false);
				if (isDbNameRemoved)
				{
					// if deleting the current binder, close it and set another binder as current
					if (_currentBinderName == dbName)
					{
						await _currentBinder.CloseAsync().ConfigureAwait(false);
						CurrentBinderName = _dbNames.Count > 0 ? _dbNames[0] : string.Empty;
						await UpdateCurrentBinder2Async(false);
					}
					return await DeleteBinderFilesAsync(dbName).ConfigureAwait(false);
				}
				return false;
			});
		}
		private static async Task<bool> DeleteBinderFilesAsync(string dbName)
		{
			try
			{
				var binderDirectory = await BindersDirectory
					.GetFolderAsync(dbName)
					.AsTask().ConfigureAwait(false);
				if (binderDirectory != null) await binderDirectory.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
				return true;
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return false;
		}

		public async Task<bool> ExportBinderAsync(string dbName, StorageFolder fromDirectory, StorageFolder toRootDirectory)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(dbName) /*|| _dbNames?.Contains(dbName) == false */ || fromDirectory == null || toRootDirectory == null) return false;

				//var fromDirectory = await BindersDirectory
				//	.GetFolderAsync(dbName)
				//	.AsTask().ConfigureAwait(false);
				//if (fromDirectory == null) return false;

				//// what if you copy a directory to an existing one? Shouldn't you delete the contents first? No! But then, shouldn't you issue a warning?
				//var toDirectoryTest = await toRootDirectory.TryGetItemAsync(dbName).AsTask().ConfigureAwait(false);
				//if (toDirectoryTest != null)
				//{
				//	var confirmation = await UserConfirmationPopup.GetInstance().GetUserConfirmationBeforeExportingBinderAsync().ConfigureAwait(false);
				//	if (confirmation == null || confirmation.Item1 == false || confirmation.Item2 == false) return false;
				//}

				var toDirectory = await toRootDirectory
					.CreateFolderAsync(dbName, CreationCollisionOption.ReplaceExisting)
					.AsTask().ConfigureAwait(false);

				if (toDirectory == null) return false;

				Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", toDirectory);
				await fromDirectory.CopyDirContentsAsync(toDirectory, CancToken).ConfigureAwait(false);
				return true;
			}
			catch (OperationCanceledException) { }
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}
			return false;
		}

		public Task<bool> ImportBinderAsync(StorageFolder fromStorageFolder)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (fromStorageFolder == null) return false;
				bool isOk = false;

				// close the current binder if it is the one to be restored
				bool wasOpen = false;
				if (_currentBinderName == fromStorageFolder.Name)
				{
					wasOpen = await CloseCurrentBinder2Async().ConfigureAwait(false);
				}

				if (await ImportBinderFilesAsync(fromStorageFolder).ConfigureAwait(false))
				{
					if (!_dbNames.Contains(fromStorageFolder.Name))
					{
						await RunInUiThreadAsync(() => _dbNames.Add(fromStorageFolder.Name)).ConfigureAwait(false);
					}
					isOk = true;
				}
				// update the current binder and open it if it was open before
				if (wasOpen) await UpdateCurrentBinder2Async(true).ConfigureAwait(false);

				return isOk;
			});
		}
		public Task<bool> MergeBinderAsync(StorageFolder fromDirectory)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (string.IsNullOrWhiteSpace(fromDirectory?.Name) || !_dbNames.Contains(fromDirectory.Name)) return false;

				// close the current binder if it is NOT the one to be merged into, and open the binder to be merged into
				CurrentBinderName = fromDirectory.Name;
				await UpdateCurrentBinder2Async(true).ConfigureAwait(false);

				bool isOk = await _currentBinder.ImportFoldersAsync(fromDirectory).ConfigureAwait(false);

				return isOk;
			});
		}
		private async Task<bool> ImportBinderFilesAsync(StorageFolder fromDirectory)
		{
			if (fromDirectory != null)
			{
				try
				{
					// Check if you are restoring a Binder or something completely unrelated, which may cause trouble.
					// Make sure you restore a Binder and not just any directory!
					var srcFiles = await fromDirectory.GetFilesAsync().AsTask().ConfigureAwait(false);
					bool isSrcOk = srcFiles.Any(file => file.Name == DBManager.DB_FILE_NAME)
						&& srcFiles.Any(file => file.Name == Binder.FILENAME);
					if (!isSrcOk) return false;

					var toDirectory = await BindersDirectory
						.CreateFolderAsync(fromDirectory.Name, CreationCollisionOption.ReplaceExisting)
						.AsTask().ConfigureAwait(false);
					await fromDirectory.CopyDirContentsAsync(toDirectory, CancToken).ConfigureAwait(false);
					return true;
				}
				catch (OperationCanceledException) { }
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
			}
			return false;
		}

		public Task<bool> CloseCurrentBinderAsync()
		{
			return RunFunctionIfOpenAsyncTB(CloseCurrentBinder2Async);
		}
		private async Task<bool> CloseCurrentBinder2Async()
		{
			var cb = _currentBinder;
			if (cb != null)
			{
				bool wasOpen = await cb.CloseAsync().ConfigureAwait(false);
				cb.Dispose();
				_currentBinder = null; // don't use CurrentBinder here, it triggers stuff
				return wasOpen;
			}
			return false;
		}


		public Task<BoolWhenOpen> IsDbNameAvailableAsync(string dbName)
		{
			return RunFunctionIfOpenThreeStateAsyncB(() => _dbNames.Contains(dbName));
		}
		public Task<bool> IsNewDbNameWrongAsync(string newDbName)
		{
			return RunFunctionIfOpenAsyncB(() => IsNewDbNameWrong2(newDbName));
		}
		private bool IsNewDbNameWrong2(string newDbName)
		{
			if (string.IsNullOrWhiteSpace(newDbName)) return true;
			return _dbNames.Contains(newDbName);
		}
		public Task<bool> ExportSettingsAsync(StorageFile toFile)
		{
			return RunFunctionIfOpenAsyncTB(delegate
			{
				var mbc = _metaBriefcase; // LOLLO TODO test this: it was GetCreateInstance
				return mbc.SaveACopyAsync(toFile);
			});
		}
		public Task<bool> ImportSettingsAsync(StorageFile fromFile)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (fromFile == null) return false;

				bool wasOpen = await CloseCurrentBinder2Async().ConfigureAwait(false);

				await _metaBriefcase.CloseAsync().ConfigureAwait(false);
				// do not replace the instance or you may screw the binding. Close, change and reopen will do.
				_metaBriefcase.SetSourceFileJustOnce(fromFile);

				await _metaBriefcase.OpenAsync().ConfigureAwait(false);
				RaisePropertyChanged_UI(nameof(MetaBriefcase)); // notify the UI once the data has been loaded

				// update the current binder, whichever it is, and open it if it was open before
				await UpdateCurrentBinder2Async(wasOpen).ConfigureAwait(false);
				return true;
			});
		}
		#endregion while open methods


		#region loading methods

		private IBackgroundTaskRegistration _oduBkgTaskReg = null;
		private ApplicationTrigger _trigger = null;
		private static IBackgroundTaskRegistration GetTaskIfAlreadyRegistered()
		{
			//return (from cur in BackgroundTaskRegistration.AllTasks
			//		where cur.Value.Name == ConstantData.GET_LOCATION_BACKGROUND_TASK_NAME
			//		select cur.Value).FirstOrDefault();
			foreach (var cur in BackgroundTaskRegistration.AllTasks)
			{
				if (cur.Value.Name == ConstantData.GET_ODU_BACKGROUND_TASK_NAME)
				{
					return cur.Value;
				}
			}
			return null;
		}

		//private void CloseGetLocBackgroundTask_All()
		//{
		//	if (_oduBkgTaskReg != null)
		//	{
		//		_oduBkgTaskReg.Unregister(true);
		//		_oduBkgTaskReg = null;
		//	}

		//	var allBkgTasks = BackgroundTaskRegistration.AllTasks.Values.ToList(); // clone
		//	foreach (var item in allBkgTasks)
		//	{
		//		if (item.Name == ConstantData.GET_ODU_BACKGROUND_TASK_NAME)
		//		{
		//			item.Unregister(true);
		//		}
		//	}
		//}
		private async Task<Tuple<bool, string>> TryOpenGetLocBackgroundTaskAsync()
		{
			bool isOk = false;
			string msg = string.Empty;

			string errorMsg = string.Empty;
			BackgroundAccessStatus backgroundAccessStatus = BackgroundAccessStatus.Unspecified;

			_oduBkgTaskReg = GetTaskIfAlreadyRegistered();

			if (_oduBkgTaskReg == null) // bkg task not registered yet: register it
			{
				try
				{
					//maniman
					//CloseGetLocBackgroundTask_All();

					// Get permission for a background task from the user. If the user has already answered once,
					// this does nothing and the user must manually update their preference via PC Settings.
					backgroundAccessStatus = await BackgroundExecutionManager.RequestAccessAsync().AsTask().ConfigureAwait(false);

					// Regardless of the answer, register the background task. If the user later adds this application
					// to the lock screen, the background task will be ready to run.
					// Create a new background task builder
					BackgroundTaskBuilder bkgTaskBuilder = new BackgroundTaskBuilder()
					{
						Name = ConstantData.GET_ODU_BACKGROUND_TASK_NAME,
						TaskEntryPoint = ConstantData.GET_ODU_BACKGROUND_TASK_ENTRY_POINT
					};

					//SystemCondition condition = new SystemCondition(SystemConditionType.UserPresent);
					//var trigger = new SystemTrigger(SystemTriggerType.UserAway, false);
					_trigger = new ApplicationTrigger();
					bkgTaskBuilder.SetTrigger(_trigger); // LOLLO TODO check this

					// Register the background task
					_oduBkgTaskReg = bkgTaskBuilder.Register();
				}
				catch (Exception ex)
				{
					errorMsg = ex.ToString();
					backgroundAccessStatus = BackgroundAccessStatus.Denied;
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
			}
			else // bkg task registered: see if it is ok
			{
				try
				{
					backgroundAccessStatus = BackgroundExecutionManager.GetAccessStatus();
				}
				catch (Exception ex)
				{
					errorMsg = ex.ToString();
					backgroundAccessStatus = BackgroundAccessStatus.Denied;
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
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
		private const string FILENAME = "LolloSessionDataBriefcase.xml";

		private async Task LoadAsync()
		{
			string errorMessage = string.Empty;
			Briefcase newBriefcase = null;

			try
			{
				if (CancToken.IsCancellationRequested) return;

				newBriefcase = await RegistryAccess.GetObject<Briefcase>(ConstantData.REG_BRIEFCASE).ConfigureAwait(false);

				//var localFile = await GetDirectory()
				//	.CreateFileAsync(FILENAME, CreationCollisionOption.OpenIfExists)
				//	.AsTask().ConfigureAwait(false);

				//if (CancToken.IsCancellationRequested) return;

				//var localFileStream = await localFile.OpenStreamForReadAsync().ConfigureAwait(false);
				//localFileStream.Position = 0;
				//newBriefcase = (Briefcase)(new DataContractSerializer(typeof(Briefcase)).ReadObject(localFileStream));
				//await localFileStream.FlushAsync().ConfigureAwait(false);
			}
			catch (OperationCanceledException) { }
			//catch (FileNotFoundException ex) //ignore file not found, this may be the first run just after installing
			//{
			//	errorMessage = "starting afresh";
			//	await Logger.AddAsync(errorMessage + ex.ToString(), Logger.FileErrorLogFilename);
			//}
			catch (Exception ex)                 //must be tolerant or the app might crash when starting
			{
				errorMessage = "could not restore the data, starting afresh";
				await Logger.AddAsync(errorMessage + ex.ToString(), Logger.FileErrorLogFilename);
			}

			if (string.IsNullOrWhiteSpace(errorMessage))
			{
				CopyFrom(newBriefcase);
			}

			Debug.WriteLine("ended method Briefcase.LoadAsync()");
		}

		private async Task SaveAsync(/*bool updateOneDrive*/)
		{
			//for (int i = 0; i < 100000000; i++) //wait a few seconds, for testing
			//{
			//    String aaa = i.ToString();
			//}

			try
			{
				await RegistryAccess.TrySetObject(ConstantData.REG_BRIEFCASE, this).ConfigureAwait(false);

				//var memoryStream = new MemoryStream();
				//var sessionDataSerializer = new DataContractSerializer(typeof(Briefcase));
				//sessionDataSerializer.WriteObject(memoryStream, this);

				//var file = await GetDirectory()
				//	.CreateFileAsync(FILENAME, CreationCollisionOption.ReplaceExisting)
				//	.AsTask().ConfigureAwait(false);
				//using (var fileStream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
				//{
				//	memoryStream.Seek(0, SeekOrigin.Begin);
				//	await memoryStream.CopyToAsync(fileStream).ConfigureAwait(false);
				//	await memoryStream.FlushAsync().ConfigureAwait(false);
				//	await fileStream.FlushAsync().ConfigureAwait(false);
				//}
				Debug.WriteLine("ended method Briefcase.SaveAsync()");
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}
		}
		private async Task LoadDbNames()
		{
			var directories = await BindersDirectory.GetFoldersAsync().AsTask().ConfigureAwait(false);
			await RunInUiThreadAsync(delegate
			{
				_dbNames.Clear();
				foreach (var dir in directories)
				{
					_dbNames.Add(dir.Name);
				}
			}).ConfigureAwait(false);
		}
		private bool CopyFrom(Briefcase source)
		{
			if (source == null) return false;

			IsAllowMeteredConnection = source._isAllowMeteredConnection;
			NewDbName = source._newDbName;
			CurrentBinderName = source._currentBinderName; // CurrentBinder is set later
			return true;
		}

		private static StorageFolder GetDirectory()
		{
			return ApplicationData.Current.LocalFolder; //.RoamingFolder;
		}

		private static async Task GetCreateBindersDirectoryAsync()
		{
			if (_bindersDirectory == null)
			{
				_bindersDirectory = await ApplicationData.Current.LocalFolder
					.CreateFolderAsync(BINDERS_DIRECTORY_NAME, CreationCollisionOption.OpenIfExists)
					.AsTask().ConfigureAwait(false);
			}
		}
		#endregion loading methods
	}
}