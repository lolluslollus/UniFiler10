using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using UniFiler10.Data.Runtime;
using Utilz;
using Utilz.Data;
using Windows.Storage;
using Windows.Storage.Streams;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public class Binder : DbBoundObservableData
	{
		#region lifecycle
		private static readonly object _instanceLock = new object();
		internal static Binder GetCreateInstance(string dbName)
		{
			lock (_instanceLock)
			{
				if (_instance == null || _instance._isDisposed)
				{
					_instance = new Binder(dbName);
				}
				return _instance;
			}
		}
		protected Binder(string dbName)
		{
			if (dbName == null || string.IsNullOrWhiteSpace(dbName)) throw new ArgumentException("Binder ctor: dbName cannot be null or empty");
			SetDBName(dbName);
		}
		protected override void Dispose(bool isDisposing)
		{
			_folders?.Dispose();
			_folders = null;
			base.Dispose(isDisposing);
		}

		protected override async Task OpenMayOverrideAsync()
		{
			await GetCreateDirectoryAsync().ConfigureAwait(false);

			_dbManager = new DBManager(_directory, false);
			await _dbManager.OpenAsync().ConfigureAwait(false);

			await LoadNonDbPropertiesAsync().ConfigureAwait(false);
			await LoadFoldersWithoutContentAsync().ConfigureAwait(false);

			await UpdateCurrentFolder2Async(false).ConfigureAwait(false);
		}
		protected override async Task CloseMayOverrideAsync()
		{
			var dbM = _dbManager;
			if (dbM != null)
			{
				await dbM.CloseAsync().ConfigureAwait(false);
				dbM.Dispose();
			}
			_dbManager = null;

			Task closeFolders = new Task(delegate
			{
				Parallel.ForEach(_folders, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, (folder) =>
				   {
					   // await folder.CloseAsync().ConfigureAwait(false); // LOLLO NOTE avoid async calls within a Parallel.ForEach coz they are not awaited
					   folder?.Dispose();
				   });
			});
			Task save = SaveNonDbPropertiesAsync();
			closeFolders.Start();

			await Task.WhenAll(closeFolders, save).ConfigureAwait(false);

			await RunInUiThreadAsync(delegate
			{
				_folders.Clear();
				_currentFolder = null; // don't set CurrentFolder, it triggers stuff
			}).ConfigureAwait(false);
		}
		#endregion lifecycle


		#region main properties
		private static Binder _instance = null;

		protected StorageFolder _directory = null;
		[IgnoreDataMember]
		public StorageFolder Directory { get { return _directory; } }


		protected DBManager _dbManager = null;
		[IgnoreDataMember]
		internal DBManager DbManager { get { return _dbManager; } }

		private readonly object _dbNameLocker = new object();
		private string _dbName = string.Empty;
		[DataMember]
		public string DBName
		{
			get
			{
				lock (_dbNameLocker)
				{
					return _dbName;
				}
			}
			private set // this lockless set teris only for serialisation
			{
				if (_dbName != value) { _dbName = value; RaisePropertyChanged_UI(); }
			}
		}
		private void SetDBName(string dbName)
		{
			lock (_dbNameLocker)
			{
				DBName = dbName;
			}
		}
		protected SwitchableObservableDisposableCollection<Folder> _folders = new SwitchableObservableDisposableCollection<Folder>();
		[IgnoreDataMember]
		public SwitchableObservableDisposableCollection<Folder> Folders { get { return _folders; } protected set { if (_folders != value) { _folders = value; RaisePropertyChanged(); } } }

		private volatile string _currentFolderId = DEFAULT_ID;
		[DataMember]
		public string CurrentFolderId
		{
			get { return _currentFolderId; }
			private set
			{
				if (_currentFolderId != value) // this property is only for the serialiser! If you set it, call UpdateCurrentFolderAsync() after.
				{
					_currentFolderId = value;
					RaisePropertyChanged_UI();
				}
			}
		}

		private volatile Folder _currentFolder = null;
		[IgnoreDataMember]
		public Folder CurrentFolder
		{
			get { return _currentFolder; }
			private set { if (_currentFolder != value) { _currentFolder = value; RaisePropertyChanged_UI(); } }
		}

		[DataMember]
		public override string ParentId { get { return DEFAULT_ID; } set { SetPropertyUpdatingDb(ref _parentId, DEFAULT_ID); } }
		#endregion main properties


		#region filter properties
		public enum Filters { All, Recent, Cat, Field };
		private const int HOW_MANY_FOLDERS_IN_RECENT_VIEW = 10;

		public class FolderPreview : ObservableData
		{
			protected string _folderId = string.Empty;
			public string FolderId { get { return _folderId; } set { if (_folderId != value) { _folderId = value; RaisePropertyChanged_UI(); } } }

			private string _folderName = string.Empty;
			public string FolderName { get { return _folderName; } set { if (_folderName != value) { _folderName = value; RaisePropertyChanged_UI(); } } }

			private string _documentUri0 = string.Empty;
			public string DocumentUri0 { get { return _documentUri0; } set { if (_documentUri0 != value) { _documentUri0 = value; RaisePropertyChanged_UI(); } } }

			private Document _document = null;
			public Document Document { get { return _document; } set { _document = value; RaisePropertyChanged_UI(); } }
		}

		private string _catIdForCatFilter = DEFAULT_ID;
		[DataMember]
		public string CatIdForCatFilter
		{
			get
			{
				return GetProperty(ref _catIdForCatFilter, _filterLocker);
				//lock (_filterLocker)
				//{
				//	return _catIdForCatFilter;
				//}
			}
			private set // this lockless set teris only for serialisation
			{
				_catIdForCatFilter = value ?? DEFAULT_ID;
				//string newValue = value ?? DEFAULT_ID;
				//if (_catIdForCatFilter != newValue) { _catIdForCatFilter = newValue; }
			}
		}
		public void SetIdsForCatFilter(string catId)
		{
			SetProperty(ref _catIdForCatFilter, catId, _filterLocker);
			//lock (_filterLocker)
			//{
			//	CatIdForCatFilter = catId;
			//};
		}

		//public Task SetIdsForCatFilterAsync(string catId)
		//{
		//	return RunFunctionIfOpenAsyncA(delegate // only when it's open, to avoid surprises from the binding when objects are closed and reset
		//	{
		//		CatIdForCatFilter = catId;
		//	});
		//}

		private string _catIdForFldFilter = DEFAULT_ID;
		[DataMember]
		public string CatIdForFldFilter
		{
			get
			{
				return GetProperty(ref _catIdForFldFilter, _filterLocker);
				//lock (_filterLocker)
				//{
				//	return _catIdForFldFilter;
				//}
			}
			private set // this lockless set teris only for serialisation
			{
				_catIdForFldFilter = value ?? DEFAULT_ID;
				//string newValue = value ?? DEFAULT_ID;
				//if (_catIdForFldFilter != newValue) { _catIdForFldFilter = newValue; }
			}
		}

		private string _fldDscIdForFldFilter = DEFAULT_ID;
		[DataMember]
		public string FldDscIdForFldFilter
		{
			get
			{
				return GetProperty(ref _fldDscIdForFldFilter, _filterLocker);
				//lock (_filterLocker)
				//{
				//	return _fldDscIdForFldFilter;
				//}
			}
			private set // this lockless set teris only for serialisation
			{
				_fldDscIdForFldFilter = value ?? DEFAULT_ID;
				//string newValue = value ?? DEFAULT_ID;
				//if (_fldDscIdForFldFilter != newValue) { _fldDscIdForFldFilter = newValue; }
			}
		}

		private string _fldValIdForFldFilter = DEFAULT_ID;
		[DataMember]
		public string FldValIdForFldFilter
		{
			get
			{
				return GetProperty(ref _fldValIdForFldFilter, _filterLocker);
				//lock (_filterLocker)
				//{
				//	return _fldValIdForFldFilter;
				//}
			}
			private set // this lockless set teris only for serialisation
			{
				_fldValIdForFldFilter = value ?? DEFAULT_ID;
				//string newValue = value ?? DEFAULT_ID;
				//if (_fldValIdForFldFilter != newValue) { _fldValIdForFldFilter = newValue; }
			}
		}
		public void SetIdsForFldFilter(string catId, string fldDscId, string fldValId)
		{
			lock (_filterLocker)
			{
				CatIdForFldFilter = catId;
				FldDscIdForFldFilter = fldDscId;
				FldValIdForFldFilter = fldValId;
			};
		}
		//public Task SetIdsForFldFilterAsync(string catId, string fldDscId, string fldValId)
		//{
		//	return RunFunctionIfOpenAsyncA(delegate // only when it's open, to avoid surprises from the binding when objects are closed and reset
		//	{
		//		CatIdForFldFilter = catId;
		//		FldDscIdForFldFilter = fldDscId;
		//		FldValIdForFldFilter = fldValId;
		//	});
		//}

		private readonly object _filterLocker = new object();
		private Filters _whichFilter = Filters.All;
		[DataMember]
		public Filters WhichFilter
		{
			get
			{
				return GetProperty(ref _whichFilter, _filterLocker);
				//lock (_filterLocker)
				//{
				//	return _whichFilter;
				//}
			}
			private set // this lockless set teris only for serialisation
			{
				_whichFilter = value;
				//if (_whichFilter != value)
				//{
				//	_whichFilter = value; RaisePropertyChanged_UI();
				//}
			}
		}
		public void SetFilter(Filters whichFilter)
		{
			SetProperty(ref _whichFilter, whichFilter, _filterLocker);
			//lock (_filterLocker)
			//{
			//	WhichFilter = whichFilter;
			//}
		}
		#endregion filter properties


		#region loading methods
		internal const string FILENAME = "LolloSessionDataBinder.xml";

		protected async Task LoadNonDbPropertiesAsync()
		{
			string errorMessage = string.Empty;
			Binder newBinder = null;

			try
			{
				var file = await Directory
					.CreateFileAsync(FILENAME, CreationCollisionOption.OpenIfExists)
					.AsTask().ConfigureAwait(false);

				//String ssss = null; //this is useful when you debug and want to see the file as a string
				//using (IInputStream inStream = await file.OpenSequentialReadAsync())
				//{
				//    using (StreamReader streamReader = new StreamReader(inStream.AsStreamForRead()))
				//    {
				//      ssss = streamReader.ReadToEnd();
				//    }
				//}

				using (IInputStream inStream = await file.OpenSequentialReadAsync().AsTask().ConfigureAwait(false))
				{
					using (var iinStream = inStream.AsStreamForRead())
					{
						DataContractSerializer serializer = new DataContractSerializer(typeof(Binder));
						iinStream.Position = 0;
						newBinder = (Binder)(serializer.ReadObject(iinStream));
						await iinStream.FlushAsync().ConfigureAwait(false);
					}
				}
			}
			catch (FileNotFoundException ex) //ignore file not found, this may be the first run just after installing
			{
				errorMessage = "starting afresh";
				await Logger.AddAsync(errorMessage + ex.ToString(), Logger.FileErrorLogFilename);
			}
			catch (Exception ex)                 //must be tolerant or the app might crash when starting
			{
				// Debugger.Break();
				errorMessage = "could not restore the data, starting afresh";
				await Logger.AddAsync(errorMessage + ex.ToString(), Logger.FileErrorLogFilename);
			}
			if (string.IsNullOrWhiteSpace(errorMessage))
			{
				if (newBinder != null) CopyFrom(newBinder);
			}

			Debug.WriteLine("ended method Binder.LoadAsync()");
		}
		private async Task SaveNonDbPropertiesAsync()
		{
			//for (int i = 0; i < 100000000; i++) //wait a few seconds, for testing
			//{
			//    String aaa = i.ToString();
			//}

			try
			{
				using (MemoryStream memoryStream = new MemoryStream())
				{
					var file = await Directory
						.CreateFileAsync(FILENAME, CreationCollisionOption.ReplaceExisting)
						.AsTask().ConfigureAwait(false);

					DataContractSerializer sessionDataSerializer = new DataContractSerializer(typeof(Binder));
					sessionDataSerializer.WriteObject(memoryStream, this);

					using (Stream fileStream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
					{
						memoryStream.Seek(0, SeekOrigin.Begin);
						await memoryStream.CopyToAsync(fileStream).ConfigureAwait(false);
						await memoryStream.FlushAsync().ConfigureAwait(false);
						await fileStream.FlushAsync().ConfigureAwait(false);
					}
				}
				Debug.WriteLine("ended method Binder.SaveAsync()");
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
			}
		}
		private async Task GetCreateDirectoryAsync()
		{
			_directory = await Briefcase.BindersDirectory
				.CreateFolderAsync(DBName, CreationCollisionOption.OpenIfExists)
				.AsTask().ConfigureAwait(false);
		}
		private void CopyFrom(Binder source)
		{
			SetIdsForCatFilter(source._catIdForFldFilter);
			// CatIdForFldFilter = source._catIdForFldFilter;
			SetIdsForFldFilter(source._catIdForCatFilter, source._fldDscIdForFldFilter, source._fldValIdForFldFilter);
			//FldDscIdForFldFilter = source._fldDscIdForFldFilter;
			//FldValIdForFldFilter = source._fldValIdForFldFilter;
			//CatIdForCatFilter = source._catIdForCatFilter;
			SetFilter(source._whichFilter);
			// WhichFilter = source._whichFilter;
			SetDBName(source._dbName);
			//DBName = source._dbName;
			CurrentFolderId = source._currentFolderId; // CurrentFolder will be updated later
		}
		protected async Task LoadFoldersWithoutContentAsync()
		{
			var folders = await _dbManager.GetFoldersAsync();

			await RunInUiThreadAsync(delegate
			{
				_folders.ReplaceAll(folders);
			}).ConfigureAwait(false);
		}

		#endregion loading methods


		#region while open methods
		private async Task UpdateCurrentFolder2Async(bool openTheFolder)
		{
			if (_folders != null)
			{
				if (_currentFolderId != null)
				{
					// do not close the folder, just disable it. It keeps more memory busy but it's faster.
					_currentFolder = _folders.FirstOrDefault(fo => fo.Id == _currentFolderId);
				}
				else
				{
					_currentFolder = null;
				}
				if (_currentFolder != null && openTheFolder)
				{
					await _currentFolder.OpenAsync().ConfigureAwait(false);
				}
				RaisePropertyChanged_UI(nameof(CurrentFolder)); // notify the UI once the data has been loaded
			}
		}

		public Task SetCurrentFolderIdAsync(string folderId)
		{
			return RunFunctionIfOpenAsyncT(delegate
			{
				CurrentFolderId = folderId;
				return UpdateCurrentFolder2Async(false);
			});
		}

		public Task<bool> OpenCurrentFolderAsync()
		{
			return RunFunctionIfOpenAsyncT(delegate
			{
				return UpdateCurrentFolder2Async(true);
			});
		}

		public Task<bool> OpenFolderAsync(string folderId)
		{
			return RunFunctionIfOpenAsyncT(delegate
			{
				CurrentFolderId = folderId;
				return UpdateCurrentFolder2Async(true);
			});
		}

		public async Task<Folder> AddFolderAsync()
		{
			var folder = new Folder(_dbManager);

			bool isOk = await RunFunctionIfOpenAsyncTB(async delegate
			{
				// folder.ParentId = Id; // folders may not have ParentId because they can be exported or imported
				folder.Name = RuntimeData.GetText("NewFolder");
				folder.DateCreated = DateTime.Now;

				if (await _dbManager.InsertIntoFoldersAsync(folder, true))
				{
					_folders.Add(folder);
					return true;
				}

				return false;
			});

			if (isOk) return folder;
			else return null;
		}
		public Task<bool> ImportFoldersAsync(StorageFolder fromDirectory)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (fromDirectory == null) return false;
				bool isOk = false;
				bool isDeleteTempDir = false;
				MergingBinder mergingBinder = null;
				StorageFolder tempDirectory = null;

				try
				{
					// I can only import from the app local folder, otherwise sqlite says "Cannot open", even in read-only mode. 
					// So I copy the source files into the temp directory.
					if (fromDirectory.Path.Contains(ApplicationData.Current.LocalCacheFolder.Path) || fromDirectory.Path.Contains(ApplicationData.Current.LocalFolder.Path))
					{
						tempDirectory = fromDirectory;
					}
					else
					{
						tempDirectory = await ApplicationData.Current.LocalCacheFolder
							.CreateFolderAsync(Guid.NewGuid().ToString(), CreationCollisionOption.ReplaceExisting)
							.AsTask().ConfigureAwait(false);
						await fromDirectory.CopyDirContentsAsync(tempDirectory).ConfigureAwait(false);
						isDeleteTempDir = true;
					}

					mergingBinder = MergingBinder.CreateInstance(DBName, tempDirectory);
					await mergingBinder.OpenAsync().ConfigureAwait(false);

					// parallelisation here seems ideal, but it screws with SQLite.
					foreach (var fol in mergingBinder.Folders)
					{
						await fol.OpenAsync().ConfigureAwait(false);
						if (await _dbManager.InsertIntoFoldersAsync(fol, true).ConfigureAwait(false))
						{
							await fol.SetDbManager(_dbManager).ConfigureAwait(false);

							await _dbManager.InsertIntoWalletsAsync(fol.Wallets, true).ConfigureAwait(false);
							foreach (var wal in fol.Wallets)
							{
								foreach (var doc in wal.Documents)
								{
									var file = await StorageFile.GetFileFromPathAsync(doc.GetFullUri0(fromDirectory)).AsTask().ConfigureAwait(false);
									if (file != null)
									{
										// the file name might change to avoid name collisions
										var copiedFile = await file.CopyAsync(_directory, file.Name, NameCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);
										doc.Uri0 = copiedFile.Name;
									}
								}
								await _dbManager.InsertIntoDocumentsAsync(wal.Documents, true).ConfigureAwait(false);
							}

							await _dbManager.InsertIntoDynamicFieldsAsync(fol.DynamicFields, true).ConfigureAwait(false);
							await _dbManager.InsertIntoDynamicCategoriesAsync(fol.DynamicCategories, true).ConfigureAwait(false);

							_folders.Add(fol);
							//isOk = true;
						}
					}
					isOk = true;
				}
				catch (Exception ex)
				{
					await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
				}

				if (mergingBinder != null)
				{
					await mergingBinder.CloseAsync().ConfigureAwait(false);
					mergingBinder.Dispose();
				}
				mergingBinder = null;

				if (isDeleteTempDir && tempDirectory != null) await tempDirectory.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);

				return isOk;
			});
		}
		public Task<bool> RemoveFolderAsync(string folderId)
		{
			return RunFunctionIfOpenAsyncTB(delegate
			{
				return RemoveFolder2Async(folderId);
			});
		}
		public Task<bool> RemoveFolderAsync(Folder folder)
		{
			return RunFunctionIfOpenAsyncTB(delegate
			{
				return RemoveFolder2Async(folder);
			});
		}
		private async Task<bool> RemoveFolder2Async(string folderId)
		{
			var folder = _folders.FirstOrDefault(fol => fol.Id == folderId);
			return await RemoveFolder2Async(folder).ConfigureAwait(false);
		}
		private async Task<bool> RemoveFolder2Async(Folder folder)
		{
			if (folder == null) return false;

			if (await _dbManager.DeleteFromFoldersAsync(folder))
			{
				if (folder.Id == _currentFolderId)
				{
					int previousFolderIndex = Math.Max(0, _folders.IndexOf(folder) - 1);
					CurrentFolderId = _folders.Count > previousFolderIndex ? _folders[previousFolderIndex].Id : DEFAULT_ID;
					await UpdateCurrentFolder2Async(false);
				}

				await RunInUiThreadAsync(delegate { _folders.Remove(folder); }).ConfigureAwait(false);

				await folder.OpenAsync().ConfigureAwait(false);
				await folder.RemoveWalletsAsync().ConfigureAwait(false);
				await folder.CloseAsync().ConfigureAwait(false);
				folder.Dispose();

				return true;
			}
			else
			{
				Debugger.Break(); // LOLLO this must never happen, check it
				await Logger.AddAsync("Attempting to remove folder, the db operation failed", Logger.FileErrorLogFilename).ConfigureAwait(false);
			}
			return false;
		}
		#endregion while open methods


		#region while open filter methods
		public async Task<List<FolderPreview>> GetAllFolderPreviewsAsync()
		{
			var output = new List<FolderPreview>();
			await RunFunctionIfOpenAsyncT(async delegate
			{
				if (WhichFilter != Filters.All) return;

				var folders = await _dbManager.GetFoldersAsync().ConfigureAwait(false);
				var wallets = await _dbManager.GetWalletsAsync().ConfigureAwait(false);
				var docs = await _dbManager.GetDocumentsAsync().ConfigureAwait(false);

				output = GetFolderPreviews(folders, wallets, docs);
			}).ConfigureAwait(false);
			return output;
		}

		public async Task<List<FolderPreview>> GetRecentFolderPreviewsAsync()
		{
			var output = new List<FolderPreview>();
			await RunFunctionIfOpenAsyncT(async delegate
			{
				if (WhichFilter != Filters.Recent) return;
				var folders = (await _dbManager.GetFoldersAsync().ConfigureAwait(false)).OrderByDescending(ff => ff.DateCreated).Take(HOW_MANY_FOLDERS_IN_RECENT_VIEW);
				var wallets = await _dbManager.GetWalletsAsync().ConfigureAwait(false);
				var documents = await _dbManager.GetDocumentsAsync().ConfigureAwait(false);

				output = GetFolderPreviews(folders, wallets, documents);
			}).ConfigureAwait(false);
			return output;
		}

		public async Task<List<FolderPreview>> GetByCatFolderPreviewsAsync()
		{
			var output = new List<FolderPreview>();
			await RunFunctionIfOpenAsyncT(async delegate
			{
				if (WhichFilter != Filters.Cat || _dbManager == null || CatIdForCatFilter == null || CatIdForCatFilter == DEFAULT_ID) return;

				//var dynCatsTest = await _binder.DbManager.GetDynamicCategoriesAsync().ConfigureAwait(false);
				var dynCatsWithChosenId = await _dbManager.GetDynamicCategoriesByCatIdAsync(CatIdForCatFilter).ConfigureAwait(false);
				var folders = (await _dbManager.GetFoldersAsync().ConfigureAwait(false)).Where(fol => dynCatsWithChosenId.Any(cat => cat.ParentId == fol.Id));
				var wallets = (await _dbManager.GetWalletsAsync().ConfigureAwait(false)).Where(wal => folders.Any(fol => fol.Id == wal.ParentId));
				var documents = (await _dbManager.GetDocumentsAsync().ConfigureAwait(false)).Where(doc => wallets.Any(wal => wal.Id == doc.ParentId));

				output = GetFolderPreviews(folders, wallets, documents);
			}).ConfigureAwait(false);
			return output;
		}

		public async Task<List<FolderPreview>> GetByFldFolderPreviewsAsync()
		{
			var output = new List<FolderPreview>();
			await RunFunctionIfOpenAsyncT(async delegate
			{
				if (WhichFilter != Filters.Field || _dbManager == null || FldDscIdForFldFilter == null || FldDscIdForFldFilter == DEFAULT_ID) return;

				var dynFldsWithChosenId = (await _dbManager.GetDynamicFieldsByFldDscIdAsync(FldDscIdForFldFilter).ConfigureAwait(false))
					.Where(df => df.FieldValue?.Id == FldValIdForFldFilter);
				var folders = (await _dbManager.GetFoldersAsync().ConfigureAwait(false)).Where(fol => dynFldsWithChosenId.Any(df => df.ParentId == fol.Id));
				var wallets = (await _dbManager.GetWalletsAsync().ConfigureAwait(false)).Where(wal => folders.Any(fol => fol.Id == wal.ParentId));
				var documents = (await _dbManager.GetDocumentsAsync().ConfigureAwait(false)).Where(doc => wallets.Any(wal => wal.Id == doc.ParentId));

				output = GetFolderPreviews(folders, wallets, documents);
			}).ConfigureAwait(false);
			return output;
		}
		private List<FolderPreview> GetFolderPreviews(IEnumerable<Folder> folders, IEnumerable<Wallet> wallets, IEnumerable<Document> documents)
		{
			var folderPreviews = new List<FolderPreview>();

			try
			{
				foreach (var fol in folders)
				{
					var folderPreview = new FolderPreview() { FolderName = fol.Name, FolderId = fol.Id };
					bool exit = false;
					foreach (var wal in wallets.Where(wlt => wlt.ParentId == fol.Id))
					{
						foreach (var doc in documents.Where(dcm => dcm.ParentId == wal.Id))
						{
							if (!string.IsNullOrWhiteSpace(doc.Uri0))
							{
								folderPreview.DocumentUri0 = doc.GetFullUri0();
								folderPreview.Document = doc;
								exit = true;
							}
							if (exit) break;
						}
						if (exit) break;
					}
					folderPreviews.Add(folderPreview);
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}

			return folderPreviews;
		}
		#endregion while open filter methods

		protected override bool UpdateDbMustOverride()
		{
			throw new NotImplementedException();
		}

		protected override bool CheckMeMustOverride()
		{
			throw new NotImplementedException("ERROR in Binder: CheckOneValue() was called but it must not. ");
		}
	}
}