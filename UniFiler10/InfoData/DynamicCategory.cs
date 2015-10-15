using SQLite;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using UniFiler10.Data.Metadata;

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
                if (_categoryId != value)
                {
                    _categoryId = value;
                    RefreshCategory(MetaBriefcase.OpenInstance);
                    RaisePropertyChanged_UI();
                    Task upd = UpdateDbAsync();
                }
                else if (_category == null)
                {
                    RefreshCategory(MetaBriefcase.OpenInstance);
                }
            }
        }
        private void RefreshCategory(MetaBriefcase metaBriefCase)
        {
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

        protected override async Task<bool> UpdateDbMustOverrideAsync()
        {
            if (DBManager.OpenInstance != null) return await DBManager.OpenInstance.UpdateDynamicCategoriesAsync(this).ConfigureAwait(false);
            else return false;
        }

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
        protected override void CopyMustOverride(ref DbBoundObservableData target)
        {
            var tgt = target as DynamicCategory;

            tgt.CategoryId = CategoryId;
        }
    }
}
