using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
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
		public string Uri0
		{
			get { return _uri0; }
			set
			{
				string newValue = value == null ? string.Empty : Path.GetFileName(value);
				SetProperty(ref _uri0, newValue);
				//SetProperty(ref _uri0, value ?? string.Empty);
			}
		}
		public string GetFullUri0()
		{
			if (string.IsNullOrWhiteSpace(_uri0)) return string.Empty;
			else return Path.Combine(Binder.OpenInstance?.Directory?.Path, _uri0);
		}
		#endregion properties

		protected override bool UpdateDbMustOverride()
		{
			var ins = DBManager.OpenInstance;
			if (ins != null) return ins.UpdateDocuments(this);
			else return false;
		}

		protected override bool IsEqualToMustOverride(DbBoundObservableData that)
		{
			var target = that as Document;

			return _uri0 == target._uri0;
		}

		protected override bool CheckMeMustOverride()
		{
			return _id != DEFAULT_ID && _parentId != DEFAULT_ID;
		}
	}
}
