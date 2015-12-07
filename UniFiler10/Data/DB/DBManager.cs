using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using Utilz;
using Windows.Storage;

namespace UniFiler10.Data.DB
{
	public sealed class DBManager : OpenableObservableData
	{
		#region fields
		// one db for all tables
		// one semaphore each table
		//private string _dbName = string.Empty;
		private const string DB_FILE_NAME = "Db.db";
		private string _dbFullPath = string.Empty;
		private StorageFolder _directory = null;
		public StorageFolder Directory { get { return _directory; } }

		private readonly bool _isStoreDateTimeAsTicks = true;
		private readonly SQLiteOpenFlags _openFlags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create; //.FullMutex;

		private SemaphoreSlimSafeRelease _foldersSemaphore = null;
		private SemaphoreSlimSafeRelease _walletsSemaphore = null;
		private SemaphoreSlimSafeRelease _documentsSemaphore = null;
		private SemaphoreSlimSafeRelease _dynamicFieldsSemaphore = null;
		private SemaphoreSlimSafeRelease _dynamicCategoriesSemaphore = null;

		private LolloSQLiteConnectionPoolMT _connectionPool = null;
		#endregion fields

		#region construct and dispose
		//private static readonly object _instanceLock = new object();
		//private static DBManager _instance = null;
		//public static DBManager OpenInstance { get { if (_isOpen) return _instance; else return null; } }

		//public static DBManager CreateInstance(string dbName)
		//{
		//	lock (_instanceLock)
		//	{
		//		if (_instance == null || _instance._isDisposed)
		//		{
		//			_instance = new DBManager(dbName);
		//		}
		//		return _instance;
		//	}
		//}
		//public DBManager(string pathInFolder)
		//{
		//	if (pathInFolder != null && !string.IsNullOrWhiteSpace(pathInFolder))
		//	{
		//		_dbName = pathInFolder;
		//		// _dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, _dbName, DB_FILE_NAME);
		//		_dbPath = Path.Combine(Briefcase.BindersDirectory.Path, _dbName, DB_FILE_NAME);
		//	}
		//	else throw new ArgumentNullException("DBManager ctor: dbName cannot be null or empty");
		//}
		public DBManager(StorageFolder directory, bool isReadOnly)
		{
			if (directory != null)
			{
				//_dbName = pathInFolder;
				// _dbPath = Path.Combine(Briefcase.BindersDirectory.Path, _dbName, DB_FILE_NAME);
				if (isReadOnly)
				{
					_openFlags = SQLiteOpenFlags.ReadOnly;
				}
				else
				{
					_openFlags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create;
				}

				_directory = directory;
				_dbFullPath = Path.Combine(directory.Path, DB_FILE_NAME);
				_connectionPool = new LolloSQLiteConnectionPoolMT();
			}
			else throw new ArgumentNullException("DBManager ctor: dbName cannot be null or empty");
		}
		//private volatile bool _isDisposed = false;
		//public void Dispose()
		//{
		//	_isDisposed = true;
		//	CloseAsync().Wait();
		//}
		#endregion construct and dispose

		#region open and close
		//private SemaphoreSlimSafeRelease _isOpenSemaphore = null;
		//private volatile bool _isOpen = false;
		//public bool IsOpen { get { return _isOpen; } private set { _isOpen = value; } }
		protected override async Task OpenMayOverrideAsync()
		{
			// var dbFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(_dbName, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);
			//var dbFolder = await Briefcase.BindersDirectory.CreateFolderAsync(_dbName, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);

			if (!SemaphoreSlimSafeRelease.IsAlive(_foldersSemaphore)) _foldersSemaphore = new SemaphoreSlimSafeRelease(1, 1);
			if (!SemaphoreSlimSafeRelease.IsAlive(_walletsSemaphore)) _walletsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
			if (!SemaphoreSlimSafeRelease.IsAlive(_documentsSemaphore)) _documentsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
			if (!SemaphoreSlimSafeRelease.IsAlive(_dynamicFieldsSemaphore)) _dynamicFieldsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
			if (!SemaphoreSlimSafeRelease.IsAlive(_dynamicCategoriesSemaphore)) _dynamicCategoriesSemaphore = new SemaphoreSlimSafeRelease(1, 1);

			await _connectionPool.OpenAsync().ConfigureAwait(false);
		}
		/// <summary>
		/// Open the DB
		/// </summary>
		//public async Task OpenAsync()
		//{
		//	if (!_isOpen)
		//	{
		//		try
		//		{
		//			if (!SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore)) _isOpenSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		//			await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);

		//			if (!_isOpen)
		//			{
		//				// var dbFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(_dbName, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);
		//				//var dbFolder = await Briefcase.BindersDirectory.CreateFolderAsync(_dbName, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);

