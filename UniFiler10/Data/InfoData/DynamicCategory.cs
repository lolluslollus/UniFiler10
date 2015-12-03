using SQLite;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using UniFiler10.Data.Metadata;
using System;
using Utilz;

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
					UpdateCategory2();
					RaisePropertyChanged_UI();

					Task upd = RunFunctionWhileOpenAsyncA_MT(delegate
					{
						if (DBManager.OpenInstance?.UpdateDynamicCategories(this) == false)
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
			var ins = DBManager.OpenInstance;
			if (ins != null) return ins.UpdateDynamicCategories(this);
			else return false;
		}

		protected override bool IsEqualToMustOverride(DbBoundObservableData that)
		{
			var target = that as DynamicCategory;

			return _category == target._category &&
				_categoryId == target._categoryId;
		}
		protected override bool CheckMeMustOverride()
		{
			return _id != DEFAULT_ID && _parentId != DEFAULT_ID && _categoryId != DEFAULT_ID;
		}
	}
}
