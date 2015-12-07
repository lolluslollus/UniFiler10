﻿using SQLite;
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
using Windows.Storage;
using Windows.UI.Core;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public class Document : DbBoundObservableData
	{
		public Document() { }
		public Document(DBManager dbManager)
		{
			_dbManager = dbManager;
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
			else return Path.Combine(_dbManager.Directory.Path, _uri0);
		}
		public string GetFullUri0(StorageFolder directory)
		{
			if (string.IsNullOrWhiteSpace(_uri0) || directory == null) return string.Empty;
			else return Path.Combine(directory.Path, _uri0);
		}
		#endregion properties

		protected override bool UpdateDbMustOverride()
		{
			return _dbManager?.UpdateDocuments(this) == true;
		}

		//protected override bool IsEqualToMustOverride(DbBoundObservableData that)
		//{
		//	var target = that as Document;

		//	return _parentId == that._parentId && // I don't want it for the folder, but I want it for the smaller objects
		//		_uri0 == target._uri0;
		//}

		protected override bool CheckMeMustOverride()
		{
			return _id != DEFAULT_ID && _parentId != DEFAULT_ID;
		}
	}
}
