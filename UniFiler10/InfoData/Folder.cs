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

namespace UniFiler10.Data.Model
{
	[DataContract]
	public sealed class Folder : DbBoundObservableData
	{
		public Folder() { }

		#region properties
		private SwitchableObservableCollection<DynamicCategory> _dynamicCategories = new SwitchableObservableCollection<DynamicCategory>();
		[IgnoreDataMember]
		[Ignore]
		public SwitchableObservableCollection<DynamicCategory> DynamicCategories { get { return _dynamicCategories; } set { if (_dynamicCategories != value) { _dynamicCategories = value; RaisePropertyChanged_UI(); } } }

		private SwitchableObservableCollection<DynamicField> _dynamicFields = new SwitchableObservableCollection<DynamicField>();
		[IgnoreDataMember]
		[Ignore]
		public SwitchableObservableCollection<DynamicField> DynamicFields { get { return _dynamicFields; } set { if (_dynamicFields != value) { _dynamicFields = value; RaisePropertyChanged_UI(); } } }

		public string _name = string.Empty;
		[DataMember]
		public string Name { get { return _name; } set { SetProperty(ref _name, value); } }

		public string _descr0 = string.Empty;
		[DataMember]
		public string Descr0 { get { return _descr0; } set { SetProperty(ref _descr0, value); } }

		public string _descr1 = string.Empty;
		[DataMember]
		public string Descr1 { get { return _descr1; } set { SetProperty(ref _descr1, value); } }

		public string _descr2 = string.Empty;
		[DataMember]
		public string Descr2 { get { return _descr2; } set { SetProperty(ref _descr2, value); } }

		public string _descr3 = string.Empty;
		[DataMember]
		public string Descr3 { get { return _descr3; } set { SetProperty(ref _descr3, value); } }

		public DateTime _dateCreated = default(DateTime);
		[DataMember]
		public DateTime DateCreated { get { return _dateCreated; } set { SetProperty(ref _dateCreated, value); } }

		public DateTime _date0 = default(DateTime);
		[DataMember]
		public DateTime Date0 { get { return _date0; } set { SetProperty(ref _date0, value); } }

		public DateTime _date1 = default(DateTime);
		[DataMember]
		public DateTime Date1 { get { return _date1; } set { SetProperty(ref _date1, value); } }

		public DateTime _date2 = default(DateTime);
		[DataMember]
		public DateTime Date2 { get { return _date2; } set { SetProperty(ref _date2, value); } }

		public DateTime _date3 = default(DateTime);
		[DataMember]
		public DateTime Date3 { get { return _date3; } set { SetProperty(ref _date3, value); } }

		private SwitchableObservableCollection<Wallet> _wallets = new SwitchableObservableCollection<Wallet>();
		[IgnoreDataMember]
		[Ignore]
		public SwitchableObservableCollection<Wallet> Wallets { get { return _wallets; } private set { if (_wallets != value) { _wallets = value; RaisePropertyChanged_UI(); } } }

		public bool _isSelected = false;
		[DataMember]
		public bool IsSelected { get { return _isSelected; } set { SetProperty(ref _isSelected, value, true, false); } }

		public bool _isEditingCategories = true;
		[DataMember]
		public bool IsEditingCategories { get { return _isEditingCategories; } set { SetProperty(ref _isEditingCategories, value); } }
		#endregion properties

		protected override bool UpdateDbMustOverride()
		{
			var ins = DBManager.OpenInstance;
			if (ins != null) return ins.UpdateFolders(this);
			else return false;
		}

		protected override bool IsEqualToMustOverride(DbBoundObservableData that)
		{
			var target = that as Folder;

			return
				DateCreated == target.DateCreated &&
				Date0 == target.Date0 &&
				Date1 == target.Date1 &&
				Date2 == target.Date2 &&
				Date3 == target.Date3 &&
				Descr0 == target.Descr0 &&
				Descr1 == target.Descr1 &&
				Descr2 == target.Descr2 &&
				Descr3 == target.Descr3 &&
				//IsSelected == target.IsSelected &&
				Name == target.Name &&
				Wallet.AreEqual(Wallets, target.Wallets) &&
				DynamicCategory.AreEqual(DynamicCategories, target.DynamicCategories) &&
				DynamicField.AreEqual(DynamicFields, target.DynamicFields);
		}

		protected override bool CheckMeMustOverride()
		{
			bool result = _id != DEFAULT_ID && Check(_wallets) && Check(_dynamicCategories) && Check(_dynamicFields);
			return result;
		}

