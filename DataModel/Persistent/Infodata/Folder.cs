using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using UniFiler10.Data.Metadata;
using Utilz;
using Windows.Storage;
using System.Diagnostics;
using Utilz.Data;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public sealed class Folder : DbBoundObservableData
	{
		#region lifecycle
		public Folder() { }
		public Folder(DBManager dbManager, string name, DateTime dateCreated) : base()
		{
			DBManager = dbManager;
			Name = name;
			DateCreated = dateCreated;
		}
		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			if (DBManager == null) throw new Exception("Folder.OpenMayOverrideAsync found no open instances of DBManager");

			// read children from db
			var wallets = await DBManager.GetWalletsAsync(Id).ConfigureAwait(false);
			foreach (var wallet in wallets)
			{
				var docs = await DBManager.GetDocumentsAsync(wallet.Id).ConfigureAwait(false);
				wallet.Documents.ReplaceAll(docs);
			}

			var dynamicFields = await DBManager.GetDynamicFieldsAsync(Id).ConfigureAwait(false);

			var dynamicCategories = await DBManager.GetDynamicCategoriesByParentIdAsync(Id).ConfigureAwait(false);

			// refresh dynamic categories and fields if something changed in the metadata since the last save
			await RefreshDynamicPropertiesAsync(DBManager, dynamicCategories, dynamicFields, Id).ConfigureAwait(false);

			// populate collections
			await RunInUiThreadAsync(delegate
			{
				_wallets.ReplaceAll(wallets);
				_dynamicFields.ReplaceAll(dynamicFields);
				_dynamicCategories.ReplaceAll(dynamicCategories);
			}).ConfigureAwait(false);

			// open all children
			foreach (var wallet in _wallets)
			{
				await wallet.OpenAsync().ConfigureAwait(false);
			}
			foreach (var dynFld in _dynamicFields)
			{
				await dynFld.OpenAsync().ConfigureAwait(false);
			}
			foreach (var dynCat in _dynamicCategories)
			{
				await dynCat.OpenAsync().ConfigureAwait(false);
			}
		}
		protected override async Task CloseMayOverrideAsync()
		{
			var wallets = _wallets;
			if (wallets != null)
			{
				foreach (var wallet in wallets)
				{
					await wallet.CloseAsync().ConfigureAwait(false);
					wallet.Dispose();
				}
			}

			var dynFlds = _dynamicFields;
			if (dynFlds != null)
			{
				foreach (var dynFld in dynFlds)
				{
					await dynFld.CloseAsync().ConfigureAwait(false);
					dynFld.Dispose();
				}
			}

			var dynCats = _dynamicCategories;
			if (dynCats != null)
			{
				foreach (var dynCat in dynCats)
				{
					await dynCat.CloseAsync().ConfigureAwait(false);
					dynCat.Dispose();
				}
			}

			await RunInUiThreadAsync(delegate
			{
				_wallets?.Clear();
				_dynamicFields?.Clear();
				_dynamicCategories?.Clear();
			}).ConfigureAwait(false);
		}

		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_wallets?.Dispose();

			_dynamicCategories?.Dispose();

			_dynamicFields?.Dispose();

			// do not touch _dbManager, it was imported in the ctor and it it does not belong here
			_dbManager = null;
		}
		#endregion lifecycle


		#region properties
		private readonly object _dbManagerLocker = new object();
		private DBManager _dbManager = null;
		[IgnoreDataMember]
		[Ignore]
		public DBManager DBManager { get { lock (_dbManagerLocker) { return _dbManager; } } set { lock (_dbManagerLocker) { _dbManager = value; } } }

		private readonly SwitchableObservableDisposableCollection<DynamicCategory> _dynamicCategories = new SwitchableObservableDisposableCollection<DynamicCategory>();
		[IgnoreDataMember]
		[Ignore]
		public SwitchableObservableDisposableCollection<DynamicCategory> DynamicCategories { get { return _dynamicCategories; } }

		private readonly SwitchableObservableDisposableCollection<DynamicField> _dynamicFields = new SwitchableObservableDisposableCollection<DynamicField>();
		[IgnoreDataMember]
		[Ignore]
		public SwitchableObservableDisposableCollection<DynamicField> DynamicFields { get { return _dynamicFields; } }

		private string _name = string.Empty;
		[DataMember]
		public string Name { get { return _name; } set { SetPropertyUpdatingDb(ref _name, value); } }

		private string _descr0 = string.Empty;
		[DataMember]
		public string Descr0 { get { return _descr0; } set { SetPropertyUpdatingDb(ref _descr0, value); } }

		private string _descr1 = string.Empty;
		[DataMember]
		public string Descr1 { get { return _descr1; } set { SetPropertyUpdatingDb(ref _descr1, value); } }

		private string _descr2 = string.Empty;
		[DataMember]
		public string Descr2 { get { return _descr2; } set { SetPropertyUpdatingDb(ref _descr2, value); } }

		private string _descr3 = string.Empty;
		[DataMember]
		public string Descr3 { get { return _descr3; } set { SetPropertyUpdatingDb(ref _descr3, value); } }

		private DateTime _dateCreated = default(DateTime);
		[DataMember]
		public DateTime DateCreated { get { return _dateCreated; } set { SetPropertyUpdatingDb(ref _dateCreated, value); } }

		private DateTime _date0 = default(DateTime);
		[DataMember]
		public DateTime Date0 { get { return _date0; } set { SetPropertyUpdatingDb(ref _date0, value); } }

		private DateTime _date1 = default(DateTime);
		[DataMember]
		public DateTime Date1 { get { return _date1; } set { SetPropertyUpdatingDb(ref _date1, value); } }

		private DateTime _date2 = default(DateTime);
		[DataMember]
		public DateTime Date2 { get { return _date2; } set { SetPropertyUpdatingDb(ref _date2, value); } }

		private DateTime _date3 = default(DateTime);
		[DataMember]
		public DateTime Date3 { get { return _date3; } set { SetPropertyUpdatingDb(ref _date3, value); } }

		private readonly SwitchableObservableDisposableCollection<Wallet> _wallets = new SwitchableObservableDisposableCollection<Wallet>();
		[IgnoreDataMember]
		[Ignore]
		public SwitchableObservableDisposableCollection<Wallet> Wallets { get { return _wallets; } }

		private bool _isEditingCategories = true;
		[DataMember]
		public bool IsEditingCategories { get { return _isEditingCategories; } set { SetPropertyUpdatingDb(ref _isEditingCategories, value); } }

		[DataMember]
		public override string ParentId { get { return DEFAULT_ID; } set { SetPropertyUpdatingDb(ref _parentId, DEFAULT_ID, false); } }
		#endregion properties

		protected override bool UpdateDbMustOverride()
		{
			return DBManager?.UpdateFolders(this) == true;
		}

		//protected override bool IsEqualToMustOverride(DbBoundObservableData that)
		//{
		//	var target = that as Folder;

		//	return
		//		DateCreated == target.DateCreated &&
		//		Date0 == target.Date0 &&
		//		Date1 == target.Date1 &&
		//		Date2 == target.Date2 &&
		//		Date3 == target.Date3 &&
		//		Descr0 == target.Descr0 &&
		//		Descr1 == target.Descr1 &&
		//		Descr2 == target.Descr2 &&
		//		Descr3 == target.Descr3 &&
		//		Name == target.Name &&
		//		Wallet.AreEqual(Wallets, target.Wallets) &&
		//		DynamicCategory.AreEqual(DynamicCategories, target.DynamicCategories) &&
		//		DynamicField.AreEqual(DynamicFields, target.DynamicFields);
		//}

		protected override bool CheckMeMustOverride()
		{
			bool result = _id != DEFAULT_ID && Check(_wallets) && Check(_dynamicCategories) && Check(_dynamicFields);
			return result;
		}

		#region loading methods
		/// <summary>
		/// Refreshes dynamic categories and fields if something changed in the metadata since the last save
		/// </summary>
		/// <param name="dbM"></param>
		/// <param name="dynamicCategories"></param>
		/// <param name="dynamicFields"></param>
		/// <param name="folderId"></param>
		/// <returns></returns>
		private static async Task RefreshDynamicPropertiesAsync(DBManager dbM, ICollection<DynamicCategory> dynamicCategories, ICollection<DynamicField> dynamicFields, string folderId)
		{
			await RefreshDynamicCategoriesAsync(dbM, dynamicCategories, dynamicFields).ConfigureAwait(false);
			await RefreshDynamicFieldsAsync(dbM, dynamicCategories, dynamicFields, folderId).ConfigureAwait(false);
		}
		/// <summary>
		/// Removes those categories that have been removed since the last save, and their fields.
		/// To be sure, we assume the metadata has been changed since the folder was last saved.
		/// </summary>
		/// <returns></returns>
		private static async Task RefreshDynamicCategoriesAsync(DBManager dbM, ICollection<DynamicCategory> dynamicCategories, ICollection<DynamicField> dynamicFields)
		{
			var availableCats = MetaBriefcase.OpenInstance?.Categories;
			if (availableCats == null) return;

			var obsoleteDynCats = dynamicCategories.Where(dynCat => availableCats.All(cat => cat.Id != dynCat.CategoryId)).ToList();
			foreach (var obsoleteDynCat in obsoleteDynCats)
			{
				await RemoveDynamicCategoryAndItsDynFields2Async(obsoleteDynCat.CategoryId, dbM, dynamicCategories, dynamicFields).ConfigureAwait(false);
			}
		}
		/// <summary>
		/// Removes those fields that have been removed from the categories since the last save,
		/// and adds those fields that have been added to the categories since the last save.
		/// To be sure, we assume the metadata has been changed since the folder was last saved.
		/// </summary>
		/// <returns></returns>
		private static async Task RefreshDynamicFieldsAsync(DBManager dbM, ICollection<DynamicCategory> dynamicCategories, ICollection<DynamicField> dynamicFields, string folderId)
		{
			if (dbM == null) return;

			try
			{
				var shouldBeFldDscs = new List<FieldDescription>(); // HashSet may be tricky, a List is easier
				foreach (var dynCat in dynamicCategories)
				{
					if (dynCat.Category != null)
					{
						foreach (var fldDsc in dynCat.Category.FieldDescriptions)
						{
							if (shouldBeFldDscs.All(fd => fd.Id != fldDsc.Id)) shouldBeFldDscs.Add(fldDsc);
						}
					}
					else
					{
						await Logger.AddAsync("DynamicCategory " + dynCat.Id + " has Category = null: this must never happen", Logger.ForegroundLogFilename).ConfigureAwait(false);
						Debugger.Break(); // this must never happen
					}
				}
				// remove obsolete fields
				// LOLLO I create a List, instead of using the IEnumerable, otherwise the following Remove will break the ienumerable and dump!
				var obsoleteDynFlds = dynamicFields.Where(dynFld => shouldBeFldDscs.All(fldDsc => fldDsc.Id != dynFld.FieldDescriptionId)).ToList();
				foreach (var obsoleteDynFld in obsoleteDynFlds)
				{
					if (await dbM.DeleteFromDynamicFieldsAsync(obsoleteDynFld).ConfigureAwait(false) && obsoleteDynFld != null)
					{
						dynamicFields.Remove(obsoleteDynFld);
						await obsoleteDynFld.CloseAsync().ConfigureAwait(false);
					}
				}

				var newFldDscs = shouldBeFldDscs.Where(fldDsc => dynamicFields.All(dynFld => dynFld.FieldDescriptionId != fldDsc.Id));
				foreach (var newFldDsc in newFldDscs)
				{
					var dynFld = new DynamicField(dbM, folderId, newFldDsc.Id);
					if (await dbM.InsertIntoDynamicFieldsAsync(dynFld).ConfigureAwait(false) && dynFld != null)
					{
						dynamicFields.Add(dynFld);
						await dynFld.OpenAsync().ConfigureAwait(false);
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
				Debugger.Break();
			}
		}
		#endregion loading methods


		#region while open methods
		public Task<bool> SetDbManager(DBManager dbManager)
		{
			return RunFunctionIfOpenAsyncA(delegate
			{
				DBManager = dbManager;
				foreach (var wal in _wallets)
				{
					wal.DBManager = dbManager;
					foreach (var doc in wal.Documents)
					{
						doc.DBManager = dbManager;
					}
				}
				foreach (var dynCat in _dynamicCategories)
				{
					dynCat.DBManager = dbManager;
				}
				foreach (var dynFld in _dynamicFields)
				{
					dynFld.DBManager = dbManager;
				}
			});
		}
		public Task<bool> AddWalletAsync()
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				var wallet = new Wallet(DBManager, Id);
				return await AddWallet2Async(wallet).ConfigureAwait(false);
			});
		}

		private async Task<bool> AddWallet2Async(Wallet wallet)
		{
			if (wallet != null)
			{
				if (Wallet.Check(wallet))
				{
					var dbM = DBManager;
					if (dbM != null && await dbM.InsertIntoWalletsAsync(wallet))
					{
						await RunInUiThreadAsync(delegate { _wallets.Add(wallet); }).ConfigureAwait(false);

						await wallet.OpenAsync().ConfigureAwait(false);
						return true;
					}
				}
				else
				{
					Logger.Add_TPL("ERROR in Folder.AddWalletAsync(): new wallet did not stand Wallet.CheckAllowedValues()", Logger.ForegroundLogFilename);
				}
			}
			return false;
		}

		public Task<bool> RemoveWalletsAsync()
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				while (_wallets?.Count > 0)
				{
					await _wallets[0].OpenAsync().ConfigureAwait(false);
					await RemoveWallet2Async(_wallets[0]).ConfigureAwait(false);
				}
				return true;
			});
		}
		public Task<bool> RemoveWalletAsync(Wallet wallet)
		{
			return RunFunctionIfOpenAsyncTB(async () => await RemoveWallet2Async(wallet).ConfigureAwait(false));
		}
		private async Task<bool> RemoveWallet2Async(Wallet wallet)
		{
			if (wallet != null && wallet.ParentId == Id)
			{
				await DBManager.DeleteFromWalletsAsync(wallet);

				await RunInUiThreadAsync(delegate { _wallets.Remove(wallet); }).ConfigureAwait(false);

				await wallet.OpenAsync().ConfigureAwait(false);
				await wallet.RemoveDocumentsAsync().ConfigureAwait(false);
				await wallet.CloseAsync().ConfigureAwait(false);
				wallet.Dispose();

				return true;
			}
			return false;
		}

		public Task<bool> AddDynamicCategoryAsync(string catId)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (string.IsNullOrWhiteSpace(catId)) return false;

				var newDynCat = new DynamicCategory(DBManager, Id, catId);

				//if (Check(newDynCat) && !_dynamicCategories.Any(dc => dc.CategoryId == catId))
				if (Check(newDynCat) && _dynamicCategories.All(dc => dc.CategoryId != catId) && await MetaBriefcase.OpenInstance.IsCategoryAvailableAsync(catId).ConfigureAwait(false))
				{
					List<DynamicField> newDynFlds = new List<DynamicField>();
					var dbM = DBManager;
					if (dbM != null && await dbM.InsertIntoDynamicCategoriesAsync(newDynCat, newDynFlds))
					{
						_dynamicCategories.Add(newDynCat);
						await newDynCat.OpenAsync();
						foreach (var dynFld in newDynFlds)
						{
							_dynamicFields.Add(dynFld);
							await dynFld.OpenAsync();
						}
						return true;
					}
				}
				return false;
			});
		}

		public Task<bool> RemoveDynamicCategoryAndItsFieldsAsync(string catId)
		{
			return RunFunctionIfOpenAsyncT(async delegate
			{
				Task remove = null;
				await RunInUiThreadAsync(() =>
				{
					remove = RemoveDynamicCategoryAndItsDynFields2Async(catId, DBManager, _dynamicCategories, _dynamicFields);
				}).ConfigureAwait(false);
				await remove.ConfigureAwait(false);
			});
		}
		private static async Task RemoveDynamicCategoryAndItsDynFields2Async(string catId, DBManager dbM, ICollection<DynamicCategory> dynamicCategories, ICollection<DynamicField> dynamicFields)
		{
			if (!string.IsNullOrWhiteSpace(catId) && dbM != null)
			{
				var dynCat = dynamicCategories.FirstOrDefault(a => a.CategoryId == catId);
				if (dynCat != null)
				{
					var descriptionIdsOfFieldsToBeRemoved = new List<string>();
					if (await dbM.DeleteFromDynamicCategoriesAsync(dynCat, descriptionIdsOfFieldsToBeRemoved))
					{
						dynamicCategories.Remove(dynCat);
						await dynCat.CloseAsync(); // no need to open dynCats

						foreach (var fieldDescriptionId in descriptionIdsOfFieldsToBeRemoved)
						{
							var fieldToBeRemoved = dynamicFields.FirstOrDefault(dynFld => dynFld.FieldDescriptionId == fieldDescriptionId);
							if (fieldToBeRemoved != null)
							{
								dynamicFields.Remove(fieldToBeRemoved);
								await fieldToBeRemoved.CloseAsync(); // no need to open dynFlds
							}
						}
					}
				}
			}
		}

		public Task<bool> ImportMediaFileIntoNewWalletAsync(StorageFile file)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (DBManager != null && file != null)
				{
					var newWallet = new Wallet(DBManager, Id);
					await newWallet.OpenAsync().ConfigureAwait(false); // open the wallet or the following won't run
					bool isOk = await newWallet.ImportFileAsync(file).ConfigureAwait(false)
						&& await AddWallet2Async(newWallet).ConfigureAwait(false);

					if (isOk)
					{
						return true;
					}
					else
					{
						await RemoveWallet2Async(newWallet).ConfigureAwait(false);
					}
				}
				return false;
			});
		}
		#endregion while open methods
	}
}