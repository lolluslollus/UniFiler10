using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.DB;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Runtime;
using Utilz;
using Utilz.Data;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.OneDrive.Sdk;
using UniFiler10.Data.Constants;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Threading;

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

		protected override async Task OpenMayOverrideAsync()
		{
			// LOLLO TODO testing the onedrive sdk
			// http://blogs.u2u.net/diederik/post/2015/04/06/Using-the-OneDrive-SDK-in-universal-apps.aspx
			// https://msdn.microsoft.com/en-us/magazine/mt632271.aspx
			// https://onedrive.live.com/?authkey=%21ADtqHIG1cV7g5EI&cid=40CFFDE85F1AB56A&id=40CFFDE85F1AB56A%212187&parId=40CFFDE85F1AB56A%212186&action=locate

			try
			{
				// LOLLO NOTE all one drive operations must run in the UI thread!
				_oneDriveClient = OneDriveClientExtensions.GetClientUsingOnlineIdAuthenticator(_oneDriveScopes);
				_oneDriveAccessToken = await _oneDriveClient.AuthenticateAsync();
				var appRoot = await _oneDriveClient.Drive.Special.AppRoot.Request().GetAsync(); // just for testing or we need it?
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}

			await GetCreateBindersDirectoryAsync(); //.ConfigureAwait(false);
			await LoadAsync(); //.ConfigureAwait(false);

			_metaBriefcase = MetaBriefcase.GetInstance(_oneDriveClient);
			await _metaBriefcase.OpenAsync().ConfigureAwait(false);
			RaisePropertyChanged_UI(nameof(MetaBriefcase)); // notify the UI once the data has been loaded

			_runtimeData = RuntimeData.GetCreateInstance(this);
			await _runtimeData.OpenAsync().ConfigureAwait(false);
			RaisePropertyChanged_UI(nameof(RuntimeData)); // notify the UI once the data has been loaded

			await Licenser.CheckLicensedAsync().ConfigureAwait(false);

			await UpdateCurrentBinder2Async(false).ConfigureAwait(false);
		}
		protected override async Task CloseMayOverrideAsync()
		{
			await SaveAsync(true).ConfigureAwait(false);

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
				//if (_currentBinderName != value)
				//{
				//	_currentBinderName = value;
				//	Task upd = UpdateCurrentBinderAsync().ContinueWith(delegate
				//	{
				//		RaisePropertyChanged_UI();
				//	});
				//}
				//else if (_currentBinder == null)
				//{
				//	Task upd = UpdateCurrentBinderAsync();
				//}
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

		private static readonly SemaphoreSlimSafeRelease _loadSaveSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		private IOneDriveClient _oneDriveClient = null;
		//[IgnoreDataMember]
		//public IOneDriveClient OneDriveClient => _oneDriveClient;
		private AccountSession _oneDriveAccessToken = null;
		private readonly string[] _oneDriveScopes = { "onedrive.readwrite", "onedrive.appfolder", "wl.signin"/*, "wl.offline_access"*/ };
		private const string _oneDriveAppRootUri = "https://api.onedrive.com/v1.0/drive/special/approot/";
		//private string _fileId = string.Empty;
		//private IItemRequestBuilder _builder = null;
		private string _oneDriveFileUrl = null;
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

		public async Task<bool> ExportBinderAsync(string dbName, StorageFolder toRootDirectory)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(dbName) /*|| _dbNames?.Contains(dbName) == false */|| toRootDirectory == null) return false;

				var fromDirectory = await BindersDirectory
					.GetFolderAsync(dbName)
					.AsTask().ConfigureAwait(false);
				if (fromDirectory == null) return false;

				// what if you copy a directory to an existing one? Shouldn't you delete the contents first? No! But then, shouldn't you issue a warning?
				var toDirectoryTest = await toRootDirectory.TryGetItemAsync(dbName).AsTask().ConfigureAwait(false);
				if (toDirectoryTest != null)
				{
					var confirmation = await UserConfirmationPopup.GetInstance().GetUserConfirmationBeforeExportingBinderAsync().ConfigureAwait(false);
					if (confirmation == null || confirmation.Item1 == false || confirmation.Item2 == false) return false;
				}

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
		private const string FILENAME = "LolloSessionDataBriefcase.xml";

		private async Task LoadAsync()
		{
			string errorMessage = string.Empty;
			Briefcase newBriefcase = null;

			try
			{
				await _loadSaveSemaphore.WaitAsync(CancToken); //.ConfigureAwait(false);
				var localFile = await GetDirectory()
					.CreateFileAsync(FILENAME, CreationCollisionOption.OpenIfExists)
					.AsTask(); //.ConfigureAwait(false);

				//String ssss = null; //this is useful when you debug and want to see the file as a string
				//using (IInputStream inStream = await file.OpenSequentialReadAsync())
				//{
				//    using (StreamReader streamReader = new StreamReader(inStream.AsStreamForRead()))
				//    {
				//      ssss = streamReader.ReadToEnd();
				//    }
				//}

				if (CancToken.IsCancellationRequested) return;
				var children = await _oneDriveClient.Drive.Special.AppRoot.Children.Request().GetAsync();
				if (CancToken.IsCancellationRequested) return;
				var oneDriveFile = children.FirstOrDefault(child => child.Name == FILENAME);
				if (CancToken.IsCancellationRequested) return;
				DataContractSerializer serializer = new DataContractSerializer(typeof(Briefcase));
				if (oneDriveFile != null)
				{
					_oneDriveFileUrl = oneDriveFile.Id;

					using (var oneDriveFileStream = await _oneDriveClient.Drive.Special.AppRoot.ItemWithPath(FILENAME).Content.Request().GetAsync())
					{
						oneDriveFileStream.Position = 0;
						newBriefcase = (Briefcase)(serializer.ReadObject(oneDriveFileStream));
						await oneDriveFileStream.FlushAsync().ConfigureAwait(false);
					}
					// sync local from OneDrive
					Task syncLocal = Task.Run(() => newBriefcase?.SaveAsync(false), CancToken);
				}
				else
				{
					var localFileStream = await localFile.OpenStreamForReadAsync().ConfigureAwait(false);
					localFileStream.Position = 0;
					newBriefcase = (Briefcase)(serializer.ReadObject(localFileStream));
					await localFileStream.FlushAsync().ConfigureAwait(false);

					// sync OneDrive from local
					Task syncOneDrive = Task.Run(() => SyncOneDrive(localFileStream), CancToken).ContinueWith(state => localFileStream?.Dispose());
				}
			}
			catch (OperationCanceledException) { }
			catch (FileNotFoundException ex) //ignore file not found, this may be the first run just after installing
			{
				errorMessage = "starting afresh";
				await Logger.AddAsync(errorMessage + ex.ToString(), Logger.FileErrorLogFilename);
			}
			catch (Exception ex)                 //must be tolerant or the app might crash when starting
			{
				errorMessage = "could not restore the data, starting afresh";
				await Logger.AddAsync(errorMessage + ex.ToString(), Logger.FileErrorLogFilename);
			}
			finally
			{
				// if (CancToken.IsCancellationRequested) localFileStream?.Dispose();
				SemaphoreSlimSafeRelease.TryRelease(_loadSaveSemaphore);
			}

			if (string.IsNullOrWhiteSpace(errorMessage))
			{
				if (newBriefcase != null) CopyFrom(newBriefcase);
			}

			await LoadDbNames().ConfigureAwait(false);

			Debug.WriteLine("ended method Briefcase.LoadAsync()");
		}
		private async Task SyncOneDrive(Stream stream)
		{
			try
			{
				stream.Position = 0;

				Task<Item> tsk = null;
				// LOLLO TODO try and do this in a non-UI thread, maybe even in a background task.
				await RunInUiThreadIdleAsync(() =>
				{
					tsk = _oneDriveClient.Drive.Special.AppRoot
					 .ItemWithPath(FILENAME)
					 .Content.Request()
					 .PutAsync<Item>(stream);
				}).ConfigureAwait(false);

				var oneDriveFile = await tsk;

				_oneDriveFileUrl = oneDriveFile?.WebUrl;
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}
			//finally
			//{
			//	stream?.Dispose();
			//}
		}
		private async Task SaveAsync(bool updateOneDrive)
		{
			//for (int i = 0; i < 100000000; i++) //wait a few seconds, for testing
			//{
			//    String aaa = i.ToString();
			//}

			try
			{
				await _loadSaveSemaphore.WaitAsync().ConfigureAwait(false);

				var memoryStream = new MemoryStream();
				var sessionDataSerializer = new DataContractSerializer(typeof(Briefcase));
				sessionDataSerializer.WriteObject(memoryStream, this);

				var file = await GetDirectory()
					.CreateFileAsync(FILENAME, CreationCollisionOption.ReplaceExisting)
					.AsTask().ConfigureAwait(false);
				using (var fileStream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
				{
					memoryStream.Seek(0, SeekOrigin.Begin);
					await memoryStream.CopyToAsync(fileStream).ConfigureAwait(false);
					await memoryStream.FlushAsync().ConfigureAwait(false);
					await fileStream.FlushAsync().ConfigureAwait(false);
				}

				if (updateOneDrive)
				{
					Task syncOneDrive = Task.Run(() => SyncOneDrive(memoryStream)).ContinueWith(state => memoryStream?.Dispose());
				}

				Debug.WriteLine("ended method Briefcase.SaveAsync()");
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_loadSaveSemaphore);
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