		#region loading methods
		//private DBManager _dbManager = null;
		//public async Task OpenAsync(DBManager dbManager)
		//{
		//	_dbManager = dbManager;
		//	await OpenAsync().ConfigureAwait(false);
		//}
		protected override async Task OpenMayOverrideAsync()
		{
			var _dbManager = DBManager.OpenInstance; if (_dbManager == null) throw new Exception("Folder.OpenMayOverrideAsync found no open instances of DBManager");

			// read children from db
			var wallets = await _dbManager.GetWalletsAsync(Id).ConfigureAwait(false);
			var documents = new List<Document>();
			foreach (var wallet in wallets)
			{
				documents.AddRange(await _dbManager.GetDocumentsAsync(wallet.Id).ConfigureAwait(false));
			}

			var dynamicFields = await _dbManager.GetDynamicFieldsAsync(Id).ConfigureAwait(false);

			var dynamicCategories = await _dbManager.GetDynamicCategoriesAsync(Id).ConfigureAwait(false);

			// populate my collections
			await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
			{
				_wallets.Clear();
				_wallets.AddRange(wallets);
				foreach (var wallet in _wallets)
				{
					wallet.Documents.Clear();
					wallet.Documents.AddRange(documents.Where(a => a.ParentId == wallet.Id));
				}

				_dynamicFields.Clear();
				_dynamicFields.AddRange(dynamicFields);

				_dynamicCategories.Clear();
				_dynamicCategories.AddRange(dynamicCategories);
			}).AsTask().ConfigureAwait(false);

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
			if (_wallets != null)
			{
				foreach (var wallet in _wallets)
				{
					await wallet.CloseAsync().ConfigureAwait(false);
				}
			}
			if (_dynamicFields != null)
			{
				foreach (var dynFld in _dynamicFields)
				{
					await dynFld.CloseAsync().ConfigureAwait(false);
				}
			}
			if (_dynamicCategories != null)
			{
				foreach (var dynCat in _dynamicCategories)
				{
					await dynCat.CloseAsync().ConfigureAwait(false);
				}
			}
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
				if (DBManager.OpenInstance != null
					&& await DBManager.OpenInstance.DeleteFromDynamicFieldsAsync(obsoleteDynFld).ConfigureAwait(false))
				{
					await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
					{
						DynamicFields.Remove(obsoleteDynFld);
					}).AsTask().ConfigureAwait(false);
				}
			}

			// add new fields
			var newFldDscs = shouldBeFldDscs.Where(fldDsc => !DynamicFields.Any(dynFld => dynFld.FieldDescriptionId == fldDsc.Id));
			foreach (var newFldDsc in newFldDscs)
			{
				var dynFld = new DynamicField() { ParentId = Id, FieldDescriptionId = newFldDsc.Id };
				if (DBManager.OpenInstance != null
					&& await DBManager.OpenInstance.InsertIntoDynamicFieldsAsync(dynFld, true).ConfigureAwait(false))
				{
					await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
					{
						DynamicFields.Add(dynFld);
					}).AsTask().ConfigureAwait(false);
					await dynFld.OpenAsync();
				}
			}
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
		}
		#endregion loading methods

		#region loaded methods
		public Task<bool> AddWalletAsync(Wallet wallet)
		{
			return RunFunctionWhileEnabledAsyncTB(async delegate
			{
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
					var dbM = DBManager.OpenInstance;
					if (dbM != null && await dbM.InsertIntoWalletsAsync(wallet, true))
					{
						RunInUiThread(delegate { _wallets.Add(wallet); });

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

		public Task<bool> RemoveWalletAsync(Wallet wallet)
		{
			return RunFunctionWhileEnabledAsyncTB(async delegate
			{
				return await RemoveWallet2Async(wallet).ConfigureAwait(false);
			});
		}
		private async Task<bool> RemoveWallet2Async(Wallet wallet)
		{
			if (wallet != null && wallet.ParentId == Id)
			{
				var dbM = DBManager.OpenInstance;
				if (dbM != null)
				{
					await dbM.DeleteFromWalletsAsync(wallet);

					RunInUiThread(delegate { _wallets.Remove(wallet); });

					return await wallet.RemoveAllDocumentsAsync().ConfigureAwait(false)
						& await wallet.CloseAsync().ConfigureAwait(false);
				}
			}
			return false;
		}

		public Task<bool> AddDynamicCategoryAsync(string catId)
		{
			return RunFunctionWhileEnabledAsyncTB(async delegate
			{
				if (!string.IsNullOrWhiteSpace(catId))
				{
					var newDynCat = new DynamicCategory();
					newDynCat.ParentId = Id;
					newDynCat.CategoryId = catId;

					if (Check(newDynCat) && !_dynamicCategories.Any(dc => dc.CategoryId == catId))
					{
						List<DynamicField> newFields = new List<DynamicField>();
						var dbM = DBManager.OpenInstance;
						if (dbM != null && await dbM.InsertIntoDynamicCategoriesAsync(newDynCat, newFields, true))
						{
							DynamicCategories.Add(newDynCat);
							//DynamicFields.AddRange(newFields);
							await newDynCat.OpenAsync();
							foreach (var field in newFields)
							{
								DynamicFields.Add(field);
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
			return RunFunctionWhileEnabledAsyncTB(async delegate
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
					var dbM = DBManager.OpenInstance;
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
			return RunFunctionWhileEnabledAsyncTB(async delegate
			{
				if (Binder.OpenInstance != null && file != null)
				{
					var newWallet = new Wallet();
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
