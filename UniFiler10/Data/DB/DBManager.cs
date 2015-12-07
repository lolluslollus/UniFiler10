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
		#region enums
		private enum InsertResult { NothingDone, AlreadyThere, Added, SomeAdded, SomeAlreadyThere };
		//private enum DeleteResult { NothingDone, AlreadyMissing, Deleted };
		#endregion enums

		#region fields
		// one db for all tables
		// one semaphore each table
		private const string DB_FILE_NAME = "Db.db";
		private string _dbFullPath = string.Empty;
		private StorageFolder _directory = null;
		internal StorageFolder Directory { get { return _directory; } }

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
		internal DBManager(StorageFolder directory, bool isReadOnly)
		{
			if (directory != null)
			{
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
			else throw new ArgumentNullException("DBManager ctor: directory cannot be null or empty");
		}
		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_connectionPool?.Dispose();
			_connectionPool = null;
		}
		#endregion construct and dispose

		#region open and close
		protected override async Task OpenMayOverrideAsync()
		{
			if (!SemaphoreSlimSafeRelease.IsAlive(_foldersSemaphore)) _foldersSemaphore = new SemaphoreSlimSafeRelease(1, 1);
			if (!SemaphoreSlimSafeRelease.IsAlive(_walletsSemaphore)) _walletsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
			if (!SemaphoreSlimSafeRelease.IsAlive(_documentsSemaphore)) _documentsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
			if (!SemaphoreSlimSafeRelease.IsAlive(_dynamicFieldsSemaphore)) _dynamicFieldsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
			if (!SemaphoreSlimSafeRelease.IsAlive(_dynamicCategoriesSemaphore)) _dynamicCategoriesSemaphore = new SemaphoreSlimSafeRelease(1, 1);

			await _connectionPool.OpenAsync().ConfigureAwait(false);
		}
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
					if (!fieldsAlreadyInFolder.Any(fld => fld.FieldDescriptionId == newFld.FieldDescriptionId))
					{
						var dbResult = await InsertAsync<DynamicField>(newFld, checkMaxEntries, _dynamicFieldsSemaphore).ConfigureAwait(false);
						if (dbResult == InsertResult.AlreadyThere || dbResult == InsertResult.Added) result = true;
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
					var dbResult = await InsertManyAsync<DynamicField>(records, checkMaxEntries, _dynamicFieldsSemaphore).ConfigureAwait(false);
					if (dbResult == InsertResult.AlreadyThere || dbResult == InsertResult.Added) result = true;
				}
			}
			catch (Exception exc)
			{
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}
		internal async Task<bool> InsertIntoDynamicCategoriesAsync(DynamicCategory newCat, List<DynamicField> newDynFlds, bool checkMaxEntries)
		{
			bool result = false;
			try
			{
				newDynFlds.Clear();
				if (DynamicCategory.Check(newCat)) //  && await CheckUniqueKeyInDocumentsAsync(record).ConfigureAwait(false))
				{
					var catsAlreadyInFolder = await ReadRecordsWithParentIdAsync<DynamicCategory>(_dynamicCategoriesSemaphore, nameof(DynamicCategory), newCat.ParentId).ConfigureAwait(false);
					if (!catsAlreadyInFolder.Any(ca => ca.CategoryId == newCat.CategoryId))
					{
						var dbResult = await InsertAsync<DynamicCategory>(newCat, checkMaxEntries, _dynamicCategoriesSemaphore).ConfigureAwait(false);
						if (dbResult == InsertResult.AlreadyThere || dbResult == InsertResult.Added) result = true;
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
									var dbResult2 = await InsertAsync<DynamicField>(dynamicField, checkMaxEntries, _dynamicFieldsSemaphore).ConfigureAwait(false);
									if (dbResult2 == InsertResult.AlreadyThere || dbResult2 == InsertResult.Added)
									{
										newDynFlds.Add(dynamicField);
									}
								}
							}
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
		internal async Task<bool> InsertIntoDynamicCategoriesAsync(IEnumerable<DynamicCategory> records, bool checkMaxEntries)
		{
			bool result = false;
			try
			{
				if (records.Count() > 0 && DynamicCategory.Check(records)) //  && await CheckUniqueKeyInDocumentsAsync(record).ConfigureAwait(false))
				{
					var dbResult = await InsertManyAsync<DynamicCategory>(records, checkMaxEntries, _dynamicCategoriesSemaphore).ConfigureAwait(false);
					if (dbResult == InsertResult.AlreadyThere || dbResult == InsertResult.Added) result = true;
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
					var otherCatsInFolder = await ReadRecordsWithParentIdAsync<DynamicCategory>(_dynamicCategoriesSemaphore, nameof(DynamicCategory), cat.ParentId).ConfigureAwait(false);

					List<string> otherFieldDescrIds = new List<string>();
					foreach (var otherCat in otherCatsInFolder)
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

					var dynFldsInFolder = await ReadRecordsWithParentIdAsync<DynamicField>(_dynamicFieldsSemaphore, nameof(DynamicField), cat.ParentId).ConfigureAwait(false);
					foreach (var fieldToDelete in dynFldsInFolder.Where(dynFld => dynFld?.FieldDescriptionId != null && !otherFieldDescrIds.Contains(dynFld.FieldDescriptionId)))
					{
						bool isDynFldsDeleted = await DeleteAsync<DynamicField>(fieldToDelete.Id, _dynamicFieldsSemaphore).ConfigureAwait(false);
						if (isDynFldsDeleted)
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
					var dbResult = await InsertAsync<Document>(record, checkMaxEntries, _documentsSemaphore).ConfigureAwait(false);
					if (dbResult == InsertResult.AlreadyThere || dbResult == InsertResult.Added) result = true;
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
					var dbResult = await InsertManyAsync<Document>(records, checkMaxEntries, _documentsSemaphore).ConfigureAwait(false);
					if (dbResult == InsertResult.AlreadyThere || dbResult == InsertResult.Added) result = true;
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
					var dbResult = await InsertAsync<Wallet>(record, checkMaxEntries, _walletsSemaphore).ConfigureAwait(false);
					if (dbResult == InsertResult.AlreadyThere || dbResult == InsertResult.Added) result = true;
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
					var dbResult = await InsertManyAsync<Wallet>(records, checkMaxEntries, _walletsSemaphore).ConfigureAwait(false);
					if (dbResult == InsertResult.AlreadyThere || dbResult == InsertResult.Added) result = true;
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
					var dbResult = await InsertAsync<Folder>(record, checkMaxEntries, _foldersSemaphore).ConfigureAwait(false);
					if (dbResult == InsertResult.AlreadyThere || dbResult == InsertResult.Added) result = true;
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
				if (result)
				{
					var wallets = await ReadRecordsWithParentIdAsync<Wallet>(_walletsSemaphore, nameof(Wallet), folder.Id).ConfigureAwait(false);

					bool isWalsDeleted = await DeleteRecordsWithParentIdAsync<Wallet>(_documentsSemaphore, nameof(Wallet), folder.Id).ConfigureAwait(false);
					if (isWalsDeleted)
					{
						foreach (var wallet in wallets.Distinct())
						{
							await DeleteRecordsWithParentIdAsync<Document>(_documentsSemaphore, nameof(Document), wallet.Id).ConfigureAwait(false);
						}
					}
					await DeleteRecordsWithParentIdAsync<DynamicCategory>(_dynamicCategoriesSemaphore, nameof(DynamicCategory), folder.Id).ConfigureAwait(false);
					await DeleteRecordsWithParentIdAsync<DynamicField>(_dynamicFieldsSemaphore, nameof(DynamicField), folder.Id).ConfigureAwait(false);
				}
			}
			catch (Exception exc)
			{
				Debugger.Break();
				Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
			}
			return result;
		}
		#endregion public methods

		#region private methods
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
						if (deleteResult > 0)
						{
							result = true;
						}
						else
						{
							result = conn.Get<T>(pk_mt) == null;
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
					Debugger.Break();
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
			return Task.Run(() =>
			//                return Task.Factory.StartNew<List<T>>(() => // Task.Run is newer and shorter than Task.Factory.StartNew . It also has some different default settings in certain overloads.
			{
				return DeleteRecordsWithParentId<T>(SemaphoreSlimSafeRelease, tableName, parentId);
			});
		}
		private bool DeleteRecordsWithParentId<T>(SemaphoreSlimSafeRelease semaphore, string tableName, string parentId, string parentIdFieldName = nameof(DbBoundObservableData.ParentId)) where T : new()
		{
			if (!_isOpen) return false; ;

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

						string delQuery = string.Format("DELETE FROM {0} WHERE " + parentIdFieldName + " = '{1}'", tableName, parentId);
						int delQueryResult = conn.Execute(delQuery);

						string readQuery = string.Format("SELECT * FROM {0} WHERE " + parentIdFieldName + " = '{1}'", tableName, parentId);
						var readQueryResult = conn.Query<T>(delQuery);
						if (readQueryResult.Count <= 0) result = true;
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
					Debugger.Break();
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
					Debugger.Break();
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
					Debugger.Break();
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
					Debugger.Break();
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
		private Task<T> ReadRecordByIdAsync<T>(SemaphoreSlimSafeRelease SemaphoreSlimSafeRelease, object primaryKey) where T : new()
		{
			return Task.Run<T>(() =>
			//                return Task.Factory.StartNew<List<T>>(() => // Task.Run is newer and shorter than Task.Factory.StartNew . It also has some different default settings in certain overloads.
			{
				return ReadRecordById<T>(SemaphoreSlimSafeRelease, primaryKey);
			});
		}
		private T ReadRecordById<T>(SemaphoreSlimSafeRelease semaphore, object primaryKey) where T : new()
		{
			if (!_isOpen) return default(T);

			T result = default(T);
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
						var query = conn.Get<T>(primaryKey);
						result = query;
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
					Debugger.Break();
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

		private Task<InsertResult> InsertAsync<T>(object item, bool checkMaxEntries, SemaphoreSlimSafeRelease semaphore) where T : new()
		{
			return Task.Run(() =>
			{
				return Insert<T>(item, checkMaxEntries, semaphore);
			});
		}
		private InsertResult Insert<T>(object item, bool checkMaxEntries, SemaphoreSlimSafeRelease semaphore) where T : new()
		{
			if (!_isOpen) return InsertResult.NothingDone;
			InsertResult result = InsertResult.NothingDone;
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
						if (insertResult > 0) result = InsertResult.Added;
					}
#pragma warning disable 0168
					catch (NotNullConstraintViolationException ex0)
#pragma warning restore 0168
					{
						result = InsertResult.AlreadyThere;
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
					Debugger.Break();
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

		private Task<InsertResult> InsertManyAsync<T>(IEnumerable<T> items, bool checkMaxEntries, SemaphoreSlimSafeRelease semaphore) where T : DbBoundObservableData, new() // new() //where T : DbBoundObservableData
		{
			return Task.Run(() =>
			{
				return InsertMany<T>(items, checkMaxEntries, semaphore);
			});
		}
		private InsertResult InsertMany<T>(IEnumerable<T> items, bool checkMaxEntries, SemaphoreSlimSafeRelease semaphore) where T : DbBoundObservableData, new()
		{
			if (!_isOpen) return InsertResult.NothingDone;
			InsertResult result = InsertResult.NothingDone;
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

						var itemsInTable = conn.Table<T>(); //.ToList();
						var newItems = items.Except(itemsInTable, new DbBoundObservableData());

						// LOLLO TODO check the following, I have added an equality comparer. 
						// LOLLO TODO Also see if you can make DbBoundObservableData abstract again (I think not).
						insertResult = conn.InsertAll(newItems);
						//}
						if (insertResult == newItems.Count()) result = InsertResult.Added;
						else if (insertResult == 0) result = InsertResult.NothingDone;
						else result = InsertResult.SomeAdded;
					}
#pragma warning disable 0168
					catch (NotNullConstraintViolationException ex0)
#pragma warning restore 0168
					{
						Debugger.Break();
						result = InsertResult.SomeAlreadyThere; // LOLLO TODO what if some have been added and some not?
																// I'd need to do th einserts one by one and surround each with a try and this catch.
																// I want to get rid of SomeAdded and SomeAlreadyThere !

						// LOLLO TODO check the estimators on DeleteResult and InsertResult,
						// we may want to "like" DeleteResult.AlreadyMissing and DeleteResult.Deleted
						// but only InsertResult.Added.
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
					Debugger.Break();
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

		private Task<bool> UpdateAsync<T>(object item, SemaphoreSlimSafeRelease semaphore) where T : new()
		{
			return Task.Run(() =>
			{
				return Update<T>(item, semaphore);
			});
		}
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
					Debugger.Break();
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
						Debugger.Break();
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
					Debugger.Break();
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
					{
						Debugger.Break();
						Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
					}
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryDispose(_connectionsDictSemaphore);
					_connectionsDictSemaphore = null;
				}
				return Task.CompletedTask;
			}
		}
	}
}
