using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using UniFiler10.Data.Metadata;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using System.Reflection;
using System.Diagnostics;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public sealed class Folder : DbBoundObservableData
	{
		public Folder(DBManager dbManager)
		{
			_dbManager = dbManager;
		}
		public Folder() { }

		#region properties
		private DBManager _dbManager = null;
		[IgnoreDataMember]
		[Ignore]
		public DBManager DBManager { get { return _dbManager; } set { _dbManager = value; } }

		private SwitchableObservableCollection<DynamicCategory> _dynamicCategories = new SwitchableObservableCollection<DynamicCategory>();
		[IgnoreDataMember]
		[Ignore]
		public SwitchableObservableCollection<DynamicCategory> DynamicCategories { get { return _dynamicCategories; } set { if (_dynamicCategories != value) { _dynamicCategories = value; RaisePropertyChanged_UI(); } } }

		private SwitchableObservableCollection<DynamicField> _dynamicFields = new SwitchableObservableCollection<DynamicField>();
		[IgnoreDataMember]
		[Ignore]
		public SwitchableObservableCollection<DynamicField> DynamicFields { get { return _dynamicFields; } set { if (_dynamicFields != value) { _dynamicFields = value; RaisePropertyChanged_UI(); } } }

		private string _name = string.Empty;
		[DataMember]
		public string Name { get { return _name; } set { SetProperty(ref _name, value); } }

		private string _descr0 = string.Empty;
		[DataMember]
		public string Descr0 { get { return _descr0; } set { SetProperty(ref _descr0, value); } }

		private string _descr1 = string.Empty;
		[DataMember]
		public string Descr1 { get { return _descr1; } set { SetProperty(ref _descr1, value); } }

		private string _descr2 = string.Empty;
		[DataMember]
		public string Descr2 { get { return _descr2; } set { SetProperty(ref _descr2, value); } }

		private string _descr3 = string.Empty;
		[DataMember]
		public string Descr3 { get { return _descr3; } set { SetProperty(ref _descr3, value); } }

		private DateTime _dateCreated = default(DateTime);
		[DataMember]
		public DateTime DateCreated { get { return _dateCreated; } set { SetProperty(ref _dateCreated, value); } }

		private DateTime _date0 = default(DateTime);
		[DataMember]
		public DateTime Date0 { get { return _date0; } set { SetProperty(ref _date0, value); } }

		private DateTime _date1 = default(DateTime);
		[DataMember]
		public DateTime Date1 { get { return _date1; } set { SetProperty(ref _date1, value); } }

		private DateTime _date2 = default(DateTime);
		[DataMember]
		public DateTime Date2 { get { return _date2; } set { SetProperty(ref _date2, value); } }

		private DateTime _date3 = default(DateTime);
		[DataMember]
		public DateTime Date3 { get { return _date3; } set { SetProperty(ref _date3, value); } }

		private SwitchableObservableCollection<Wallet> _wallets = new SwitchableObservableCollection<Wallet>();
		[IgnoreDataMember]
		[Ignore]
		public SwitchableObservableCollection<Wallet> Wallets { get { return _wallets; } private set { if (_wallets != value) { _wallets = value; RaisePropertyChanged_UI(); } } }

		private bool _isEditingCategories = true;
		[DataMember]
		public bool IsEditingCategories { get { return _isEditingCategories; } set { SetProperty(ref _isEditingCategories, value); } }
		#endregion properties

		protected override bool UpdateDbMustOverride()
		{
			return _dbManager?.UpdateFolders(this) == true;
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
		protected override async Task OpenMayOverrideAsync()
		{
			// LOLLO TODO the following OpenInstance fails when opening an ExtraBinder, across the app.
			if (_dbManager == null) throw new Exception("Folder.OpenMayOverrideAsync found no open instances of DBManager");

			// read children from db
			var wallets = await _dbManager.GetWalletsAsync(Id).ConfigureAwait(false);
			foreach (var wallet in wallets)
			{
				var docs = await _dbManager.GetDocumentsAsync(wallet.Id).ConfigureAwait(false);
				wallet.Documents.AddRange(docs);
			}

			var dynamicFields = await _dbManager.GetDynamicFieldsAsync(Id).ConfigureAwait(false);

			var dynamicCategories = await _dbManager.GetDynamicCategoriesAsync(Id).ConfigureAwait(false);

			// populate my collections
			await RunInUiThreadAsync(delegate
			{
				_wallets.Clear();
				_wallets.AddRange(wallets);

				_dynamicFields.Clear();
				_dynamicFields.AddRange(dynamicFields);

				_dynamicCategories.Clear();
				_dynamicCategories.AddRange(dynamicCategories);
			}).ConfigureAwait(false);

			// refresh dynamic categories and fields if something changed in the metadata since the last save
			await RefreshDynamicPropertiesAsync().ConfigureAwait(false);

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
			_wallets = null;

			_dynamicCategories?.Dispose();
			_dynamicCategories = null;

			_dynamicFields?.Dispose();
			_dynamicFields = null;

			_dbManager = null;
		}

		private async Task RefreshDynamicPropertiesAsync()
		{
			// update DynamicCategories if metadata has changed since last db save
			await RefreshDynamicCategoriesAsync().ConfigureAwait(false);
			// update DynamicFields if metadata has changed since last db save
			await RefreshDynamicFieldsAsync().ConfigureAwait(false);
		}
		/// <summary>
		/// Removes those categories that have been removed since the last save, and their fields.
		/// To be sure, we assume the metadata has been changed since the folder was last saved.
		/// </summary>
		/// <returns></returns>
		private async Task RefreshDynamicCategoriesAsync()
		{
			if (MetaBriefcase.OpenInstance == null || MetaBriefcase.OpenInstance.Categories == null) return;

			var availableCats = MetaBriefcase.OpenInstance.Categories;

			var obsoleteDynCats = _dynamicCategories.Where(dynCat => !availableCats.Any(cat => cat.Id == dynCat.CategoryId)).ToList();
			foreach (var obsoleteDynCat in obsoleteDynCats)
			{
				await RemoveDynamicCategory2Async(obsoleteDynCat.CategoryId).ConfigureAwait(false);
			}
		}
		/// <summary>
		/// Removes those fields that have been removed from the category since the last save,
		/// and adds those fields that have been added to the category since the last save.
		/// To be sure, we assume the metadata has been changed since the folder was last saved.
		/// </summary>
		/// <returns></returns>
		private async Task RefreshDynamicFieldsAsync()
		{
			if (MetaBriefcase.OpenInstance == null || MetaBriefcase.OpenInstance.FieldDescriptions == null) return;

			var shouldBeFldDscs = new HashSet<FieldDescription>();
			foreach (var dynCat in _dynamicCategories)
			{
				if (dynCat.Category != null)
				{
					foreach (var fldDsc in dynCat.Category.FieldDescriptions)
					{
						shouldBeFldDscs.Add(fldDsc);
					}
				}
				else
				{
					Debugger.Break();
					// LOLLO TODO the category is empty sometimes. Reloading the folder fixes it, I presume the problem lies in the Get, which is not as clever as the Set,
					// which, in turn, is slow.
					// We don't want to slow down the Get as well, so we live with it for now.
					// The problem has not appeared anymore.
				}
			}
			// remove obsolete fields
			var obsoleteDynFlds = DynamicFields.Where(dynFld => !shouldBeFldDscs.Any(fldDsc => fldDsc.Id == dynFld.FieldDescriptionId));

			foreach (var obsoleteDynFld in obsoleteDynFlds)
			{
				var dbM = _dbManager;
				if (dbM != null && await dbM.DeleteFromDynamicFieldsAsync(obsoleteDynFld).ConfigureAwait(false))
				{
					await RunInUiThreadAsync(delegate
					{
						DynamicFields.Remove(obsoleteDynFld);
					}).ConfigureAwait(false);
				}
			}

			// add new fields
			var newFldDscs = shouldBeFldDscs.Where(fldDsc => !DynamicFields.Any(dynFld => dynFld.FieldDescriptionId == fldDsc.Id));
			foreach (var newFldDsc in newFldDscs)
			{
				var dynFld = new DynamicField(_dbManager) { ParentId = Id, FieldDescriptionId = newFldDsc.Id };
				var dbM = _dbManager;
				if (dbM != null
					&& await dbM.InsertIntoDynamicFieldsAsync(dynFld, true).ConfigureAwait(false))
				{
					await RunInUiThreadAsync(delegate
					{
						DynamicFields.Add(dynFld);
					}).ConfigureAwait(false);
					await dynFld.OpenAsync();
				}
			}
		}
		#endregion loading methods

		#region loaded methods
		public Task<bool> SetDbManager(DBManager dbManager)
		{
			return RunFunctionWhileOpenAsyncA(delegate
			{
				_dbManager = dbManager;
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
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				var wallet = new Wallet(_dbManager);
				return await AddWallet2Async(wallet).ConfigureAwait(false);
			});
		}

		private async Task<bool> AddWallet2Async(Wallet wallet)
		{
			if (wallet != null)
			{
				wallet.ParentId = Id;

				if (Wallet.Check(wallet))
				{
					var dbM = _dbManager;
					if (dbM != null && await dbM.InsertIntoWalletsAsync(wallet, true))
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
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				while (_wallets.Count > 0)
				{
					await _wallets[0].OpenAsync().ConfigureAwait(false);
					await RemoveWallet2Async(_wallets[0]).ConfigureAwait(false);
				}
				return true;
			});
		}
		public Task<bool> RemoveWalletAsync(Wallet wallet)
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				return await RemoveWallet2Async(wallet).ConfigureAwait(false);
			});
		}
		private async Task<bool> RemoveWallet2Async(Wallet wallet)
		{
			if (wallet != null && wallet.ParentId == Id)
			{
				await _dbManager.DeleteFromWalletsAsync(wallet);

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
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				if (!string.IsNullOrWhiteSpace(catId))
				{
					var newDynCat = new DynamicCategory(_dbManager) { ParentId = Id, CategoryId = catId };

					if (Check(newDynCat) && !_dynamicCategories.Any(dc => dc.CategoryId == catId))
					{
						List<DynamicField> newFields = new List<DynamicField>();
						var dbM = _dbManager;
						if (dbM != null && await dbM.InsertIntoDynamicCategoriesAsync(newDynCat, newFields, true))
						{
							_dynamicCategories.Add(newDynCat);
							//DynamicFields.AddRange(newFields);
							await newDynCat.OpenAsync();
							foreach (var field in newFields)
							{
								_dynamicFields.Add(field);
								await field.OpenAsync();
							}
							return true;
						}
					}
				}
				return false;
			});
		}

		public Task<bool> RemoveDynamicCategoryAsync(string catId)
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				return await RemoveDynamicCategory2Async(catId).ConfigureAwait(false);
			});
		}
		private async Task<bool> RemoveDynamicCategory2Async(string catId)
		{
			if (!string.IsNullOrWhiteSpace(catId))
			{
				var dynCat = DynamicCategories.FirstOrDefault(a => a.CategoryId == catId);
				if (dynCat != null)
				{
					var descriptionIdsOfFieldsToBeRemoved = new List<string>();
					var dbM = _dbManager;
					if (dbM != null && await dbM.DeleteFromDynamicCategoriesAsync(dynCat, descriptionIdsOfFieldsToBeRemoved))
					{
						_dynamicCategories.Remove(dynCat);

						await dynCat.CloseAsync();

						foreach (var fieldDescriptionId in descriptionIdsOfFieldsToBeRemoved)
						{
							var fieldToBeRemoved = _dynamicFields.FirstOrDefault(a => a.FieldDescriptionId == fieldDescriptionId);
							if (fieldToBeRemoved != null)
							{
								_dynamicFields.Remove(fieldToBeRemoved);
								await fieldToBeRemoved.CloseAsync().ConfigureAwait(false);
							}
						}
						return true;
					}
				}
			}
			return false;
		}

		public Task<bool> ImportMediaFileIntoNewWalletAsync(StorageFile file, bool copyFile)
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				if (_dbManager != null && file != null)
				{
					var newWallet = new Wallet(_dbManager);
					await newWallet.OpenAsync().ConfigureAwait(false); // open the wallet or the following won't run
					bool isOk = await newWallet.ImportMediaFileAsync(file, copyFile).ConfigureAwait(false)
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
		#endregion loaded methods
	}
}