		//				if (!SemaphoreSlimSafeRelease.IsAlive(_foldersSemaphore)) _foldersSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		//				if (!SemaphoreSlimSafeRelease.IsAlive(_walletsSemaphore)) _walletsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		//				if (!SemaphoreSlimSafeRelease.IsAlive(_documentsSemaphore)) _documentsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		//				if (!SemaphoreSlimSafeRelease.IsAlive(_dynamicFieldsSemaphore)) _dynamicFieldsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		//				if (!SemaphoreSlimSafeRelease.IsAlive(_dynamicCategoriesSemaphore)) _dynamicCategoriesSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		//				LolloSQLiteConnectionPoolMT.Open();
		//				IsOpen = true;
		//			}
		//		}
		//		catch (Exception ex)
		//		{
		//			if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
		//				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
		//		}
		//	}
		//}
		protected override async Task CloseMayOverrideAsync()
		{
			try
			{
				await Task.Run(async delegate
				{
					_foldersSemaphore?.Wait();
					_walletsSemaphore?.Wait();
					_documentsSemaphore?.Wait();
					_dynamicFieldsSemaphore?.Wait();
					_dynamicCategoriesSemaphore?.Wait();

					var cp = _connectionPool;
					if (cp != null)
					{
						await cp.CloseAsync();
					}
				}).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				if (SemaphoreSlimSafeRelease.IsAlive(_foldersSemaphore)
					&& SemaphoreSlimSafeRelease.IsAlive(_walletsSemaphore)
					&& SemaphoreSlimSafeRelease.IsAlive(_documentsSemaphore)
					&& SemaphoreSlimSafeRelease.IsAlive(_dynamicFieldsSemaphore)
					&& SemaphoreSlimSafeRelease.IsAlive(_dynamicCategoriesSemaphore))
				{
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
					throw;
				}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryDispose(_dynamicCategoriesSemaphore);
				_dynamicCategoriesSemaphore = null;
				SemaphoreSlimSafeRelease.TryDispose(_dynamicFieldsSemaphore);
				_dynamicFieldsSemaphore = null;
				SemaphoreSlimSafeRelease.TryDispose(_documentsSemaphore);
				_documentsSemaphore = null;
				SemaphoreSlimSafeRelease.TryDispose(_walletsSemaphore);
				_walletsSemaphore = null;
				SemaphoreSlimSafeRelease.TryDispose(_foldersSemaphore);
				_foldersSemaphore = null;
			}
		}
		/// <summary>
		/// Wait for all DB operations to end and close the DB
		/// </summary>
		/// <returns></returns>
		//public async Task CloseAsync()
		//{
		//	if (_isOpen)
		//	{
		//		try
		//		{
		//			await Task.Run(() =>
		//			{
		//				if (_isOpen)
		//				{
		//					try
		//					{
		//						_foldersSemaphore.Wait();
		//						_walletsSemaphore.Wait();
		//						_documentsSemaphore.Wait();
		//						_dynamicFieldsSemaphore.Wait();
		//						_dynamicCategoriesSemaphore.Wait();

		//						IsOpen = false;

		//						LolloSQLiteConnectionPoolMT.Close();
		//					}
		//					catch (Exception ex)
		//					{
		//						if (SemaphoreSlimSafeRelease.IsAlive(_foldersSemaphore)
		//							&& SemaphoreSlimSafeRelease.IsAlive(_walletsSemaphore)
		//							&& SemaphoreSlimSafeRelease.IsAlive(_documentsSemaphore)
		//							&& SemaphoreSlimSafeRelease.IsAlive(_dynamicFieldsSemaphore)
		//							&& SemaphoreSlimSafeRelease.IsAlive(_dynamicCategoriesSemaphore))
		//							Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
		//					}
		//					finally
		//					{
		//						SemaphoreSlimSafeRelease.TryDispose(_dynamicCategoriesSemaphore);
		//						_dynamicCategoriesSemaphore = null;
		//						SemaphoreSlimSafeRelease.TryDispose(_dynamicFieldsSemaphore);
		//						_dynamicFieldsSemaphore = null;
		//						SemaphoreSlimSafeRelease.TryDispose(_documentsSemaphore);
		//						_documentsSemaphore = null;
		//						SemaphoreSlimSafeRelease.TryDispose(_walletsSemaphore);
		//						_walletsSemaphore = null;
		//						SemaphoreSlimSafeRelease.TryDispose(_foldersSemaphore);
		//						_foldersSemaphore = null;
		//					}
		//				}
		//			}).ConfigureAwait(false);
		//		}
		//		finally
		//		{
		//			SemaphoreSlimSafeRelease.TryDispose(_isOpenSemaphore);
		//			_isOpenSemaphore = null;
		//		}
		//	}
		//}
		#endregion open and close

		#region public methods
		internal bool UpdateDynamicFields(DynamicField newRecord)
		{
			bool result = false;
			try
			{
				result = Update<DynamicField>(newRecord, _dynamicFieldsSemaphore);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
				result = false;
			}
			return result;
		}

		internal bool UpdateDynamicCategories(DynamicCategory newRecord)
		{
			bool result = false;
			try
			{
				result = Update<DynamicCategory>(newRecord, _dynamicCategoriesSemaphore);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
				result = false;
			}
			return result;
		}

		internal bool UpdateDocuments(Document newRecord)
		{
			bool result = false;
			try
			{
				result = Update<Document>(newRecord, _documentsSemaphore);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
				result = false;
			}
			return result;
		}

		internal bool UpdateWallets(Wallet newRecord)
		{
			bool result = false;
			try
			{
				result = Update<Wallet>(newRecord, _walletsSemaphore);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
				result = false;
			}
			return result;
		}

		internal bool UpdateFolders(Folder newRecord)
		{
			bool result = false;
			try
			{
				result = Update<Folder>(newRecord, _foldersSemaphore);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
				result = false;
			}
			return result;
		}

