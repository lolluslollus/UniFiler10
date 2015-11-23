using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using Utilz;
using System.Reflection;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using UniFiler10.Data.Metadata;
using System.Diagnostics;
using Windows.Storage;
using Windows.Storage.Streams;
using System.IO;
using Windows.ApplicationModel.Resources.Core;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public sealed class Binder : DbBoundObservableData
	{
		#region construct and dispose
		private static readonly object _instanceLock = new object();
		internal static Binder CreateInstance(string dbName, IPaneOpener parentPaneOpener)
		{
			lock (_instanceLock)
			{
				if (_instance == null || _instance._isDisposed)
				{
					_instance = new Binder(dbName, parentPaneOpener);
				}
				return _instance;
			}
		}
		private Binder(string dbName, IPaneOpener parentPaneOpener)
		{
			if (dbName == null || string.IsNullOrWhiteSpace(dbName)) throw new ArgumentException("Binder ctor: dbName cannot be null or empty");
			if (parentPaneOpener == null) throw new ArgumentNullException("Binder ctor: parentPaneOpener cannot be null");

			DBName = dbName;
			ParentPaneOpener = parentPaneOpener;
		}
		#endregion construct and dispose


		#region open and close
		public override async Task<bool> SetIsEnabledAsync(bool enable)
		{
			try
			{
				await _isClosedSemaphore.WaitAsync().ConfigureAwait(false);
				return await base.SetIsEnabledAsync(enable).ConfigureAwait(false);
			}
			finally
			{
				_isClosedSemaphore.Release();
			}
		}
		public override async Task<bool> OpenAsync(bool enable = true)
		{
			try
			{
				await _isClosedSemaphore.WaitAsync().ConfigureAwait(false);
				return await base.OpenAsync(enable).ConfigureAwait(false);
			}
			finally
			{
				_isClosedSemaphore.Release();
			}
		}
		protected override async Task OpenMayOverrideAsync()
		{
			_dbManager = DBManager.CreateInstance(_dbName);
			await _dbManager.OpenAsync().ConfigureAwait(false);

			await LoadNonDbPropertiesAsync().ConfigureAwait(false);
			await LoadFoldersWithoutContentAsync().ConfigureAwait(false);

			_runAsSoonAsOpen = delegate { return UpdateCurrentFolderAsync(); };
			//await UpdateCurrentFolder2Async().ConfigureAwait(false);
		}
		protected override async Task CloseMayOverrideAsync()
		{
			await _dbManager.CloseAsync().ConfigureAwait(false);
			_dbManager?.Dispose();
			_dbManager = null;

			foreach (var folder in _folders)
			{
				await folder.CloseAsync().ConfigureAwait(false);
			}

			await SaveNonDbPropertiesAsync().ConfigureAwait(false);

			await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
			{
				_folders.Clear();
				CurrentFolder = null;
			}).AsTask().ConfigureAwait(false);
		}
		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_folders?.Dispose();
			_folders = null;
		}
		#endregion open and close


		#region main properties
		private DBManager _dbManager = null;
		[IgnoreDataMember]
		internal DBManager DbManager { get { return _dbManager; } }

		private static Binder _instance = null;
		[IgnoreDataMember]
		public static Binder OpenInstance { get { var instance = _instance; if (instance != null && instance._isOpen) return instance; else return null; } }

		private string _dbName = string.Empty;
		[DataMember]
		public string DBName { get { return _dbName; } private set { if (_dbName != value) { _dbName = value; RaisePropertyChanged_UI(); } } }

		private IPaneOpener _parentPaneOpener = null;
		[IgnoreDataMember]
		public IPaneOpener ParentPaneOpener { get { return _parentPaneOpener; } private set { _parentPaneOpener = value; RaisePropertyChanged_UI(); /*UpdateIsPaneOpen();*/ } }

		private SwitchableObservableCollection<Folder> _folders = new SwitchableObservableCollection<Folder>();
		[IgnoreDataMember]
		public SwitchableObservableCollection<Folder> Folders { get { return _folders; } private set { if (_folders != value) { _folders = value; RaisePropertyChanged(); } } }

		private string _currentFolderId = DEFAULT_ID;
		[DataMember]
		public string CurrentFolderId
		{
			get { return _currentFolderId; }
			set
			{
				if (_currentFolderId != value)
				{
					_currentFolderId = value;
					Task upd = UpdateCurrentFolderAsync();
					RaisePropertyChanged_UI();
				}
				else if (_currentFolder == null)
				{
					Task upd = UpdateCurrentFolderAsync();
				}
			}
		}
		private async Task UpdateCurrentFolder2Async()
		{
			if (_folders != null)
			{
				foreach (var item in _folders)
				{
					item.IsSelected = false;
				}
				if (_currentFolderId != null)
				{
					// do not close the folder, just disable it. It keeps more memory busy but it's faster.
					if (_currentFolder != null) await _currentFolder.SetIsEnabledAsync(false).ConfigureAwait(false);
					//if (_currentFolder != null) await _currentFolder.CloseAsync().ConfigureAwait(false);
					_currentFolder = _folders.FirstOrDefault(fo => fo.Id == _currentFolderId);
				}
				else
				{
					_currentFolder = null;
				}
				if (_currentFolder != null)
				{
					await _currentFolder.OpenAsync().ConfigureAwait(false);
					_currentFolder.IsSelected = true;					
				}
				RaisePropertyChanged_UI(nameof(CurrentFolder)); // notify the UI once the data has been loaded
			}
		}
		private Task UpdateCurrentFolderAsync()
		{
			return RunFunctionWhileEnabledAsyncT(UpdateCurrentFolder2Async);
		}

		public Task SetCurrentFolderIdAsync(string newValue)
		{
			return RunFunctionWhileEnabledAsyncA(delegate { CurrentFolderId = newValue; });
		}

		private Folder _currentFolder = null;
		[IgnoreDataMember]
		public Folder CurrentFolder { get { return _currentFolder; } private set { if (_currentFolder != value) { _currentFolder = value; RaisePropertyChanged_UI(); } } }

		//private bool _isPaneOpen = true;
		//[DataMember]
		//public bool IsPaneOpen { get { return _isPaneOpen; } set { if (_isPaneOpen != value) { _isPaneOpen = value; RaisePropertyChanged_UI(); /*if (!_isPaneOpen) _parentPaneOpener.IsPaneOpen = false; */} } }
		//private void UpdateIsPaneOpen()
		//{
		//    if (_parentPaneOpener == null) IsPaneOpen = false;
		//}
		//public void ToggleIsPaneOpen()
		//{
		//    IsPaneOpen = !_isPaneOpen;
		//}

		private bool _isCoverOpen = true;
		[DataMember]
		public bool IsCoverOpen { get { return _isCoverOpen; } set { if (_isCoverOpen != value) { _isCoverOpen = value; RaisePropertyChanged_UI(); } } }
		public void SetIsCoverOpen(bool newValue)
		{
			IsCoverOpen = newValue;
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
			return RunFunctionWhileEnabledAsyncA(delegate // only when it's open, to avoid surprises from the binding when objects are closed and reset
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
			return RunFunctionWhileEnabledAsyncA(delegate // only when it's open, to avoid surprises from the binding when objects are closed and reset
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
				var directory = await GetDirectoryAsync().ConfigureAwait(false);
				var file = await directory
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
				if (newBinder != null) CopyNonDbProperties(newBinder);
			}

			Debug.WriteLine("ended method Binder.LoadAsync()");
		}
		private async Task SaveNonDbPropertiesAsync()
		{
			Binder binderClone = CloneNonDbProperties();
			//for (int i = 0; i < 100000000; i++) //wait a few seconds, for testing
			//{
			//    String aaa = i.ToString();
			//}

			try
			{
				using (MemoryStream memoryStream = new MemoryStream())
				{
					var folder = await GetDirectoryAsync().ConfigureAwait(false);
					var file = await folder
						.CreateFileAsync(FILENAME, CreationCollisionOption.ReplaceExisting)
						.AsTask().ConfigureAwait(false);

					DataContractSerializer sessionDataSerializer = new DataContractSerializer(typeof(Binder));
					sessionDataSerializer.WriteObject(memoryStream, binderClone);

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
		public async Task<StorageFolder> GetDirectoryAsync()
		{
			var folder = await ApplicationData.Current.LocalFolder
				.CreateFolderAsync(DBName, CreationCollisionOption.OpenIfExists)
				.AsTask().ConfigureAwait(false);
			return folder;
		}
		private void CopyNonDbProperties(Binder source)
		{
			//IsPaneOpen = source.IsPaneOpen;
			IsCoverOpen = source.IsCoverOpen;
			CatIdForFldFilter = source.CatIdForFldFilter;
			FldDscIdForFldFilter = source.FldDscIdForFldFilter;
			FldValIdForFldFilter = source.FldValIdForFldFilter;
			CatIdForCatFilter = source.CatIdForCatFilter;
			WhichFilter = source.WhichFilter;
			DBName = source.DBName;
			CurrentFolderId = source.CurrentFolderId; // last
		}
		private Binder CloneNonDbProperties()
		{
			Binder target = new Binder(DBName, _parentPaneOpener);
			target.CurrentFolderId = _currentFolderId;
			//target.IsPaneOpen = _isPaneOpen;
			target.IsCoverOpen = _isCoverOpen;
			target.CatIdForFldFilter = _catIdForFldFilter;
			target.FldDscIdForFldFilter = _fldDscIdForFldFilter;
			target.FldValIdForFldFilter = _fldValIdForFldFilter;
			target.CatIdForCatFilter = _catIdForCatFilter;
			target.WhichFilter = _whichFilter;
			target.DBName = _dbName;
			return target;
		}
		private async Task LoadFoldersWithoutContentAsync()
		{
			var folders = await _dbManager.GetFoldersAsync();

			await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
			{
				Folders.Clear();
				Folders.AddRange(folders);
			}).AsTask().ConfigureAwait(false);
		}

		#endregion loading methods


		#region closed static methods
		/// <summary>
		/// I need this semaphore for the static operations such as backup, restore, etc
		/// </summary>
		private static SemaphoreSlimSafeRelease _isClosedSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		public static async Task<bool> DeleteClosedBinderAsync(string dbName)
		{
			try
			{
				_isClosedSemaphore.Wait();
				var openBinder = OpenInstance;
				if (openBinder == null || openBinder.DBName != dbName)
				{
					try
					{
						var storageFolder = await ApplicationData.Current.LocalFolder
							.GetFolderAsync(dbName)
							.AsTask().ConfigureAwait(false);
						if (storageFolder != null) await storageFolder.DeleteAsync(StorageDeleteOption.Default).AsTask().ConfigureAwait(false);
						return true;
					}
					catch (Exception ex)
					{
						Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
					}
				}
			}
			finally
			{
				_isClosedSemaphore.Release();
			}
			return false;
		}
		public static async Task<bool> BackupDisabledBinderAsync(string dbName, StorageFolder into)
		{
			try
			{
				_isClosedSemaphore.Wait();
				var openBinder = OpenInstance;
				if (!string.IsNullOrWhiteSpace(dbName) && into != null && (openBinder == null || openBinder.DBName != dbName || !openBinder.IsEnabled))
				{
					try
					{
						var fromStorageFolder = await ApplicationData.Current.LocalFolder
							.GetFolderAsync(dbName)
							.AsTask().ConfigureAwait(false);
						if (fromStorageFolder != null)
						{
							var toStorageFolder = await into
								.CreateFolderAsync(dbName, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);
							var fromFiles = await fromStorageFolder.GetFilesAsync().AsTask().ConfigureAwait(false);
							foreach (var stoFile in fromFiles)
							{
								await stoFile.CopyAsync(toStorageFolder, stoFile.Name, NameCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
							}
							return true;
							// LOLLO TODO test this and make sure there are no nested folders, otherwise we need a little more code
						}
					}
					catch (Exception ex)
					{
						Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
					}
				}
			}
			finally
			{
				_isClosedSemaphore.Release();
			}
			return false;
		}

		public static async Task<bool> RestoreClosedBinderAsync(StorageFolder from)
		{
			try
			{
				_isClosedSemaphore.Wait();
				// LOLLO TODO check if you are restoring a Binder or something completely unrelated, which may cause trouble.
				// Make sure you restore a Binder and not just any directory!
				var openBinder = OpenInstance;
				if (from == null || openBinder == null || openBinder.DBName != from.Name)
				{
					try
					{
						var toStorageFolder = await ApplicationData.Current.LocalFolder
							.CreateFolderAsync(from.Name, CreationCollisionOption.ReplaceExisting)
							.AsTask().ConfigureAwait(false);
						var fromFiles = await from.GetFilesAsync().AsTask().ConfigureAwait(false);
						foreach (var stoFile in fromFiles)
						{
							await stoFile.CopyAsync(toStorageFolder, stoFile.Name, NameCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
						}
						// LOLLO TODO test this and make sure there are no nested folders, otherwise we need a little more code
						return true;
					}
					catch (Exception ex)
					{
						Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
					}
				}
			}
			finally
			{
				_isClosedSemaphore.Release();
			}
			return false;
		}
		#endregion closed static methods


		#region while open methods
		public Task<bool> AddFolderAsync(Folder folder)
		{
			return RunFunctionWhileEnabledAsyncTB(async delegate
			{
				if (folder != null)
				{
					folder.ParentId = Id;
					folder.Name = ResourceManager.Current.MainResourceMap.GetValue("Resources/NewFolder/Text", ResourceContext.GetForCurrentView()).ValueAsString;

					folder.DateCreated = DateTime.Now;

					if (Check(folder))
					{
						if (await _dbManager.InsertIntoFoldersAsync(folder, true))
						{
							Folders.Add(folder);
							await folder.OpenAsync();
							CurrentFolderId = folder.Id;
							return true;
						}
					}
				}
				return false;
			});
		}
		public Task<bool> RemoveFolderAsync(string folderId)
		{
			return RunFunctionWhileEnabledAsyncTB(delegate
			{
				return RemoveFolder2Async(folderId);
			});
		}
		public Task<bool> RemoveFolderAsync(Folder folder)
		{
			return RunFunctionWhileEnabledAsyncTB(delegate
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
					bool isOK = _folders.Remove(folder);
					await folder.CloseAsync();
					CurrentFolderId = _folders.Count > previousFolderIndex ? _folders[previousFolderIndex].Id : DEFAULT_ID;
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
			await RunFunctionWhileEnabledAsyncT(async delegate
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
			await RunFunctionWhileEnabledAsyncT(async delegate
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
			await RunFunctionWhileEnabledAsyncT(async delegate
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
			await RunFunctionWhileEnabledAsyncT(async delegate
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
							folderPreview.DocumentUri0 = doc.Uri0;
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
		//protected override Task<bool> UpdateDbMustOverrideAsync()
		//{
		//	throw new NotImplementedException("ERROR in Binder: UpdateDbWithinSemaphoreMustOverride() was called but it must not. ");
		//}

		//protected override void CopyMustOverride(ref DbBoundObservableData target)
		//{
		//	throw new NotImplementedException("ERROR in Binder: CopyNoDbMustOverride() was called but it must not. ");
		//}

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
