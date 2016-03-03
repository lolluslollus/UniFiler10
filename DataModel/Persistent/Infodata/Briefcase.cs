using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Windows.Media.Capture;
using UniFiler10.Data.DB;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Runtime;
using Utilz;
using Utilz.Data;
using Windows.Storage;
using UniFiler10.Data.Constants;
// LOLLO TODO somewhere in the app, something is bound to IsEnabled and dumps after resuming, when the last visited page was settings
namespace UniFiler10.Data.Model
{
	[DataContract]
	public sealed class Briefcase : OpenableObservableData
	{
		#region lifecycle
		private readonly bool _isLight = false;
		private static readonly object _instanceLock = new object();
		public static Briefcase GetCreateInstance(bool isLight = false)
		{
			lock (_instanceLock)
			{
				if (_instance == null /*|| _instance._isDisposed*/ || _instance._isLight != isLight)
				{
					_instance = new Briefcase(isLight);
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

		private Briefcase(bool isLight = false)
		{
			_isLight = isLight;
		}

		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			if (!_isLight) await GetCreateBindersDirectoryAsync().ConfigureAwait(false);
			await LoadAsync().ConfigureAwait(false);
			if (!_isLight) await LoadDbNames().ConfigureAwait(false);

			_runtimeData = RuntimeData.GetInstance(this, _isLight);
			await _runtimeData.OpenAsync().ConfigureAwait(false);
			if (!_isLight) RaisePropertyChanged_UI(nameof(RuntimeData)); // notify the UI once the data has been loaded

			_metaBriefcase = MetaBriefcase.GetInstance(_runtimeData, this);
			if (_isLight) return;
			await _metaBriefcase.OpenAsync().ConfigureAwait(false);
			RaisePropertyChanged_UI(nameof(MetaBriefcase)); // notify the UI once the data has been loaded

			_isWantAndCanUseOneDrive = IsWantToUseOneDrive && _metaBriefcase.IsLocalSyncedOnceSinceLastOpen && _runtimeData.IsConnectionAvailable;
			RaisePropertyChanged_UI(nameof(IsWantAndCanUseOneDrive));
			_isWantAndCannotUseOneDrive = _isWantToUseOneDrive && !_isWantAndCanUseOneDrive;
			RaisePropertyChanged_UI(nameof(IsWantAndCannotUseOneDrive));

			_runtimeData.PropertyChanged += OnRuntimeData_PropertyChanged;

			await UpdateCurrentBinder2Async(false).ConfigureAwait(false);
		}

		private void OnRuntimeData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(RuntimeData.IsConnectionAvailable))
			{
				Task upd = RunFunctionIfOpenAsyncT(() => { return UpdateIsWantAndCanUseOneDriveAsync(); });
			}
		}