		internal async Task<bool> InsertIntoDynamicFieldsAsync(DynamicField newFld, bool checkMaxEntries)
		{
			bool result = false;
			try
			{
				if (DynamicField.Check(newFld)) //  && await CheckUniqueKeyInDocumentsAsync(record).ConfigureAwait(false))
				{
					var fieldsAlreadyInFolder = await ReadRecordsWithParentIdAsync<DynamicField>(_dynamicFieldsSemaphore, nameof(DynamicCategory), newFld.ParentId).ConfigureAwait(false);
					if (!fieldsAlreadyInFolder.Any(ff => ff.FieldDescriptionId == newFld.FieldDescriptionId))
					{
						result = await InsertAsync<DynamicField>(newFld, checkMaxEntries, _dynamicFieldsSemaphore).ConfigureAwait(false);
					}
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}
		internal async Task<bool> InsertIntoDynamicFieldsAsync(IEnumerable<DynamicField> records, bool checkMaxEntries)
		{
			bool result = false;
			try
			{
				if (records.Count() > 0 && DynamicField.Check(records)) //  && await CheckUniqueKeyInDocumentsAsync(record).ConfigureAwait(false))
				{
					result = await InsertManyAsync<DynamicField>(records, checkMaxEntries, _dynamicFieldsSemaphore).ConfigureAwait(false);
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}
		internal async Task<bool> InsertIntoDynamicCategoriesAsync(DynamicCategory newCat, List<DynamicField> newFields, bool checkMaxEntries)
		{
			bool result = false;
			try
			{
				newFields.Clear();
				if (DynamicCategory.Check(newCat)) //  && await CheckUniqueKeyInDocumentsAsync(record).ConfigureAwait(false))
				{
					//var catsAlreadyInFolder = await ReadRecordsWithParentIdAsync<DynamicCategory>(_dynamicCategoriesSemaphore, nameof(DynamicCategory), newCat.ParentId).ConfigureAwait(false);
					//if (!catsAlreadyInFolder.Any(ca => ca.CategoryId == newCat.CategoryId))
					//{
					// LOLLO TODO I have commented out the lines above coz the DB already checks if the key is unique. It should be OK but check it.
					result = await InsertAsync<DynamicCategory>(newCat, checkMaxEntries, _dynamicCategoriesSemaphore).ConfigureAwait(false);
					if (result)
					{
						// add the fields belonging to the new category, without duplicating existing fields (categories may share fields)
						var fieldDescriptionIdsAlreadyInFolder = new List<string>();

						var fieldsInFolder = await ReadRecordsWithParentIdAsync<DynamicField>(_dynamicFieldsSemaphore, nameof(DynamicField), newCat.ParentId).ConfigureAwait(false);
						foreach (var fieldInFolder in fieldsInFolder)
						{
							if (fieldInFolder?.FieldDescriptionId != null
								&& !fieldDescriptionIdsAlreadyInFolder.Contains(fieldInFolder.FieldDescriptionId))
								fieldDescriptionIdsAlreadyInFolder.Add(fieldInFolder.FieldDescriptionId);
						}

						foreach (var fieldDescriptionId in newCat.Category.FieldDescriptionIds)
						{
							if (fieldDescriptionId != null
								&& !fieldDescriptionIdsAlreadyInFolder.Contains(fieldDescriptionId)) // do not duplicate existing fields, since different categories may have fields in common
							{
								var dynamicField = new DynamicField(this) { FieldDescriptionId = fieldDescriptionId, ParentId = newCat.ParentId };
								if (await InsertAsync<DynamicField>(dynamicField, checkMaxEntries, _dynamicFieldsSemaphore).ConfigureAwait(false))
								{
									newFields.Add(dynamicField);
								}
							}
						}
					}
					//}
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}
		internal async Task<bool> InsertIntoDynamicCategoriesAsync(IEnumerable<DynamicCategory> records, bool checkMaxEntries)
		{
			bool result = false;
			try
			{
				if (records.Count() > 0 && DynamicCategory.Check(records)) //  && await CheckUniqueKeyInDocumentsAsync(record).ConfigureAwait(false))
				{
					result = await InsertManyAsync<DynamicCategory>(records, checkMaxEntries, _dynamicCategoriesSemaphore).ConfigureAwait(false);
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}
		internal async Task<bool> DeleteFromDynamicCategoriesAsync(DynamicCategory cat, List<string> deletedFieldDescriptionIds)
		{
			if (cat == null) return true;
			bool result = false;
			try
			{
				deletedFieldDescriptionIds.Clear();
				result = await DeleteAsync<DynamicCategory>(cat.Id, _dynamicCategoriesSemaphore).ConfigureAwait(false);
				// delete the dynamic fields owned by this category unless they are owned by another category
				if (result)
				{
					var otherAvailableCategories = await ReadRecordsWithParentIdAsync<DynamicCategory>(_dynamicCategoriesSemaphore, nameof(DynamicCategory), cat.ParentId).ConfigureAwait(false);

					List<string> otherFieldDescrIds = new List<string>();
					foreach (var otherCat in otherAvailableCategories)
					{
						if (otherCat?.Category?.FieldDescriptionIds != null)
						{
							foreach (var fieldDescrId in otherCat.Category.FieldDescriptionIds)
							{
								if (fieldDescrId != null
									&& !otherFieldDescrIds.Contains(fieldDescrId))
									otherFieldDescrIds.Add(fieldDescrId);
							}
						}
					}

					var dynamicFieldsInCurrentFolder = await ReadRecordsWithParentIdAsync<DynamicField>(_dynamicFieldsSemaphore, nameof(DynamicField), cat.ParentId).ConfigureAwait(false);
					foreach (var fieldToDelete in dynamicFieldsInCurrentFolder.Where(a => a?.FieldDescriptionId != null && !otherFieldDescrIds.Contains(a.FieldDescriptionId)))
					{
						if (await DeleteAsync<DynamicField>(fieldToDelete.Id, _dynamicFieldsSemaphore).ConfigureAwait(false))
						{
							deletedFieldDescriptionIds.Add(fieldToDelete.FieldDescriptionId);
						}
					}
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}
		internal async Task<bool> DeleteFromDynamicFieldsAsync(DynamicField fld)
		{
			if (fld == null) return true;
			bool result = false;
			try
			{
				result = await DeleteAsync<DynamicField>(fld.Id, _dynamicFieldsSemaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}

		internal async Task<bool> InsertIntoDocumentsAsync(Document record, bool checkMaxEntries)
		{
			bool result = false;
			try
			{
				if (Document.Check(record)) //  && await CheckUniqueKeyInDocumentsAsync(record).ConfigureAwait(false))
				{
					result = await InsertAsync<Document>(record, checkMaxEntries, _documentsSemaphore).ConfigureAwait(false);
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}
		internal async Task<bool> InsertIntoDocumentsAsync(IEnumerable<Document> records, bool checkMaxEntries)
		{
			bool result = false;
			try
			{
				if (records.Count() > 0 && Document.Check(records)) //  && await CheckUniqueKeyInDocumentsAsync(record).ConfigureAwait(false))
				{
					result = await InsertManyAsync<Document>(records, checkMaxEntries, _documentsSemaphore).ConfigureAwait(false);
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}
		internal async Task<bool> InsertIntoWalletsAsync(Wallet record, bool checkMaxEntries)
		{
			bool result = false;
			try
			{
				if (Wallet.Check(record)) // && await CheckUniqueKeyInWalletsAsync(record).ConfigureAwait(false))
				{
					result = await InsertAsync<Wallet>(record, checkMaxEntries, _walletsSemaphore).ConfigureAwait(false);
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}
		internal async Task<bool> InsertIntoWalletsAsync(IEnumerable<Wallet> records, bool checkMaxEntries)
		{
			bool result = false;
			try
			{
				if (records.Count() > 0 && Wallet.Check(records)) // && await CheckUniqueKeyInWalletsAsync(record).ConfigureAwait(false))
				{
					result = await InsertManyAsync<Wallet>(records, checkMaxEntries, _walletsSemaphore).ConfigureAwait(false);
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}

		internal async Task<bool> InsertIntoFoldersAsync(Folder record, bool checkMaxEntries)
		{
			bool result = false;
			try
			{
				if (Folder.Check(record)) // && await CheckForeignKey_TagsInFolderAsync(record).ConfigureAwait(false)) // && await CheckUniqueKeyInEntriesAsync(record).ConfigureAwait(false))
				{
					result = await InsertAsync<Folder>(record, checkMaxEntries, _foldersSemaphore).ConfigureAwait(false);
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}

		internal async Task<List<DynamicCategory>> GetDynamicCategoriesAsync(string parentId)
		{
			List<DynamicCategory> dynCats = new List<DynamicCategory>();
			try
			{
				dynCats = await ReadRecordsWithParentIdAsync<DynamicCategory>
					(_dynamicCategoriesSemaphore, nameof(DynamicCategory), parentId)
					.ConfigureAwait(false);
				foreach (var dynCat in dynCats)
				{
					dynCat.DBManager = this;
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return dynCats;
		}
		//internal async Task<List<DynamicCategory>> GetDynamicCategoriesAsync()
		//{
		//	List<DynamicCategory> dynCats = new List<DynamicCategory>();
		//	try
		//	{
		//		dynCats = await LolloSQLiteConnectionMT.ReadTableAsync<DynamicCategory>
		//			(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _dynamicCategoriesSemaphore)
		//			.ConfigureAwait(false);
		//	}
		//	catch (Exception exc)
		//	{
		//		Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
		//	}
		//	return dynCats;
		//}
		internal async Task<List<DynamicCategory>> GetDynamicCategoriesByCatIdAsync(string catId)
		{
			var dynCats = new List<DynamicCategory>();
			try
			{
				dynCats = await ReadRecordsWithParentIdAsync<DynamicCategory>
					(_dynamicCategoriesSemaphore, nameof(DynamicCategory), catId, "CategoryId")
					.ConfigureAwait(false);
				foreach (var dynCat in dynCats)
				{
					dynCat.DBManager = this;
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return dynCats;
		}
		internal async Task<List<DynamicField>> GetDynamicFieldsByFldDscIdAsync(string fldDscId)
		{
			var dynFlds = new List<DynamicField>();
			try
			{
				dynFlds = await ReadRecordsWithParentIdAsync<DynamicField>
					(_dynamicFieldsSemaphore, nameof(DynamicField), fldDscId, "FieldDescriptionId")
					.ConfigureAwait(false);
				foreach (var dynFld in dynFlds)
				{
					dynFld.DBManager = this;
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return dynFlds;
		}
		internal async Task<List<DynamicField>> GetDynamicFieldsAsync(string parentId)
		{
			var dynFlds = new List<DynamicField>();
			try
			{
				dynFlds = await ReadRecordsWithParentIdAsync<DynamicField>
					(_dynamicFieldsSemaphore, nameof(DynamicField), parentId)
					.ConfigureAwait(false);
				foreach (var dynFld in dynFlds)
				{
					dynFld.DBManager = this;
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return dynFlds;
		}

		internal async Task<List<Document>> GetDocumentsAsync(string parentId)
		{
			var docs = new List<Document>();
			try
			{
				docs = await ReadRecordsWithParentIdAsync<Document>(_documentsSemaphore, nameof(Document), parentId).ConfigureAwait(false);
				foreach (var doc in docs)
				{
					doc.DBManager = this;
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return docs;
		}
		internal async Task<List<Document>> GetDocumentsAsync()
		{
			var docs = new List<Document>();
			try
			{
				docs = await ReadTableAsync<Document>(_documentsSemaphore).ConfigureAwait(false);
				foreach (var doc in docs)
				{
					doc.DBManager = this;
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return docs;
		}
		internal async Task<List<Wallet>> GetWalletsAsync(string parentId)
		{
			var wallets = new List<Wallet>();
			try
			{
				wallets = await ReadRecordsWithParentIdAsync<Wallet>
					(_walletsSemaphore, nameof(Wallet), parentId).ConfigureAwait(false);
				foreach (var wal in wallets)
				{
					wal.DBManager = this;
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return wallets;
		}
		internal async Task<List<Wallet>> GetWalletsAsync()
		{
			var wallets = new List<Wallet>();
			try
			{
				wallets = await ReadTableAsync<Wallet>(_walletsSemaphore).ConfigureAwait(false);
				foreach (var wal in wallets)
				{
					wal.DBManager = this;
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return wallets;
		}
		internal async Task<List<Folder>> GetFoldersAsync()
		{
			var folders = new List<Folder>();
			try
			{
				folders = await ReadTableAsync<Folder>(_foldersSemaphore).ConfigureAwait(false);
				foreach (var fol in folders)
				{
					fol.DBManager = this;
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return folders;
		}

		internal async Task<bool> DeleteFromDocumentsAsync(Document doc)
		{
			if (doc == null) return true;
			bool result = false;
			try
			{
				result = await DeleteAsync<Document>(doc.Id, _documentsSemaphore).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}

		internal async Task<bool> DeleteFromWalletsAsync(Wallet wallet)
		{
			if (wallet == null) return true;
			bool result = false;
			try
			{
				result = await DeleteAsync<Wallet>(wallet.Id, _walletsSemaphore).ConfigureAwait(false);
				await DeleteRecordsWithParentIdAsync<Document>(_documentsSemaphore, nameof(Document), wallet.Id).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}
		internal async Task<bool> DeleteFromFoldersAsync(Folder folder)
		{
			if (folder == null) return true;
			bool result = false;
			try
			{
				result = await DeleteAsync<Folder>(folder.Id, _foldersSemaphore).ConfigureAwait(false);
				//if (result)
				//{
				var wallets = await ReadRecordsWithParentIdAsync<Wallet>(_walletsSemaphore, nameof(Wallet), folder.Id).ConfigureAwait(false);

				if (await DeleteRecordsWithParentIdAsync<Wallet>(_documentsSemaphore, nameof(Wallet), folder.Id).ConfigureAwait(false))
				{
					foreach (var wallet in wallets.Distinct())
					{
						await DeleteRecordsWithParentIdAsync<Document>(_documentsSemaphore, nameof(Document), wallet.Id).ConfigureAwait(false);
					}
				}
				await DeleteRecordsWithParentIdAsync<DynamicCategory>(_dynamicCategoriesSemaphore, nameof(DynamicCategory), folder.Id).ConfigureAwait(false);
				await DeleteRecordsWithParentIdAsync<DynamicField>(_dynamicFieldsSemaphore, nameof(DynamicField), folder.Id).ConfigureAwait(false);
				//}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}
		#endregion public methods


		#region private methods
		private Task<List<T>> ReadTableAsync<T>(SemaphoreSlimSafeRelease SemaphoreSlimSafeRelease) where T : new()
		{
			return Task.Run<List<T>>(() =>
			//                return Task.Factory.StartNew<List<T>>(() => // Task.Run is newer and shorter than Task.Factory.StartNew . It also has some different default settings in certain overloads.
			{
				return ReadTable<T>(SemaphoreSlimSafeRelease);
			});
		}
		private List<T> ReadTable<T>(SemaphoreSlimSafeRelease semaphore) where T : new()
		{
			if (!_isOpen) return null;

			List<T> result = null;
			try
			{
				semaphore.Wait();
				if (_isOpen)
				{
					var connectionString = new SQLiteConnectionString(_dbFullPath, _isStoreDateTimeAsTicks);
					var conn = _connectionPool.GetConnection(connectionString, _openFlags);
					try
					{
						int aResult = conn.CreateTable(typeof(T));
						var query = conn.Table<T>();
						result = query.ToList<T>();
					}
					finally
					{
						_connectionPool.ResetConnection(connectionString.ConnectionString);
					}
				}
			}
			catch (Exception ex)
			{
				if (SemaphoreSlimSafeRelease.IsAlive(semaphore))
				{
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
					throw;
				}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(semaphore);
			}
			return result;
		}

		private Task<bool> DeleteRecordsWithParentIdAsync<T>(SemaphoreSlimSafeRelease SemaphoreSlimSafeRelease, string tableName, string parentId) where T : new()
		{
			return Task.Run<bool>(() =>
			//                return Task.Factory.StartNew<List<T>>(() => // Task.Run is newer and shorter than Task.Factory.StartNew . It also has some different default settings in certain overloads.
			{
				return DeleteRecordsWithParentId<T>(SemaphoreSlimSafeRelease, tableName, parentId);
			});
		}
		private bool DeleteRecordsWithParentId<T>(SemaphoreSlimSafeRelease semaphore, string tableName, string parentId, string parentIdFieldName = nameof(DbBoundObservableData.ParentId)) where T : new()
		{
			if (!_isOpen) return false;

			bool result = false;
			try
			{
				semaphore.Wait();
				if (_isOpen)
				{
					var connectionString = new SQLiteConnectionString(_dbFullPath, _isStoreDateTimeAsTicks);
					var conn = _connectionPool.GetConnection(connectionString, _openFlags);
					try
					{
						int aResult = conn.CreateTable(typeof(T));
						string delQueryString = string.Format("DELETE FROM {0} WHERE ParentId = '{1}'", tableName, parentId);
						int queryResult = conn.Execute(delQueryString);
						result = queryResult > 0;

						if (!result)
						{
							string readQueryString = string.Format("SELECT * FROM {0} WHERE " + parentIdFieldName + " = '{1}'", tableName, parentId);
							var query = conn.Query<T>(delQueryString);
							result = query.Count <= 0;
						}
					}
					finally
					{
						_connectionPool.ResetConnection(connectionString.ConnectionString);
					}
				}
			}
			catch (Exception ex)
			{
				if (SemaphoreSlimSafeRelease.IsAlive(semaphore))
				{
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
					throw;
				}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(semaphore);
			}
			return result;
		}
		private Task<List<T>> ReadRecordsWithParentIdAsync<T>(SemaphoreSlimSafeRelease SemaphoreSlimSafeRelease, string tableName, string parentId, string parentIdFieldName = nameof(DbBoundObservableData.ParentId)) where T : new()
		{
			return Task.Run<List<T>>(() =>
			//                return Task.Factory.StartNew<List<T>>(() => // Task.Run is newer and shorter than Task.Factory.StartNew . It also has some different default settings in certain overloads.
			{
				return ReadRecordsWithParentId<T>(SemaphoreSlimSafeRelease, tableName, parentId, parentIdFieldName);
			});
		}
		private List<T> ReadRecordsWithParentId<T>(SemaphoreSlimSafeRelease semaphore, string tableName, string parentId, string parentIdFieldName = nameof(DbBoundObservableData.ParentId)) where T : new()
		{
			if (!_isOpen) return null;

			List<T> result = null;
			try
			{
				semaphore.Wait();
				if (_isOpen)
				{
					var connectionString = new SQLiteConnectionString(_dbFullPath, _isStoreDateTimeAsTicks);
					var conn = _connectionPool.GetConnection(connectionString, _openFlags);
					try
					{
						int aResult = conn.CreateTable(typeof(T));
						string queryString = string.Format("SELECT * FROM {0} WHERE " + parentIdFieldName + " = '{1}'", tableName, parentId);
						var query = conn.Query<T>(queryString);
						result = query.ToList<T>();
					}
					finally
					{
						_connectionPool.ResetConnection(connectionString.ConnectionString);
					}
				}
			}
			catch (Exception ex)
			{
				if (SemaphoreSlimSafeRelease.IsAlive(semaphore))
				{
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
					throw;
				}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(semaphore);
			}
			return result;
		}
		//private Task<T> ReadRecordByIdAsync<T>(SemaphoreSlimSafeRelease SemaphoreSlimSafeRelease, object primaryKey) where T : new()
		//{
		//	return Task.Run<T>(() =>
		//	//                return Task.Factory.StartNew<List<T>>(() => // Task.Run is newer and shorter than Task.Factory.StartNew . It also has some different default settings in certain overloads.
		//	{
		//		return ReadRecordById<T>(SemaphoreSlimSafeRelease, primaryKey);
		//	});
		//}
		//private T ReadRecordById<T>(SemaphoreSlimSafeRelease semaphore, object primaryKey) where T : new()
		//{
		//	if (!_isOpen) return default(T);

		//	T result = default(T);
		//	try
		//	{
		//		semaphore.Wait();
		//		if (_isOpen)
		//		{
		//			var connectionString = new SQLiteConnectionString(_dbFullPath, _isStoreDateTimeAsTicks);
		//			var conn = _connectionPool.GetConnection(connectionString, _openFlags);
		//			try
		//			{
		//				int aResult = conn.CreateTable(typeof(T));
		//				var query = conn.Get<T>(primaryKey);
		//				result = query;
		//			}
		//			finally
		//			{
		//				_connectionPool.ResetConnection(connectionString.ConnectionString);
		//			}
		//		}
		//	}
		//	catch (Exception ex)
		//	{
		//		if (SemaphoreSlimSafeRelease.IsAlive(semaphore))
		//		{
		//			Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
		//			throw;
		//		}
		//	}
		//	finally
		//	{
		//		SemaphoreSlimSafeRelease.TryRelease(semaphore);
		//	}
		//	return result;
		//}
		private Task<bool> DeleteAllAsync<T>(SemaphoreSlimSafeRelease semaphore)
		{
			return Task.Run(() =>
			{
				return DeleteAll<T>(semaphore);
			});
		}
		private bool DeleteAll<T>(SemaphoreSlimSafeRelease semaphore)
		{
			if (!_isOpen) return false;
			bool result = false;

			try
			{
				semaphore.Wait();
				if (_isOpen)
				{
					var connectionString = new SQLiteConnectionString(_dbFullPath, _isStoreDateTimeAsTicks);
					var conn = _connectionPool.GetConnection(connectionString, _openFlags);
					try
					{
						int aResult = conn.CreateTable(typeof(T));
						int deleteResult = conn.DeleteAll<T>();
						result = true;
					}
					finally
					{
						_connectionPool.ResetConnection(connectionString.ConnectionString);
					}
				}
			}
			catch (Exception ex)
			{
				if (SemaphoreSlimSafeRelease.IsAlive(semaphore))
				{
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
					throw;
				}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(semaphore);
			}
			return result;
		}
		private Task<bool> InsertAsync<T>(object item, bool checkMaxEntries, SemaphoreSlimSafeRelease semaphore) where T : new()
		{
			return Task.Run(() =>
			{
				return Insert<T>(item, checkMaxEntries, semaphore);
			});
		}
		private bool Insert<T>(object item, bool checkMaxEntries, SemaphoreSlimSafeRelease semaphore) where T : new()
		{
			if (!_isOpen) return false;
			bool result = false;
			try
			{
				semaphore.Wait();
				if (_isOpen)
				{
					//bool isTesting = true;
					//if (isTesting)
					//{
					//    for (long i = 0; i < 10000000; i++) //wait a few seconds, for testing
					//    {
					//        string aaa = i.ToString();
					//    }
					//}

					object item_mt = item;
					var connectionString = new SQLiteConnectionString(_dbFullPath, _isStoreDateTimeAsTicks);
					var conn = _connectionPool.GetConnection(connectionString, _openFlags);
					try
					{
						int aResult = conn.CreateTable(typeof(T));
						int insertResult = 0;
						//if (checkMaxEntries)
						//{
						//	var query = conn.Table<T>();
						//	var count = query.Count();
						//	insertResult = conn.Insert(item_mt);
						//}
						//else
						//{
						insertResult = conn.Insert(item_mt);
						//}
						result = insertResult > 0;
					}
					catch (NotNullConstraintViolationException ex0)
					{
						result = false;
					}
					finally
					{
						_connectionPool.ResetConnection(connectionString.ConnectionString);
					}
				}
			}
			catch (Exception ex1)
			{
				if (SemaphoreSlimSafeRelease.IsAlive(semaphore))
				{
					Logger.Add_TPL(ex1.ToString(), Logger.PersistentDataLogFilename);
					throw;
				}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(semaphore);
			}
			return result;
		}

		private Task<bool> InsertManyAsync<T>(IEnumerable<T> items, bool checkMaxEntries, SemaphoreSlimSafeRelease semaphore) where T : new()
		{
			return Task.Run(() =>
			{
				return InsertMany<T>(items, checkMaxEntries, semaphore);
			});
		}
		private bool InsertMany<T>(IEnumerable<T> items, bool checkMaxEntries, SemaphoreSlimSafeRelease semaphore) where T : new()
		{
			if (!_isOpen) return false;
			bool result = false;
			try
			{
				semaphore.Wait();
				if (_isOpen)
				{
					//bool isTesting = true;
					//if (isTesting)
					//{
					//    for (long i = 0; i < 10000000; i++) //wait a few seconds, for testing
					//    {
					//        string aaa = i.ToString();
					//    }
					//}

					var connectionString = new SQLiteConnectionString(_dbFullPath, _isStoreDateTimeAsTicks);
					var conn = _connectionPool.GetConnection(connectionString, _openFlags);
					try
					{
						int aResult = conn.CreateTable(typeof(T));
						int insertResult = 0;
						//if (checkMaxEntries)
						//{
						//	var query = conn.Table<T>();
						//	var count = query.Count();
						//	insertResult = conn.InsertAll(items);
						//}
						//else
						//{
						insertResult = conn.InsertAll(items);
						//}
						result = insertResult > 0;
					}
					catch (NotNullConstraintViolationException ex0)
					{
						result = false;
					}
					finally
					{
						_connectionPool.ResetConnection(connectionString.ConnectionString);
					}
				}
			}
			catch (Exception ex)
			{
				if (SemaphoreSlimSafeRelease.IsAlive(semaphore))
				{
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
					throw;
				}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(semaphore);
			}
			return result;
		}
		private Task<bool> DeleteAsync<T>(object primaryKey, SemaphoreSlimSafeRelease semaphore) where T : new()
		{
			return Task.Run(() =>
			{
				return Delete<T>(primaryKey, semaphore);
			});
		}
		private bool Delete<T>(object primaryKey, SemaphoreSlimSafeRelease semaphore) where T : new()
		{
			if (!_isOpen) return false;

			bool result = false;
			try
			{
				object pk_mt = primaryKey;

				semaphore.Wait();
				if (_isOpen)
				{
					//bool isTesting = true;
					//if (isTesting)
					//{
					//    for (long i = 0; i < 10000000; i++) //wait a few seconds, for testing
					//    {
					//        string aaa = i.ToString();
					//    }
					//}

					var connectionString = new SQLiteConnectionString(_dbFullPath, _isStoreDateTimeAsTicks);
					var conn = _connectionPool.GetConnection(connectionString, _openFlags);
					try
					{
						int aResult = conn.CreateTable(typeof(T));
						int deleteResult = conn.Delete<T>(pk_mt);
						result = (deleteResult > 0);
						if (!result && conn.Get<T>(pk_mt) == null) result = true;
					}
					finally
					{
						_connectionPool.ResetConnection(connectionString.ConnectionString);
					}
				}
			}
			catch (Exception ex)
			{
				if (SemaphoreSlimSafeRelease.IsAlive(semaphore))
				{
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
					throw;
				}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(semaphore);
			}
			return result;
		}
		//private Task<bool> UpdateAsync<T>(object item, SemaphoreSlimSafeRelease semaphore) where T : new()
		//{
		//	return Task.Run(() =>
		//	{
		//		return Update<T>(item, semaphore);
		//	});
		//}
		private bool Update<T>(object item, SemaphoreSlimSafeRelease semaphore) where T : new()
		{
			if (!_isOpen) return false;

			bool result = false;
			int updateResult = 0;

			try
			{
				semaphore.Wait();
				if (_isOpen)
				{
					var connectionString = new SQLiteConnectionString(_dbFullPath, _isStoreDateTimeAsTicks);
					var conn = _connectionPool.GetConnection(connectionString, _openFlags);
					try
					{
						int aResult = conn.CreateTable(typeof(T));
						{
							updateResult = conn.Update(item);
							result = true;  //(updateResult > 0);
						}
					}
					finally
					{
						_connectionPool.ResetConnection(connectionString.ConnectionString);
					}
				}
			}
			catch (Exception ex)
			{
				if (SemaphoreSlimSafeRelease.IsAlive(semaphore))
				{
					Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
					throw;
				}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(semaphore);
				//if (updateResult < 1)
				//{
				//	Debugger.Break();
				//	List<T> test = ReadTable<T>(dbPath, openFlags, storeDateTimeAsTicks, semaphore);

				//	T itemTyped = (T)item;

				//	bool check = test.Contains(itemTyped);
				//}

			}
			return result;
		}
		#endregion private methods


		private class LolloSQLiteConnectionPoolMT : OpenableObservableData
		{
			private class LolloConnection
			{
				public SQLiteConnectionString ConnectionString { get; private set; }
				public SQLiteConnection Connection { get; private set; }

				public LolloConnection(SQLiteConnectionString connectionString, SQLiteOpenFlags openFlags)
				{
					ConnectionString = connectionString;
					Connection = new SQLiteConnection(connectionString.DatabasePath, openFlags, connectionString.StoreDateTimeAsTicks);
				}

				public void Reset()
				{
					Connection?.Dispose();
					Connection = null;
				}
			}

			private readonly Dictionary<string, LolloConnection> _connectionsDict = new Dictionary<string, LolloConnection>();
			private SemaphoreSlimSafeRelease _connectionsDictSemaphore = null;

			internal SQLiteConnection GetConnection(SQLiteConnectionString connectionString, SQLiteOpenFlags openFlags)
			{
				LolloConnection conn = null;
				try
				{
					_connectionsDictSemaphore.Wait();
					string key = connectionString.ConnectionString;

					if (!_connectionsDict.TryGetValue(key, out conn))
					{
						conn = new LolloConnection(connectionString, openFlags);
						_connectionsDict[key] = conn;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_connectionsDictSemaphore))
					{
						Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
						throw;
					}
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_connectionsDictSemaphore);
				}
				if (conn != null) return conn.Connection;
				return null;
			}
			/// <summary>
			/// Closes a given connection managed by this pool. 
			/// </summary>
			internal void ResetConnection(string connectionString)
			{
				if (connectionString == null) return;

				LolloConnection conn = null;
				try
				{
					_connectionsDictSemaphore.Wait();

					if (_connectionsDict.TryGetValue(connectionString, out conn))
					{
						conn.Reset();
						_connectionsDict.Remove(connectionString);
					}
				}
				catch (Exception ex0)
				{
					// LOLLO TODO sometimes, I get "unable to close due to unfinalized statements or unfinished backups"
					// I now use close_v2 instead of close, and it looks better.
					try
					{
						Task.Delay(conn.Connection.BusyTimeout.Milliseconds * 3).Wait();
						if (_connectionsDict.TryGetValue(connectionString, out conn))
						{
							conn.Reset();
							_connectionsDict.Remove(connectionString);
						}
					}
					catch (Exception ex1)
					{
						if (SemaphoreSlimSafeRelease.IsAlive(_connectionsDictSemaphore))
						{
							Logger.Add_TPL(ex0.ToString(), Logger.ForegroundLogFilename);
							Logger.Add_TPL(ex1.ToString(), Logger.ForegroundLogFilename);
						}
					}
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_connectionsDictSemaphore);
				}
			}

			protected override Task OpenMayOverrideAsync()
			{
				if (!SemaphoreSlimSafeRelease.IsAlive(_connectionsDictSemaphore)) _connectionsDictSemaphore = new SemaphoreSlimSafeRelease(1, 1);
				return Task.CompletedTask;
			}
			/// <summary>
			/// Call this method when the application is resumed.
			/// </summary>
			//internal void Open()
			//{
			//	// I don't need a semaphore for this Open / Close pair coz it is managed by the owner class DBManager
			//	if (!SemaphoreSlimSafeRelease.IsAlive(_connectionsDictSemaphore)) _connectionsDictSemaphore = new SemaphoreSlimSafeRelease(1, 1);
			//}
			protected override Task CloseMayOverrideAsync()
			{
				try
				{
					_connectionsDictSemaphore.Wait();
					foreach (var conn in _connectionsDict.Values)
					{
						conn.Reset();
					}
					_connectionsDict.Clear();
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_connectionsDictSemaphore))
						Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryDispose(_connectionsDictSemaphore);
					_connectionsDictSemaphore = null;
				}
				return Task.CompletedTask;
			}
			/// <summary>
			/// Closes all connections managed by this pool.
			/// </summary>
			//internal void Close()
			//{
			//	// I don't need a semaphore for this Open / Close pair coz it is managed by the owner class DBManager
			//	try
			//	{
			//		_connectionsDictSemaphore.Wait();
			//		foreach (var conn in _connectionsDict.Values)
			//		{
			//			conn.Reset();
			//		}
			//		_connectionsDict.Clear();
			//	}
			//	catch (Exception ex)
			//	{
			//		if (SemaphoreSlimSafeRelease.IsAlive(_connectionsDictSemaphore))
			//			Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			//	}
			//	finally
			//	{
			//		SemaphoreSlimSafeRelease.TryDispose(_connectionsDictSemaphore);
			//		_connectionsDictSemaphore = null;
			//	}
			//}
		}
	}
}
