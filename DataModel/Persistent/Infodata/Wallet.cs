﻿using SQLite;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using Utilz;
using Utilz.Data;
using Windows.Storage;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public class Wallet : DbBoundObservableData
	{
		#region lifecycle
		public Wallet() { }
		public Wallet(DBManager dbManager, string parentId) : base()
		{
			DBManager = dbManager;
			ParentId = parentId;
		}
		protected override async Task OpenMayOverrideAsync(object args = null)
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
			//_documents = null;

			_dbManager = null;
		}
		#endregion lifecycle


		#region properties
		private readonly object _dbManagerLocker = new object();
		private DBManager _dbManager = null;
		[IgnoreDataMember]
		[Ignore]
		public DBManager DBManager { get { lock (_dbManagerLocker) { return _dbManager; } } set { lock (_dbManagerLocker) { _dbManager = value; } } }

		private string _name = string.Empty;
		[DataMember]
		public string Name { get { return _name; } set { SetPropertyUpdatingDb(ref _name, value); } }

		private string _descr0 = string.Empty;
		[DataMember]
		public string Descr0 { get { return _descr0; } set { SetPropertyUpdatingDb(ref _descr0, value); } }

		private DateTime _date0 = default(DateTime);
		[DataMember]
		public DateTime Date0 { get { return _date0; } set { SetPropertyUpdatingDb(ref _date0, value); } }

		private readonly SwitchableObservableDisposableCollection<Document> _documents = new SwitchableObservableDisposableCollection<Document>();
		[IgnoreDataMember]
		[Ignore]
		public SwitchableObservableDisposableCollection<Document> Documents { get { return _documents; } }
		#endregion properties


		protected override bool UpdateDbMustOverride()
		{
			return DBManager?.UpdateWallets(this) == true;
		}

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
				if (Document.Check(doc))
				{
					var dbM = DBManager;
					if (dbM != null && await dbM.InsertIntoDocumentsAsync(doc))
					{
						await RunInUiThreadAsync(delegate { _documents.Add(doc); }).ConfigureAwait(false);
						await doc.OpenAsync().ConfigureAwait(false);
						return true;
					}
				}
			}
			return false;
		}
		public async Task<Document> AddDocumentAsync()
		{
			Document newDoc = null;
			if (await RunFunctionIfOpenAsyncTB(delegate
			{
				newDoc = new Document(DBManager, Id);
				return AddDocument2Async(newDoc);
			}).ConfigureAwait(false))
			{
				return newDoc;
			}
			else
			{
				return null;
			}
		}
		private async Task<bool> RemoveDocument2Async(Document doc)
		{
			if (doc != null && doc.ParentId == Id)
			{
				await DBManager.DeleteFromDocumentsAsync(doc);

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
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				var docs = _documents;
				if (docs != null)
				{
					while (docs.Count > 0)
					{
						await RemoveDocument2Async(docs[0]).ConfigureAwait(false);
					}
				}
				return true;
			});
		}

		public Task<bool> RemoveDocumentAsync(Document doc)
		{
			return RunFunctionIfOpenAsyncTB(async () => await RemoveDocument2Async(doc).ConfigureAwait(false));
		}

		public Task<bool> ImportFileAsync(StorageFile file)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				if (DBManager != null && file != null && await file.GetFileSizeAsync() > 0)
				{
					var newDoc = new Document(DBManager, Id);
					newDoc.SetUri0(Path.GetFileName(file.Path));

					return await AddDocument2Async(newDoc).ConfigureAwait(false);
				}
				return false;
			});
		}

		#endregion loaded methods
	}
}
