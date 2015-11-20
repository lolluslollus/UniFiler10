using SQLite;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using UniFiler10.Data.Metadata;
using System;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public class DynamicCategory : DbBoundObservableData
	{
		#region properties
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
					RaisePropertyChanged_UI();
					UpdateCategory();

					Task upd = RunFunctionWhileOpenAsyncA_MT(delegate
					{
						if (DBManager.OpenInstance?.UpdateDynamicCategories(this) == false)
						{
							_categoryId = oldValue;
							RaisePropertyChanged_UI();
							UpdateCategory();
						}
					});
				}
				else if (_category == null)
				{
					UpdateCategory();
				}
			}
		}
		private void UpdateCategory()
		{
			var metaBriefCase = MetaBriefcase.OpenInstance;
			if (metaBriefCase != null && metaBriefCase.IsOpen && metaBriefCase.Categories != null && !string.IsNullOrEmpty(CategoryId))
			{
				Category = metaBriefCase.Categories.FirstOrDefault(a => a.Id == CategoryId);
			}
			else
			{
				Category = null;
			}
		}
		#endregion properties

		protected override bool UpdateDbMustOverride()
		{
			var ins = DBManager.OpenInstance;
			if (ins != null) return ins.UpdateDynamicCategories(this);
			else return false;
		}
		//protected override async Task<bool> UpdateDbMustOverrideAsync()
		//      {
		//          if (DBManager.OpenInstance != null) return await DBManager.OpenInstance.UpdateDynamicCategoriesAsync(this).ConfigureAwait(false);
		//          else return false;
		//      }

		protected override bool IsEqualToMustOverride(DbBoundObservableData that)
		{
			var target = that as DynamicCategory;

			return Category == target.Category &&
				CategoryId == target.CategoryId;
		}
		protected override bool CheckMeMustOverride()
		{
			return _id != DEFAULT_ID && _parentId != DEFAULT_ID && _categoryId != DEFAULT_ID;
		}
		//protected override void CopyMustOverride(ref DbBoundObservableData target)
		//{
		//    var tgt = target as DynamicCategory;

		//    tgt.CategoryId = CategoryId;
		//}
	}
}
