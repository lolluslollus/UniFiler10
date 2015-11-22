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
        public string _uri0 = string.Empty;
        [DataMember]
        public string Uri0 { get { return _uri0; } set { SetProperty(ref _uri0, value); } }

		public bool _isSelected = false;
        [DataMember]
        public bool IsSelected { get { return _isSelected; } set { SetProperty(ref _isSelected, value); } }
		#endregion properties

		protected override bool UpdateDbMustOverride()
		{
			var ins = DBManager.OpenInstance;
			if (ins != null) return ins.UpdateDocuments(this);
			else return false;
		}
		//protected override async Task<bool> UpdateDbMustOverrideAsync()
  //      {
  //          if (DBManager.OpenInstance != null) return await DBManager.OpenInstance.UpdateDocumentsAsync(this).ConfigureAwait(false);
  //          else return false;
  //      }

        protected override bool IsEqualToMustOverride(DbBoundObservableData that)
        {
            var target = that as Document;

            return Uri0 == target.Uri0;
        }
        //protected override void CopyMustOverride(ref DbBoundObservableData target)
        //{
        //    var tgt = target as Document;

        //    tgt.IsSelected = IsSelected;
        //    tgt.Uri0 = Uri0;
        //}
        protected override bool CheckMeMustOverride()
        {
            return _id != DEFAULT_ID && _parentId != DEFAULT_ID;
        }
    }
}
