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
    public sealed class DBManager : IDisposable
    {
        #region fields
        // one db for all tables
        // one semaphore each table
        private string _dbName = string.Empty;
        private const string DB_FILE_NAME = "Db.db";
        private string _dbPath = string.Empty;

        private readonly bool _isStoreDateTimeAsTicks = true;
        private readonly SQLiteOpenFlags _openFlags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create; //.FullMutex;

        private static SemaphoreSlimSafeRelease _foldersSemaphore = null;
        private static SemaphoreSlimSafeRelease _walletsSemaphore = null;
        private static SemaphoreSlimSafeRelease _documentsSemaphore = null;
        private static SemaphoreSlimSafeRelease _dynamicFieldsSemaphore = null;
        private static SemaphoreSlimSafeRelease _dynamicCategoriesSemaphore = null;
        #endregion fields

        #region construct and dispose
        private static readonly object _instanceLock = new object();
        private static DBManager _instance = null;
        public static DBManager OpenInstance { get { if (_isOpen) return _instance; else return null; } }

        public static DBManager CreateInstance(string dbName)
        {
            lock (_instanceLock)
            {
                if (_instance == null || _instance._isDisposed)
                {
                    _instance = new DBManager(dbName);
                }
                return _instance;
            }
        }
        private DBManager(string pathInLocalFolder)
        {
            if (pathInLocalFolder != null && !string.IsNullOrWhiteSpace(pathInLocalFolder))
            {
                _dbName = pathInLocalFolder;
                _dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, _dbName, DB_FILE_NAME);
            }
            else throw new ArgumentNullException("DBManager ctor: dbName cannot be null or empty");
        }
        private volatile bool _isDisposed = false;
        public void Dispose()
        {
            _isDisposed = true;
            CloseAsync().Wait();
        }
        #endregion construct and dispose

        #region open and close
        private static SemaphoreSlimSafeRelease _isOpenSemaphore = null;
        private static volatile bool _isOpen = false;
        public static bool IsOpen { get { return _isOpen; } private set { _isOpen = value; } }
        /// <summary>
        /// Open the DB
        /// </summary>
        public async Task OpenAsync()
        {
            if (!_isOpen)
            {
                try
                {
                    if (!SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore)) _isOpenSemaphore = new SemaphoreSlimSafeRelease(1, 1);
                    await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);

                    if (!_isOpen)
                    {
                        var dbFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(_dbName, CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);
                        
                        if (!SemaphoreSlimSafeRelease.IsAlive(_foldersSemaphore)) _foldersSemaphore = new SemaphoreSlimSafeRelease(1, 1);
                        if (!SemaphoreSlimSafeRelease.IsAlive(_walletsSemaphore)) _walletsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
                        if (!SemaphoreSlimSafeRelease.IsAlive(_documentsSemaphore)) _documentsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
                        if (!SemaphoreSlimSafeRelease.IsAlive(_dynamicFieldsSemaphore)) _dynamicFieldsSemaphore = new SemaphoreSlimSafeRelease(1, 1);
                        if (!SemaphoreSlimSafeRelease.IsAlive(_dynamicCategoriesSemaphore)) _dynamicCategoriesSemaphore = new SemaphoreSlimSafeRelease(1, 1);
                        LolloSQLiteConnectionPoolMT.Open();
                        IsOpen = true;
                    }
                }
                catch (Exception ex)
                {
                    if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
                        Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                }
            }
        }
        /// <summary>
        /// Wait for all DB operations to end and close the DB
        /// </summary>
        /// <returns></returns>
        public async Task CloseAsync()
        {
            if (_isOpen)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        if (_isOpen)
                        {
                            try
                            {
                                _foldersSemaphore.Wait();
                                _walletsSemaphore.Wait();
                                _documentsSemaphore.Wait();
                                _dynamicFieldsSemaphore.Wait();
                                _dynamicCategoriesSemaphore.Wait();

                                IsOpen = false;

                                LolloSQLiteConnectionPoolMT.Close();
                            }
                            catch (Exception ex)
                            {
                                if (SemaphoreSlimSafeRelease.IsAlive(_foldersSemaphore)
                                    && SemaphoreSlimSafeRelease.IsAlive(_walletsSemaphore)
                                    && SemaphoreSlimSafeRelease.IsAlive(_documentsSemaphore)
                                    && SemaphoreSlimSafeRelease.IsAlive(_dynamicFieldsSemaphore)
                                    && SemaphoreSlimSafeRelease.IsAlive(_dynamicCategoriesSemaphore))
                                    Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
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
                    }).ConfigureAwait(false);
                }
                finally
                {
                    SemaphoreSlimSafeRelease.TryDispose(_isOpenSemaphore);
                    _isOpenSemaphore = null;
                }
            }
        }
        #endregion open and close

        #region table methods
        internal async Task<bool> UpdateDynamicFieldsAsync(DynamicField newRecord)
        {
            bool result = false;
            try
            {
                var oldRecord = await LolloSQLiteConnectionMT.ReadRecordByIdAsync<DynamicField>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _dynamicFieldsSemaphore, newRecord.Id).ConfigureAwait(false);
                if (oldRecord != null && !oldRecord.IsEqualTo(newRecord))
                {
                    if (DynamicField.Check(newRecord))
                    {
                        result = await LolloSQLiteConnectionMT.UpdateAsync<DynamicField>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, newRecord, _dynamicFieldsSemaphore).ConfigureAwait(false);
                        //// LOLLO remove when done testing
                        //var testRecord = await LolloSQLiteConnectionMT.ReadRecordByDBIndexAsync<DynamicField>(_dynamicFieldsDbPath, _openFlags, _isStoreDateTimeAsTicks, _dynamicFieldsSemaphore, newRecord.DbIndex).ConfigureAwait(false);
                        //var test = testRecord.Vaalue;
                    }
                    if (!result)
                    {
                        await oldRecord.CopyAsync(newRecord).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
            return result;
        }
        internal async Task<bool> UpdateDynamicCategoriesAsync(DynamicCategory newRecord)
        {
            bool result = false;
            try
            {
                var oldRecord = await LolloSQLiteConnectionMT.ReadRecordByIdAsync<DynamicCategory>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _dynamicCategoriesSemaphore, newRecord.Id).ConfigureAwait(false);
                if (oldRecord != null && !oldRecord.IsEqualTo(newRecord))
                {
                    if (DynamicCategory.Check(newRecord))
                    {
                        result = await LolloSQLiteConnectionMT.UpdateAsync<DynamicCategory>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, newRecord, _dynamicCategoriesSemaphore).ConfigureAwait(false);
                        //// LOLLO remove when done testing
                        //var testRecord = await LolloSQLiteConnectionMT.ReadRecordByDBIndexAsync<DynamicCategory>(_dynamicCategoriesDbPath, _openFlags, _isStoreDateTimeAsTicks, _dynamicCategoriesSemaphore, newRecord.DbIndex).ConfigureAwait(false);
                        //var test = testRecord.CategoryId;
                    }
                    if (!result)
                    {
                        await oldRecord.CopyAsync(newRecord).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
            return result;
        }
        internal async Task<bool> UpdateDocumentsAsync(Document newRecord)
        {
            bool result = false;
            try
            {
                var oldRecord = await LolloSQLiteConnectionMT.ReadRecordByIdAsync<Document>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _documentsSemaphore, newRecord.Id).ConfigureAwait(false);
                if (oldRecord != null && !oldRecord.IsEqualTo(newRecord))
                {
                    if (Document.Check(newRecord)) // && await CheckUniqueKeyInDocumentsAsync(newRecord).ConfigureAwait(false))
                    {
                        result = await LolloSQLiteConnectionMT.UpdateAsync<Document>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, newRecord, _documentsSemaphore).ConfigureAwait(false);
                    }
                    if (!result)
                    {
                        await oldRecord.CopyAsync(newRecord).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
            return result;
        }
        internal async Task<bool> UpdateWalletsAsync(Wallet newRecord)
        {
            bool result = false;
            try
            {
                var oldRecord = await LolloSQLiteConnectionMT.ReadRecordByIdAsync<Wallet>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _walletsSemaphore, newRecord.Id).ConfigureAwait(false);
                if (oldRecord != null && !oldRecord.IsEqualTo(newRecord))
                {
                    if (Wallet.Check(newRecord)) // && await CheckUniqueKeyInWalletsAsync(newRecord).ConfigureAwait(false))
                    {
                        result = await LolloSQLiteConnectionMT.UpdateAsync<Wallet>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, newRecord, _walletsSemaphore).ConfigureAwait(false);
                    }
                    if (!result)
                    {
                        await oldRecord.CopyAsync(newRecord).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
            return result;
        }
        internal async Task<bool> UpdateFoldersAsync(Folder newRecord)
        {
            bool result = false;
            try
            {
                var oldRecord = await LolloSQLiteConnectionMT.ReadRecordByIdAsync<Folder>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _foldersSemaphore, newRecord.Id).ConfigureAwait(false);
                if (oldRecord != null && !oldRecord.IsEqualTo(newRecord))
                {
                    if (Folder.Check(newRecord)) // && await CheckForeignKey_TagsInFolderAsync(newRecord).ConfigureAwait(false))
                    {
                        result = await LolloSQLiteConnectionMT.UpdateAsync<Folder>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, newRecord, _foldersSemaphore).ConfigureAwait(false);
                    }
                    if (!result)
                    {
                        await oldRecord.CopyAsync(newRecord).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
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
                    var fieldsAlreadyInFolder = await LolloSQLiteConnectionMT.ReadRecordsWithParentIdAsync<DynamicField>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _dynamicFieldsSemaphore, nameof(DynamicCategory), newFld.ParentId).ConfigureAwait(false);
                    if (!fieldsAlreadyInFolder.Any(ff => ff.FieldDescriptionId == newFld.FieldDescriptionId))
                    {
                        result = await LolloSQLiteConnectionMT.InsertAsync<DynamicField>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, newFld, checkMaxEntries, _dynamicFieldsSemaphore).ConfigureAwait(false);
                    }
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
                    var catsAlreadyInFolder = await LolloSQLiteConnectionMT.ReadRecordsWithParentIdAsync<DynamicCategory>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _dynamicCategoriesSemaphore, nameof(DynamicCategory), newCat.ParentId).ConfigureAwait(false);
                    if (!catsAlreadyInFolder.Any(ca => ca.CategoryId == newCat.CategoryId))
                    {
                        result = await LolloSQLiteConnectionMT.InsertAsync<DynamicCategory>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, newCat, checkMaxEntries, _dynamicCategoriesSemaphore).ConfigureAwait(false);
                        if (result)
                        {
                            // add the fields belonging to the new category, without duplicating existing fields (categories may share fields)
                            var fieldDescriptionIdsAlreadyInFolder = new List<string>();

                            var fieldsInFolder = await LolloSQLiteConnectionMT.ReadRecordsWithParentIdAsync<DynamicField>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _dynamicFieldsSemaphore, nameof(DynamicField), newCat.ParentId).ConfigureAwait(false);
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
                                    var dynamicField = new DynamicField() { FieldDescriptionId = fieldDescriptionId, ParentId = newCat.ParentId };
                                    if (await LolloSQLiteConnectionMT.InsertAsync<DynamicField>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, dynamicField, checkMaxEntries, _dynamicFieldsSemaphore).ConfigureAwait(false))
                                    {
                                        newFields.Add(dynamicField);
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
        internal async Task<bool> DeleteFromDynamicCategoriesAsync(DynamicCategory cat, List<string> deletedFieldDescriptionIds)
        {
            bool result = false;
            try
            {
                deletedFieldDescriptionIds.Clear();
                result = await LolloSQLiteConnectionMT.DeleteAsync<DynamicCategory>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, cat, _dynamicCategoriesSemaphore).ConfigureAwait(false);
                // delete the dynamic fields owned by this category unless they are owned by another category
                if (result)
                {
                    var otherAvailableCategories = await LolloSQLiteConnectionMT.ReadRecordsWithParentIdAsync<DynamicCategory>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _dynamicCategoriesSemaphore, nameof(DynamicCategory), cat.ParentId).ConfigureAwait(false);

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

                    var dynamicFieldsInCurrentFolder = await LolloSQLiteConnectionMT.ReadRecordsWithParentIdAsync<DynamicField>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _dynamicFieldsSemaphore, nameof(DynamicField), cat.ParentId).ConfigureAwait(false);
                    foreach (var fieldToDelete in dynamicFieldsInCurrentFolder.Where(a => a?.FieldDescriptionId != null && !otherFieldDescrIds.Contains(a.FieldDescriptionId)))
                    {
                        if (await LolloSQLiteConnectionMT.DeleteAsync<DynamicField>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, fieldToDelete, _dynamicFieldsSemaphore).ConfigureAwait(false))
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
            bool result = false;
            try
            {
                result = await LolloSQLiteConnectionMT.DeleteAsync<DynamicField>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, fld, _dynamicFieldsSemaphore).ConfigureAwait(false);
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
                    result = await LolloSQLiteConnectionMT.InsertAsync<Document>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, record, checkMaxEntries, _documentsSemaphore).ConfigureAwait(false);
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
                    result = await LolloSQLiteConnectionMT.InsertAsync<Wallet>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, record, checkMaxEntries, _walletsSemaphore).ConfigureAwait(false);
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
                    result = await LolloSQLiteConnectionMT.InsertAsync<Folder>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, record, checkMaxEntries, _foldersSemaphore).ConfigureAwait(false);
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
                dynCats = await LolloSQLiteConnectionMT.ReadRecordsWithParentIdAsync<DynamicCategory>
                    (_dbPath, _openFlags, _isStoreDateTimeAsTicks, _dynamicCategoriesSemaphore, nameof(DynamicCategory), parentId)
                    .ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
            return dynCats;
        }
        internal async Task<List<DynamicField>> GetDynamicFieldsAsync(string parentId)
        {
            var dynFlds = new List<DynamicField>();
            try
            {
                dynFlds = await LolloSQLiteConnectionMT.ReadRecordsWithParentIdAsync<DynamicField>
                    (_dbPath, _openFlags, _isStoreDateTimeAsTicks, _dynamicFieldsSemaphore, nameof(DynamicField), parentId)
                    .ConfigureAwait(false);
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
                docs = await LolloSQLiteConnectionMT.ReadRecordsWithParentIdAsync<Document>
                    (_dbPath, _openFlags, _isStoreDateTimeAsTicks, _documentsSemaphore, nameof(Document), parentId).ConfigureAwait(false);
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
                docs = await LolloSQLiteConnectionMT.ReadTableAsync<Document>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _documentsSemaphore).ConfigureAwait(false);
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
                wallets = await LolloSQLiteConnectionMT.ReadRecordsWithParentIdAsync<Wallet>
                    (_dbPath, _openFlags, _isStoreDateTimeAsTicks, _walletsSemaphore, nameof(Wallet), parentId).ConfigureAwait(false);
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
                wallets = await LolloSQLiteConnectionMT.ReadTableAsync<Wallet>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _walletsSemaphore).ConfigureAwait(false);
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
                folders = await LolloSQLiteConnectionMT.ReadTableAsync<Folder>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _foldersSemaphore).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
            return folders;
        }

        internal async Task<bool> DeleteFromDocumentsAsync(Document record)
        {
            bool result = false;
            try
            {
                result = await LolloSQLiteConnectionMT.DeleteAsync<Document>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, record, _documentsSemaphore).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
            return result;
        }

        internal async Task<bool> DeleteFromWalletsAsync(Wallet record)
        {
            bool result = false;
            try
            {
                result = await LolloSQLiteConnectionMT.DeleteAsync<Wallet>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, record, _walletsSemaphore).ConfigureAwait(false);
                result = result & await LolloSQLiteConnectionMT.DeleteRecordsWithParentIdAsync<Document>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _documentsSemaphore, nameof(Document), record.Id).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
            return result;
        }
        internal async Task<bool> DeleteFromFoldersAsync(Folder folder)
        {
            bool result = false;
            try
            {
                // TODO maybe add a new method to delete multiple records with any of the IDs, which are passed in an array
                result = await LolloSQLiteConnectionMT.DeleteAsync<Folder>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, folder, _foldersSemaphore).ConfigureAwait(false);
                if (result)
                {
                    var wallets = await LolloSQLiteConnectionMT.ReadRecordsWithParentIdAsync<Wallet>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _walletsSemaphore, nameof(Wallet), folder.Id).ConfigureAwait(false);

                    if (await LolloSQLiteConnectionMT.DeleteRecordsWithParentIdAsync<Wallet>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _documentsSemaphore, nameof(Wallet), folder.Id).ConfigureAwait(false))
                    {
                        foreach (var wallet in wallets.Distinct())
                        {
                            await LolloSQLiteConnectionMT.DeleteRecordsWithParentIdAsync<Document>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _documentsSemaphore, nameof(Document), wallet.Id).ConfigureAwait(false);
                        }
                    }
                    await LolloSQLiteConnectionMT.DeleteRecordsWithParentIdAsync<DynamicCategory>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _dynamicCategoriesSemaphore, nameof(DynamicCategory), folder.Id).ConfigureAwait(false);
                    await LolloSQLiteConnectionMT.DeleteRecordsWithParentIdAsync<DynamicField>(_dbPath, _openFlags, _isStoreDateTimeAsTicks, _dynamicFieldsSemaphore, nameof(DynamicField), folder.Id).ConfigureAwait(false);
                }
            }
            catch (Exception exc)
            {
                Logger.Add_TPL(exc.ToString(), Logger.PersistentDataLogFilename);
            }
            return result;
        }
        #endregion table methods

        private class LolloSQLiteConnectionMT
        {
            public static Task<List<T>> ReadTableAsync<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, SemaphoreSlimSafeRelease SemaphoreSlimSafeRelease) where T : new()
            {
                return Task.Run<List<T>>(() =>
                //                return Task.Factory.StartNew<List<T>>(() => // Task.Run is newer and shorter than Task.Factory.StartNew . It also has some different default settings in certain overloads.
                {
                    return ReadTable<T>(dbPath, openFlags, storeDateTimeAsTicks, SemaphoreSlimSafeRelease);
                });
            }
            public static List<T> ReadTable<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, SemaphoreSlimSafeRelease semaphore) where T : new()
            {
                if (!_isOpen) return null;

                List<T> result = null;
                try
                {
                    semaphore.Wait();
                    if (_isOpen)
                    {
                        var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                        var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                        try
                        {
                            int aResult = conn.CreateTable(typeof(T));
                            var query = conn.Table<T>();
                            result = query.ToList<T>();
                        }
                        finally
                        {
                            LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
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

            public static Task<bool> DeleteRecordsWithParentIdAsync<T>(string dbPath, SQLiteOpenFlags openFlags, bool storeDateTimeAsTicks, SemaphoreSlimSafeRelease SemaphoreSlimSafeRelease, string tableName, string parentId) where T : new()
            {
                return Task.Run<bool>(() =>
                //                return Task.Factory.StartNew<List<T>>(() => // Task.Run is newer and shorter than Task.Factory.StartNew . It also has some different default settings in certain overloads.
                {
                    return DeleteRecordsWithParentId<T>(dbPath, openFlags, storeDateTimeAsTicks, SemaphoreSlimSafeRelease, tableName, parentId);
                });
            }
            public static bool DeleteRecordsWithParentId<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, SemaphoreSlimSafeRelease semaphore, string tableName, string parentId) where T : new()
            {
                if (!_isOpen) return false;

                bool result = false;
                try
                {
                    semaphore.Wait();
                    if (_isOpen)
                    {
                        var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                        var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                        try
                        {
                            int aResult = conn.CreateTable(typeof(T));
                            string queryString = string.Format("DELETE FROM {0} WHERE ParentId = '{1}'", tableName, parentId);
                            int queryResult = conn.Execute(queryString);
                            result = queryResult > 0;
                        }
                        finally
                        {
                            LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
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
            public static Task<List<T>> ReadRecordsWithParentIdAsync<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, SemaphoreSlimSafeRelease SemaphoreSlimSafeRelease, string tableName, string parentId) where T : new()
            {
                return Task.Run<List<T>>(() =>
                //                return Task.Factory.StartNew<List<T>>(() => // Task.Run is newer and shorter than Task.Factory.StartNew . It also has some different default settings in certain overloads.
                {
                    return ReadRecordsWithParentId<T>(dbPath, openFlags, storeDateTimeAsTicks, SemaphoreSlimSafeRelease, tableName, parentId);
                });
            }
            public static List<T> ReadRecordsWithParentId<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, SemaphoreSlimSafeRelease semaphore, string tableName, string parentId) where T : new()
            {
                if (!_isOpen) return null;

                List<T> result = null;
                try
                {
                    semaphore.Wait();
                    if (_isOpen)
                    {
                        var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                        var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                        try
                        {
                            int aResult = conn.CreateTable(typeof(T));
                            string queryString = string.Format("SELECT * FROM {0} WHERE ParentId = '{1}'", tableName, parentId);
                            var query = conn.Query<T>(queryString);
                            result = query.ToList<T>();
                        }
                        finally
                        {
                            LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
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
            public static Task<T> ReadRecordByIdAsync<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, SemaphoreSlimSafeRelease SemaphoreSlimSafeRelease, object primaryKey) where T : new()
            {
                return Task.Run<T>(() =>
                //                return Task.Factory.StartNew<List<T>>(() => // Task.Run is newer and shorter than Task.Factory.StartNew . It also has some different default settings in certain overloads.
                {
                    return ReadRecordById<T>(dbPath, openFlags, storeDateTimeAsTicks, SemaphoreSlimSafeRelease, primaryKey);
                });
            }
            public static T ReadRecordById<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, SemaphoreSlimSafeRelease semaphore, object primaryKey) where T : new()
            {
                if (!_isOpen) return default(T);

                T result = default(T);
                try
                {
                    semaphore.Wait();
                    if (_isOpen)
                    {
                        var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                        var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                        try
                        {
                            int aResult = conn.CreateTable(typeof(T));
                            var query = conn.Get<T>(primaryKey);
                            result = query;
                        }
                        finally
                        {
                            LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
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
            public static Task<bool> DeleteAllAsync<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, SemaphoreSlimSafeRelease semaphore)
            {
                return Task.Run(() =>
                {
                    return DeleteAll<T>(dbPath, openFlags, storeDateTimeAsTicks, semaphore);
                });
            }
            public static bool DeleteAll<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, SemaphoreSlimSafeRelease semaphore)
            {
                if (!_isOpen) return false;
                bool result = false;

                try
                {
                    semaphore.Wait();
                    if (_isOpen)
                    {
                        var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                        var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                        try
                        {
                            int aResult = conn.CreateTable(typeof(T));
                            int deleteResult = conn.DeleteAll<T>();
                            result = true;
                        }
                        finally
                        {
                            LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
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
            public static Task<bool> InsertAsync<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, object item, bool checkMaxEntries, SemaphoreSlimSafeRelease semaphore) where T : new()
            {
                return Task.Run(() =>
                {
                    return Insert<T>(dbPath, openFlags, storeDateTimeAsTicks, item, checkMaxEntries, semaphore);
                });
            }
            public static bool Insert<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, object item, bool checkMaxEntries, SemaphoreSlimSafeRelease semaphore) where T : new()
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
                        //        String aaa = i.ToString();
                        //    }
                        //}

                        object item_mt = item;
                        var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                        var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                        try
                        {
                            int aResult = conn.CreateTable(typeof(T));
                            int insertResult = 0;
                            if (checkMaxEntries)
                            {
                                var query = conn.Table<T>();
                                var count = query.Count();
                                insertResult = conn.Insert(item_mt);
                            }
                            else
                            {
                                insertResult = conn.Insert(item_mt);
                            }
                            result = insertResult > 0;
                        }
                        finally
                        {
                            LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
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
            public static Task<bool> DeleteAsync<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, object item, SemaphoreSlimSafeRelease semaphore) where T : new()
            {
                return Task.Run(() =>
                {
                    return Delete<T>(dbPath, openFlags, storeDateTimeAsTicks, item, semaphore);
                });
            }
            public static bool Delete<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, object item, SemaphoreSlimSafeRelease semaphore) where T : new()
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
                        //        String aaa = i.ToString();
                        //    }
                        //}

                        object item_mt = item;
                        var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                        var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                        try
                        {
                            int aResult = conn.CreateTable(typeof(T));
                            int deleteResult = conn.Delete(item_mt);
                            result = (deleteResult > 0);
                        }
                        finally
                        {
                            LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
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
            public static Task<bool> UpdateAsync<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, object item, SemaphoreSlimSafeRelease semaphore) where T : new()
            {
                return Task.Run(() =>
                {
                    return Update<T>(dbPath, openFlags, storeDateTimeAsTicks, item, semaphore);
                });
            }
            public static bool Update<T>(String dbPath, SQLiteOpenFlags openFlags, Boolean storeDateTimeAsTicks, object item, SemaphoreSlimSafeRelease semaphore) where T : new()
            {
                if (!_isOpen) return false;
                bool result = false;
                try
                {
                    semaphore.Wait();
                    if (_isOpen)
                    {
                        var connectionString = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks);
                        var conn = LolloSQLiteConnectionPoolMT.GetConnection(connectionString, openFlags);
                        try
                        {
                            int aResult = conn.CreateTable(typeof(T));
                            {
                                int updateResult = conn.Update(item);
                                result = (updateResult > 0);
                            }
                        }
                        finally
                        {
                            LolloSQLiteConnectionPoolMT.ResetConnection(connectionString.ConnectionString);
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
        }
        private static class LolloSQLiteConnectionPoolMT
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

            private static readonly Dictionary<string, LolloConnection> _connectionsDict = new Dictionary<string, LolloConnection>();
            private static SemaphoreSlimSafeRelease _connectionsDictSemaphore = null;

            internal static SQLiteConnection GetConnection(SQLiteConnectionString connectionString, SQLiteOpenFlags openFlags)
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
                        Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
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
            internal static void ResetConnection(string connectionString)
            {
                try
                {
                    _connectionsDictSemaphore.Wait();
                    LolloConnection conn;
                    if (_connectionsDict.TryGetValue(connectionString, out conn))
                    {
                        conn.Reset();
                        _connectionsDict.Remove(connectionString);
                    }
                }
                catch (Exception ex)
                {
                    if (SemaphoreSlimSafeRelease.IsAlive(_connectionsDictSemaphore))
                        Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
                }
                finally
                {
                    SemaphoreSlimSafeRelease.TryRelease(_connectionsDictSemaphore);
                }
            }

            /// <summary>
            /// Call this method when the application is resumed.
            /// </summary>
            internal static void Open()
            {
                // I don't need a semaphore for this Open / Close pair coz it is managed by the owner class DBManager
                if (!SemaphoreSlimSafeRelease.IsAlive(_connectionsDictSemaphore)) _connectionsDictSemaphore = new SemaphoreSlimSafeRelease(1, 1);
            }
            /// <summary>
            /// Closes all connections managed by this pool.
            /// </summary>
            internal static void Close()
            {
                // I don't need a semaphore for this Open / Close pair coz it is managed by the owner class DBManager
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
            }
        }
    }
}