		protected override async Task CloseMayOverrideAsync()
		{
			await SaveAsync().ConfigureAwait(false);
			await CloseCurrentBinder2Async().ConfigureAwait(false);

			var rd = _runtimeData;
			if (rd != null)
			{
				rd.PropertyChanged -= OnRuntimeData_PropertyChanged;
				await rd.CloseAsync().ConfigureAwait(false);
				//rd.Dispose();
			}

			var mb = _metaBriefcase;
			if (mb != null)
			{
				await mb.CloseAsync().ConfigureAwait(false);
				//mb.Dispose();
			}
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

		private volatile bool _isWantToUseOneDrive = true;
		[DataMember]
		public bool IsWantToUseOneDrive
		{
			get { return _isWantToUseOneDrive; }
			private set { if (_isWantToUseOneDrive != value) { if (_isWantToUseOneDrive != value) { _isWantToUseOneDrive = value; RaisePropertyChanged_UI(); } } }
		}

		private volatile bool _isWantAndCanUseOneDrive = true;
		[IgnoreDataMember]
		public bool IsWantAndCanUseOneDrive
		{
			get { return _isWantAndCanUseOneDrive; }
		}
		private volatile bool _isWantAndCannotUseOneDrive = false;
		[IgnoreDataMember]
		public bool IsWantAndCannotUseOneDrive
		{
			get { return _isWantAndCannotUseOneDrive; }
		}

		public Task SetIsWantToUseOneDriveAsync(bool newValue, bool isLoadFromOneDrive) // = MetaBriefcase.OpenParameters.DefaultIsLoadFromOneDrive)
		{
			return RunFunctionIfOpenAsyncT(async () =>
			{
				IsWantToUseOneDrive = newValue;
				//RaisePropertyChanged_UI(nameof(IsWantToUseOneDrive));
				await UpdateIsWantAndCanUseOneDriveAsync(isLoadFromOneDrive).ConfigureAwait(false);
			});
		}
		private async Task UpdateIsWantAndCanUseOneDriveAsync(bool isLoadFromOneDrive = MetaBriefcase.OpenParameters.DefaultIsLoadFromOneDrive)
		{
			if (_isWantToUseOneDrive && _isWantAndCanUseOneDrive) return;

			if (_isWantToUseOneDrive)
			{
				if (_runtimeData.IsConnectionAvailable)
				{
					bool wasOpen = await CloseCurrentBinder2Async().ConfigureAwait(false);
					await _metaBriefcase.CloseAsync().ConfigureAwait(false);
					//_metaBriefcase.SetReloadJustOnce();
					await _metaBriefcase.OpenAsync(new MetaBriefcase.OpenParameters(null, true, isLoadFromOneDrive)).ConfigureAwait(false);
					RaisePropertyChanged_UI(nameof(MetaBriefcase)); // notify the UI once the data has been loaded
																	// update the current binder, whichever it is, and open it if it was open before
					await UpdateCurrentBinder2Async(wasOpen).ConfigureAwait(false);

					_isWantAndCanUseOneDrive = _metaBriefcase.IsLocalSyncedOnceSinceLastOpen;
				}
				else
				{
					_isWantAndCanUseOneDrive = false;
				}
			}
			else
			{
				_isWantAndCanUseOneDrive = false;
			}
			RaisePropertyChanged_UI(nameof(IsWantAndCanUseOneDrive));

			_isWantAndCannotUseOneDrive = _isWantToUseOneDrive && !_isWantAndCanUseOneDrive;
			RaisePropertyChanged_UI(nameof(IsWantAndCannotUseOneDrive));
		}
		private volatile string _currentBinderName = string.Empty;
		/// <summary>
		/// This property's setter is only for the serialiser! If you set it, call UpdateCurrentBinderAsync() after.
		/// </summary>
		[DataMember]
		public string CurrentBinderName
		{
			get { return _currentBinderName; }
			private set
			{
				if (_currentBinderName != value)
				{
					_currentBinderName = value;
					RaisePropertyChanged_UI();
				}
			}
		}

		private volatile Binder _currentBinder = null;
		[IgnoreDataMember]
		public Binder CurrentBinder { get { return _currentBinder; } }

		private readonly SwitchableObservableCollection<string> _dbNames = new SwitchableObservableCollection<string>();
		[IgnoreDataMember]
		public SwitchableObservableCollection<string> DbNames { get { return _dbNames; } }

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
		private CameraCaptureUIMaxPhotoResolution _cameraCaptureResolution = CameraCaptureUIMaxPhotoResolution.HighestAvailable;
		[DataMember]
		public CameraCaptureUIMaxPhotoResolution CameraCaptureResolution
		{
			get { return _cameraCaptureResolution; }
			set { _cameraCaptureResolution = value; RaisePropertyChanged_UI(); }
		}
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
					.TryGetItemAsync(dbName)
					.AsTask().ConfigureAwait(false) as StorageFolder;
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
				return _metaBriefcase?.SaveACopyAsync(toFile);
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
				//_metaBriefcase.SetSourceFileJustOnce(fromFile);
				await _metaBriefcase.OpenAsync(new MetaBriefcase.OpenParameters(fromFile, true, false)).ConfigureAwait(false);
				RaisePropertyChanged_UI(nameof(MetaBriefcase)); // notify the UI once the data has been loaded

				// update the current binder, whichever it is, and open it if it was open before
				await UpdateCurrentBinder2Async(wasOpen).ConfigureAwait(false);
				return true;
			});
		}
		#endregion while open methods


		#region loading methods
		private const string FILENAME = "LolloSessionDataBriefcase.xml";

		private async Task LoadAsync()
		{
			Briefcase newBriefcase = null;

			try
			{
				if (CancToken.IsCancellationRequested) return;
				newBriefcase = await RegistryAccess.GetObject<Briefcase>(ConstantData.REG_BRIEFCASE).ConfigureAwait(false);
			}
			catch (OperationCanceledException) { }
			catch (Exception ex) //must be tolerant or the app might crash when starting
			{
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename);
			}

			if (newBriefcase != null)
			{
				CopyFrom(newBriefcase);
			}

			Debug.WriteLine("ended method Briefcase.LoadAsync()");
		}

		private async Task SaveAsync()
		{
			//for (int i = 0; i < 100000000; i++) //wait a few seconds, for testing
			//{
			//    String aaa = i.ToString();
			//}

			try
			{
				await RegistryAccess.TrySetObject(ConstantData.REG_BRIEFCASE, this).ConfigureAwait(false);
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
			IsWantToUseOneDrive = source._isWantToUseOneDrive;
			NewDbName = source._newDbName;
			CurrentBinderName = source._currentBinderName; // CurrentBinder is set later
			CameraCaptureResolution = source._cameraCaptureResolution;
			return true;
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