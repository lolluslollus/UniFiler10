using SQLite;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using UniFiler10.Data.Metadata;
using System;
using Utilz;
using Utilz.Data;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public class DynamicCategory : DbBoundObservableData
	{
		public DynamicCategory() { }
		public DynamicCategory(DBManager dbManager, string parentId, string categoryId)
		{
			_dbManager = dbManager;
			ParentId = parentId;
			CategoryId = categoryId;
		}
		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_dbManager = null;
		}


		#region properties
		private DBManager _dbManager = null;
		[IgnoreDataMember]
		[Ignore]
		public DBManager DBManager { get { return _dbManager; } set { _dbManager = value; } }

		private Category _category = null;
		[IgnoreDataMember]
		[Ignore]
		public Category Category { get { return _category; } private set { if (_category != value) { _category = value; RaisePropertyChanged_UI(); } } }

		private string _categoryId = DEFAULT_ID;
		[DataMember]
		public string CategoryId
		{
			get { return _categoryId; }
			set
			{
				string newValue = value ?? DEFAULT_ID;
				string oldValue = _categoryId;
				if (newValue != oldValue)
				{
					_categoryId = newValue;
					UpdateCategory2();
					RaisePropertyChanged_UI();

					Task upd = RunFunctionIfOpenAsyncA_MT(delegate
					{
						if (_dbManager?.UpdateDynamicCategories(this) == false)
						{
							//_categoryId = oldValue;
							//UpdateCategory2();
							//RaisePropertyChanged_UI();
							Logger.Add_TPL(GetType().ToString() + "." + nameof(CategoryId) + " could not be set", Logger.ForegroundLogFilename);
						}
					});
				}
				else if (_category == null)
				{
					UpdateCategory2();
				}
			}
		}
		private void UpdateCategory2()
		{
			var mbf = MetaBriefcase.OpenInstance;
			if (mbf != null && mbf.Categories != null && !string.IsNullOrEmpty(_categoryId))
			{
				Category = mbf.Categories.FirstOrDefault(a => a.Id == _categoryId);
			}
			else
			{
				Category = null;
			}
		}
		#endregion properties


		protected override bool UpdateDbMustOverride()
		{
			return _dbManager?.UpdateDynamicCategories(this) == true;
		}

		//protected override bool IsEqualToMustOverride(DbBoundObservableData that)
		//{
		//	var target = that as DynamicCategory;

		//	return _parentId == that._parentId && // I don't want it for the folder, but I want it for the smaller objects
		//		_category == target._category &&
		//		_categoryId == target._categoryId;
		//}
		protected override bool CheckMeMustOverride()
		{
			return _id != DEFAULT_ID && _parentId != DEFAULT_ID && _categoryId != DEFAULT_ID;
		}
	}
}
