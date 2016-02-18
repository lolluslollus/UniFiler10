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
using Utilz.Data;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public class Document : DbBoundObservableData
	{
		public Document() { }
		public Document(DBManager dbManager, string parentId) : base()
		{
			DBManager = dbManager;
			ParentId = parentId;
		}
		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_dbManager = null;
		}

		#region properties
		private readonly object _dbManagerLocker = new object();
		private DBManager _dbManager = null;
		[IgnoreDataMember]
		[Ignore]
		public DBManager DBManager { get { lock (_dbManagerLocker) { return _dbManager; } } set { lock (_dbManagerLocker) { _dbManager = value; } } }

		private readonly object _uri0Locker = new object();
		private string _uri0 = string.Empty;
		[DataMember]
		public string Uri0
		{
			get
			{
				return GetPropertyLocking(ref _uri0, _uri0Locker);
				//lock (_uri0Locker)
				//{
				//	return _uri0;
				//}
			}
			set // this lockless setter is only for the serialiser and the db
			{
				string newValue = value == null ? string.Empty : Path.GetFileName(value);
				SetPropertyUpdatingDb(ref _uri0, newValue);
			}
		}
		public void SetUri0(string newValue)
		{
			string okValue = newValue == null ? string.Empty : Path.GetFileName(newValue);
			SetPropertyLockingUpdatingDb(ref _uri0, okValue, _uri0Locker);
		}
		public string GetFullUri0()
		{
			if (string.IsNullOrWhiteSpace(Uri0)) return string.Empty;
			else
			{
				var dbM = DBManager;
				if (dbM != null)
				{
					return Path.Combine(dbM.Directory.Path, Uri0);
				}
				else
				{
					return string.Empty;
				}
			}
		}
		public string GetFullUri0(StorageFolder directory)
		{
			if (string.IsNullOrWhiteSpace(Uri0) || directory == null) return string.Empty;
			else return Path.Combine(directory.Path, Uri0);
		}
		#endregion properties


		protected override bool UpdateDbMustOverride()
		{
			return DBManager?.UpdateDocuments(this) == true;
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


		#region while open methods
		public Task<bool> RemoveContentAsync()
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				try
				{
					if (!string.IsNullOrWhiteSpace(Uri0))
					{
						var file = await StorageFile.GetFileFromPathAsync(GetFullUri0()).AsTask().ConfigureAwait(false);
						if (file != null) await file.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
					}
					return true;
				}
				catch (Exception ex)
				{
					await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
				}
				return false;
			});
		}
		#endregion while open methods
	}
}
