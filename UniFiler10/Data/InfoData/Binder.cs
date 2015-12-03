﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using Utilz;
using Windows.ApplicationModel.Resources.Core;
using Windows.Storage;
using Windows.Storage.Streams;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public sealed class Binder : DbBoundObservableData
	{
		#region construct and dispose
		private static readonly object _instanceLock = new object();
		internal static Binder CreateInstance(string dbName)
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
		private Binder(string dbName)
		{
			if (dbName == null || string.IsNullOrWhiteSpace(dbName)) throw new ArgumentException("Binder ctor: dbName cannot be null or empty");
			DBName = dbName;
		}
		#endregion construct and dispose


		#region open and close
		//public override async Task<bool> OpenAsync()
		//{
		//	try
		//	{
		//		await _isClosedSemaphore.WaitAsync().ConfigureAwait(false);
		//		return await base.OpenAsync().ConfigureAwait(false);
		//	}
		//	finally
		//	{
		//		_isClosedSemaphore.Release();
		//	}
		//}
		protected override async Task OpenMayOverrideAsync()
		{
			await GetCreateDirectoryAsync().ConfigureAwait(false);

			_dbManager = DBManager.CreateInstance(_dbName);
			await _dbManager.OpenAsync().ConfigureAwait(false);

			await LoadNonDbPropertiesAsync().ConfigureAwait(false);
			await LoadFoldersWithoutContentAsync().ConfigureAwait(false);

			await UpdateCurrentFolder2Async(false).ConfigureAwait(false);
		}
		protected override async Task CloseMayOverrideAsync()
		{
			await _dbManager.CloseAsync().ConfigureAwait(false);
			_dbManager?.Dispose();
			_dbManager = null;

			Task closeFolders = new Task(delegate
			{
				Parallel.ForEach(_folders, (folder) =>
				{
					// await folder.CloseAsync().ConfigureAwait(false); // LOLLO NOTE avoid async calls within a Parallel.ForEach coz they are not awaited
					folder.Dispose();
				});
			});

			Task save = SaveNonDbPropertiesAsync();

			closeFolders.Start();
			// Task.WaitAll(closeFolders, save); // this is not awaitable
			await Task.WhenAll(closeFolders, save).ConfigureAwait(false);

			await RunInUiThreadAsync(delegate
			{
				_folders.Clear();
				_currentFolder = null; // don't set CurrentFolder, it triggers stuff
			}).ConfigureAwait(false);

			//foreach (var folder in _folders)
			//{
			//	await folder.CloseAsync().ConfigureAwait(false);
			//	folder.Dispose();
			//}

			//await SaveNonDbPropertiesAsync().ConfigureAwait(false);

			//await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
			//{
			//	_folders.Clear();
			//	CurrentFolder = null;
			//}).AsTask().ConfigureAwait(false);
		}
		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_folders?.Dispose();
			_folders = null;
		}
		#endregion open and close


		#region main properties
		private StorageFolder _directory = null;
		[IgnoreDataMember]
		public StorageFolder Directory { get { return _directory; } }


		private DBManager _dbManager = null;
		[IgnoreDataMember]
		internal DBManager DbManager { get { return _dbManager; } }

		private static Binder _instance = null;
		[IgnoreDataMember]
		public static Binder OpenInstance { get { var instance = _instance; if (instance != null && instance._isOpen) return instance; else return null; } }

		private string _dbName = string.Empty;
		[DataMember]
		public string DBName { get { return _dbName; } private set { if (_dbName != value) { _dbName = value; RaisePropertyChanged_UI(); } } }

		private SwitchableObservableCollection<Folder> _folders = new SwitchableObservableCollection<Folder>();
		[IgnoreDataMember]
		public SwitchableObservableCollection<Folder> Folders { get { return _folders; } private set { if (_folders != value) { _folders = value; RaisePropertyChanged(); } } }

		private string _currentFolderId = DEFAULT_ID;
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
				//if (_currentFolderId != value)
				//{
				//	_currentFolderId = value;
				//	Task upd = UpdateCurrentFolderAsync().ContinueWith(delegate
				//	{
				//		RaisePropertyChanged_UI();
				//	});
				//}
				//else if (_currentFolder == null)
				//{
				//	Task upd = UpdateCurrentFolderAsync();
				//}
			}
		}

		private Folder _currentFolder = null;
		[IgnoreDataMember]
		public Folder CurrentFolder
		{
			get { return _currentFolder; }
			private set { if (_currentFolder != value) { _currentFolder = value; RaisePropertyChanged_UI(); } }
		}
		#endregion main properties


		#region filter properties
		public enum Filters { All, Recent, Cat, Field };
		private const int HOW_MANY_IN_RECENT = 10;

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
		public string CatIdForCatFilter // the setter is only for serialising and copying
		{
			get { return _catIdForCatFilter; }
			set
			{
				string newValue = value ?? DEFAULT_ID;
				if (_catIdForCatFilter != newValue) { _catIdForCatFilter = newValue; RaisePropertyChanged(); }
			}
		}
		public Task SetIdsForCatFilterAsync(string catId)
		{
			return RunFunctionWhileOpenAsyncA(delegate // only when it's open, to avoid surprises from the binding when objects are closed and reset
			{
				CatIdForCatFilter = catId;
			});
		}

		private string _catIdForFldFilter = DEFAULT_ID;
		[DataMember]
		public string CatIdForFldFilter // the setter is only for serialising and copying
		{
			get { return _catIdForFldFilter; }
			set
			{
				string newValue = value ?? DEFAULT_ID;
				if (_catIdForFldFilter != newValue) { _catIdForFldFilter = newValue; RaisePropertyChanged(); }
			}
		}

		private string _fldDscIdForFldFilter = DEFAULT_ID;
		[DataMember]
		public string FldDscIdForFldFilter // the setter is only for serialising and copying
		{
			get { return _fldDscIdForFldFilter; }
			set
			{
				string newValue = value ?? DEFAULT_ID;
				if (_fldDscIdForFldFilter != newValue) { _fldDscIdForFldFilter = newValue; RaisePropertyChanged(); }
			}
		}

		private string _fldValIdForFldFilter = DEFAULT_ID;
		[DataMember]
		public string FldValIdForFldFilter // the setter is only for serialising and copying
		{
			get { return _fldValIdForFldFilter; }
			set
			{
				string newValue = value ?? DEFAULT_ID;
				if (_fldValIdForFldFilter != newValue) { _fldValIdForFldFilter = newValue; RaisePropertyChanged(); }
			}
		}
		public Task SetIdsForFldFilterAsync(string catId, string fldDscId, string fldValId)
		{
			return RunFunctionWhileOpenAsyncA(delegate // only when it's open, to avoid surprises from the binding when objects are closed and reset
			{
				CatIdForFldFilter = catId;
				FldDscIdForFldFilter = fldDscId;
				FldValIdForFldFilter = fldValId;
			});
		}

		private Filters _whichFilter = Filters.Recent;
		[DataMember]
		public Filters WhichFilter { get { return _whichFilter; } private set { _whichFilter = value; RaisePropertyChanged_UI(); } }
		public void SetFilter(Filters newValue)
		{
			if (_whichFilter != newValue) WhichFilter = newValue;
		}
		#endregion filter properties


		#region loading methods
		internal const string FILENAME = "LolloSessionDataBinder.xml";

		private async Task LoadNonDbPropertiesAsync()
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
		//public async Task<StorageFolder> GetCreateDirectoryAsync()
		//{
		//	var dir = await Briefcase.BindersDirectory
		//		.CreateFolderAsync(DBName, CreationCollisionOption.OpenIfExists)
		//		.AsTask().ConfigureAwait(false);
		//	return dir;
		//}
		private async Task GetCreateDirectoryAsync()
		{
			_directory = await Briefcase.BindersDirectory
				.CreateFolderAsync(DBName, CreationCollisionOption.OpenIfExists)
				.AsTask().ConfigureAwait(false);
		}
		private void CopyFrom(Binder source)
		{
			CatIdForFldFilter = source._catIdForFldFilter;
			FldDscIdForFldFilter = source._fldDscIdForFldFilter;
			FldValIdForFldFilter = source._fldValIdForFldFilter;
			CatIdForCatFilter = source._catIdForCatFilter;
			WhichFilter = source._whichFilter;
			DBName = source._dbName;
			CurrentFolderId = source._currentFolderId; // CurrentFolder will be updated later
		}
		private async Task LoadFoldersWithoutContentAsync()
		{
			var folders = await _dbManager.GetFoldersAsync();

			await RunInUiThreadAsync(delegate
			{
				Folders.Clear();
				Folders.AddRange(folders);
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
			return RunFunctionWhileOpenAsyncT(delegate
			{
				CurrentFolderId = folderId;
				return UpdateCurrentFolder2Async(false);
			});
		}

		public Task<bool> OpenCurrentFolderAsync()
		{
			return RunFunctionWhileOpenAsyncT(delegate
			{
				return UpdateCurrentFolder2Async(true);
			});
		}

		public Task<bool> OpenFolderAsync(string folderId)
		{
			return RunFunctionWhileOpenAsyncT(delegate
			{
				CurrentFolderId = folderId;
				return UpdateCurrentFolder2Async(true);
			});
		}

		public async Task<Folder> AddFolderAsync()
		{
			var folder = new Folder();

			bool isOk = await RunFunctionWhileOpenAsyncTB(async delegate
			{
				folder.ParentId = Id;
				folder.Name = ResourceManager.Current.MainResourceMap.GetValue("Resources/NewFolder/Text", ResourceContext.GetForCurrentView()).ValueAsString;
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

		public Task<bool> RemoveFolderAsync(string folderId)
		{
			return RunFunctionWhileOpenAsyncTB(delegate
			{
				return RemoveFolder2Async(folderId);
			});
		}
		public Task<bool> RemoveFolderAsync(Folder folder)
		{
			return RunFunctionWhileOpenAsyncTB(delegate
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
			if (folder != null)
			{
				if (await _dbManager.DeleteFromFoldersAsync(folder))
				{
					int previousFolderIndex = Math.Max(0, _folders.IndexOf(folder) - 1);
					await folder.CloseAsync();
					bool isOK = _folders.Remove(folder);
					if (folder.Id == _currentFolderId)
					{
						CurrentFolderId = _folders.Count > previousFolderIndex ? _folders[previousFolderIndex].Id : DEFAULT_ID;
						await UpdateCurrentFolder2Async(false);
					}
					return isOK;
				}
				else
				{
					Debugger.Break(); // LOLLO this must never happen, check it
				}
			}
			return false;
		}
		#endregion while open methods


		#region while open filter methods
		public async Task<List<FolderPreview>> GetAllFolderPreviewsAsync()
		{
			List<FolderPreview> output = null;
			await RunFunctionWhileOpenAsyncT(async delegate
			{
				if (WhichFilter != Filters.All) return;

				var folders = await _dbManager.GetFoldersAsync().ConfigureAwait(false);
				var wallets = await _dbManager.GetWalletsAsync().ConfigureAwait(false);
				var documents = await _dbManager.GetDocumentsAsync().ConfigureAwait(false);

				output = GetFolderPreviews(folders, wallets, documents);
			});
			return output;
		}

		public async Task<List<FolderPreview>> GetRecentFolderPreviewsAsync()
		{
			List<FolderPreview> output = null;
			await RunFunctionWhileOpenAsyncT(async delegate
			{
				if (WhichFilter != Filters.Recent) return;

				var folders = (await _dbManager.GetFoldersAsync().ConfigureAwait(false)).OrderByDescending(ff => ff.DateCreated).Take(HOW_MANY_IN_RECENT);
				var wallets = await _dbManager.GetWalletsAsync().ConfigureAwait(false);
				var documents = await _dbManager.GetDocumentsAsync().ConfigureAwait(false);

				output = GetFolderPreviews(folders, wallets, documents);
			});
			return output;
		}

		public async Task<List<FolderPreview>> GetByCatFolderPreviewsAsync()
		{
			List<FolderPreview> output = null;
			await RunFunctionWhileOpenAsyncT(async delegate
			{
				if (WhichFilter != Filters.Cat || _dbManager == null || _catIdForCatFilter == null || _catIdForCatFilter == DEFAULT_ID) return;

				//var dynCatsTest = await _binder.DbManager.GetDynamicCategoriesAsync().ConfigureAwait(false);
				var dynCatsWithChosenId = await _dbManager.GetDynamicCategoriesByCatIdAsync(_catIdForCatFilter).ConfigureAwait(false);
				var folders = (await _dbManager.GetFoldersAsync().ConfigureAwait(false)).Where(fol => dynCatsWithChosenId.Any(cat => cat.ParentId == fol.Id));
				var wallets = await _dbManager.GetWalletsAsync().ConfigureAwait(false);
				var documents = await _dbManager.GetDocumentsAsync().ConfigureAwait(false);

				output = GetFolderPreviews(folders, wallets, documents);
			});
			return output;
		}

		public async Task<List<FolderPreview>> GetByFldFolderPreviewsAsync()
		{
			List<FolderPreview> output = null;
			await RunFunctionWhileOpenAsyncT(async delegate
			{
				if (WhichFilter != Filters.Field || _dbManager == null || _fldDscIdForFldFilter == null) return;

				var dynFldsWithChosenId = (await _dbManager.GetDynamicFieldsByFldDscIdAsync(_fldDscIdForFldFilter).ConfigureAwait(false))
					.Where(df => df.FieldValue?.Id == _fldValIdForFldFilter);
				var folders = (await _dbManager.GetFoldersAsync().ConfigureAwait(false)).Where(fol => dynFldsWithChosenId.Any(df => df.ParentId == fol.Id));
				var wallets = await _dbManager.GetWalletsAsync().ConfigureAwait(false);
				var documents = await _dbManager.GetDocumentsAsync().ConfigureAwait(false);

				output = GetFolderPreviews(folders, wallets, documents);
			});
			return output;
		}
		private List<FolderPreview> GetFolderPreviews(IEnumerable<Folder> folders, IEnumerable<Wallet> wallets, IEnumerable<Document> documents)
		{
			var folderPreviews = new List<FolderPreview>();

			foreach (var fol in folders)
			{
				var folderPreview = new FolderPreview() { FolderName = fol.Name, FolderId = fol.Id };
				bool exit = false;
				foreach (var wal in wallets.Where(w => w.ParentId == fol.Id))
				{
					foreach (var doc in documents.Where(d => d.ParentId == wal.Id))
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

			return folderPreviews;
		}
		#endregion while open filter methods

		protected override bool UpdateDbMustOverride()
		{
			throw new NotImplementedException();
		}

		protected override bool IsEqualToMustOverride(DbBoundObservableData that)
		{
			throw new NotImplementedException("ERROR in Binder: IsEqualToMustOverride() was called but it must not. ");
		}

		protected override bool CheckMeMustOverride()
		{
			throw new NotImplementedException("ERROR in Binder: CheckOneValue() was called but it must not. ");
		}
	}
}
