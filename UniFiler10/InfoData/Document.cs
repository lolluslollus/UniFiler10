using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace UniFiler10.Data.Model
{
    [DataContract]
    public class Document : DbBoundObservableData
    {
        #region properties
        private string _uri0 = string.Empty;
        [DataMember]
        public string Uri0 { get { return _uri0; } set { if (_uri0 != value) { _uri0 = value; RaisePropertyChanged_UI(); Task upd = UpdateDbAsync(); } } }

        private bool _isSelected = false;
        [DataMember]
        public bool IsSelected { get { return _isSelected; } set { if (_isSelected != value) { _isSelected = value; RaisePropertyChanged_UI(); Task upd = UpdateDbAsync(); } } }
        #endregion properties

        protected override async Task<bool> UpdateDbMustOverrideAsync()
        {
            if (DBManager.OpenInstance != null) return await DBManager.OpenInstance.UpdateDocumentsAsync(this).ConfigureAwait(false);
            else return false;
        }

        protected override bool IsEqualToMustOverride(DbBoundObservableData that)
        {
            var target = that as Document;

            return Uri0 == target.Uri0;
        }
        protected override void CopyMustOverride(ref DbBoundObservableData target)
        {
            var tgt = target as Document;

            tgt.IsSelected = IsSelected;
            tgt.Uri0 = Uri0;
        }
        protected override bool CheckMeMustOverride()
        {
            return _id != DEFAULT_ID && _parentId != DEFAULT_ID;
        }
    }
}
