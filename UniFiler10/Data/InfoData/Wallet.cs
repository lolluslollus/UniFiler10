using SQLite;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public class Wallet : DbBoundObservableData
	{
		public Wallet() { }
		public Wallet(DBManager dbManager)
		{
			_dbManager = dbManager;
		}

		#region properties
		private DBManager _dbManager = null;
		[IgnoreDataMember]
		[Ignore]
		public DBManager DBManager { get { return _dbManager; } set { _dbManager = value; } }

		private string _name = string.Empty;
		[DataMember]
		public string Name { get { return _name; } set { SetProperty(ref _name, value); } }

		private string _descr0 = string.Empty;
		[DataMember]
		public string Descr0 { get { return _descr0; } set { SetProperty(ref _descr0, value); } }

		private DateTime _date0 = default(DateTime);
		[DataMember]
		public DateTime Date0 { get { return _date0; } set { SetProperty(ref _date0, value); } }

		private SwitchableObservableCollection<Document> _documents = new SwitchableObservableCollection<Document>();
		[IgnoreDataMember]
		[Ignore]
		public SwitchableObservableCollection<Document> Documents { get { return _documents; } private set { if (_documents != value) { _documents = value; RaisePropertyChanged_UI(); } } }
		#endregion properties

		protected override async Task OpenMayOverrideAsync()
		{
			var docs = _documents;
			if (docs != null)
			{
				foreach (var doc in docs)
				{
					await doc.OpenAsync().ConfigureAwait(false);
				}
			}
		}
		protected override async Task CloseMayOverrideAsync()
		{
			var docs = _documents;
			if (docs != null)
			{
				foreach (var doc in docs)
				{
					await doc.CloseAsync().ConfigureAwait(false);
					doc.Dispose();
				}

				await RunInUiThreadAsync(delegate
				{
					docs.Clear();
				}).ConfigureAwait(false);
			}
		}
		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_documents?.Dispose();
			_documents = null;

			_dbManager = null;
		}
		protected override bool UpdateDbMustOverride()
		{
			return _dbManager?.UpdateWallets(this) == true;
		}

		//protected override bool IsEqualToMustOverride(DbBoundObservableData that)
		//{
		//	var target = that as Wallet;

		//	return _parentId == that._parentId && // I don't want it for the folder, but I want it for the smaller objects
		//		Date0 == target.Date0 &&
		//		Descr0 == target.Descr0 &&
		//		Name == target.Name &&
		//		Document.AreEqual(Documents, target.Documents);
		//}

		protected override bool CheckMeMustOverride()
		{
			bool result = _id != DEFAULT_ID && _parentId != DEFAULT_ID && _documents != null && Check(_documents);
			return result;
		}

		#region loaded methods
		private async Task<bool> AddDocument2Async(Document doc)
		{
			if (doc != null)
			{
				doc.ParentId = Id;

				if (Document.Check(doc))
				{
					var dbM = _dbManager;
					if (dbM != null && await dbM.InsertIntoDocumentsAsync(doc, true))
					{
						_documents.Add(doc);
						await doc.OpenAsync().ConfigureAwait(false);
						return true;
					}
				}
			}
			return false;
		}
		public Task<bool> AddDocumentAsync()
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				var doc = new Document(_dbManager);
				return await AddDocument2Async(doc).ConfigureAwait(false);
			});
		}
		private async Task<bool> RemoveDocument2Async(Document doc)
		{
			if (doc != null && doc.ParentId == Id)
			{
				await _dbManager.DeleteFromDocumentsAsync(doc);

				int countBefore = _documents.Count;
				await RunInUiThreadAsync(delegate { _documents.Remove(doc); }).ConfigureAwait(false);

				await doc.OpenAsync().ConfigureAwait(false);
				await doc.RemoveContentAsync().ConfigureAwait(false);
				await doc.CloseAsync().ConfigureAwait(false);
				doc.Dispose();

				return _documents.Count < countBefore || _documents.Count == 0;
			}
			return false;
		}

		public Task<bool> RemoveDocumentsAsync()
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				while (_documents.Count > 0)
				{
					await RemoveDocument2Async(_documents[0]).ConfigureAwait(false);
				}
				return true;
			});
		}

		public Task<bool> RemoveDocumentAsync(Document doc)
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				return await RemoveDocument2Async(doc).ConfigureAwait(false);
			});
		}

		public Task<bool> ImportMediaFileAsync(StorageFile file, bool copyFile)
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				if (_dbManager != null && file != null)
				{
					var newDoc = new Document(_dbManager);

					StorageFile newFile = null;
					if (copyFile)
					{
						newFile = await file.CopyAsync(_dbManager.Directory, file.Name, NameCollisionOption.GenerateUniqueName);
						newDoc.Uri0 = Path.GetFileName(newFile.Path);
					}
					else
					{
						newDoc.Uri0 = Path.GetFileName(file.Path);
					}

					if (await AddDocument2Async(newDoc))
					{
						return true;
					}
					else
					{
						if (newFile != null) await newFile.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
					}
				}
				return false;
			});
		}

		#endregion loaded methods
	}
}
