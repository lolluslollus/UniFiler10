using SQLite;
using System;
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
		#region properties
		public string _name = string.Empty;
        [DataMember]
        public string Name { get { return _name; } set { SetProperty(ref _name, value); } }

		public string _descr0 = string.Empty;
        [DataMember]
        public string Descr0 { get { return _descr0; } set { SetProperty(ref _descr0, value); } }

		public DateTime _date0 = default(DateTime);
        [DataMember]
        public DateTime Date0 { get { return _date0; } set { SetProperty(ref _date0, value); } }

		public bool _isSelected = false;
        [DataMember]
        public bool IsSelected { get { return _isSelected; } set { SetProperty(ref _isSelected, value); } }

        //private bool _isRecordingSound = false;
        //[IgnoreDataMember]
        //[Ignore]
        //public bool IsRecordingSound { get { return _isRecordingSound; } set { if (_isRecordingSound != value) { _isRecordingSound = value; RaisePropertyChanged_UI(); } } }

        private SwitchableObservableCollection<Document> _documents = new SwitchableObservableCollection<Document>();
        [IgnoreDataMember]
        [Ignore]
        public SwitchableObservableCollection<Document> Documents { get { return _documents; } private set { if (_documents != value) { _documents = value; RaisePropertyChanged_UI(); } } }
        #endregion properties

        protected override async Task OpenMayOverrideAsync()
        {
            if (_documents != null)
            {
                foreach (var doc in _documents)
                {
                    await doc.OpenAsync();
                }
            }
        }
        protected override async Task CloseMayOverrideAsync()
        {
            if (_documents != null)
            {
                foreach (var doc in _documents)
                {
                    await doc.CloseAsync().ConfigureAwait(false);
                }
            }
        }
        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            _documents?.Dispose();
            _documents = null;
        }
		protected override bool UpdateDbMustOverride()
		{
			var ins = DBManager.OpenInstance;
			bool output = false;
			if (ins != null) output = ins.UpdateWallets(this);
			return output;
		}
		//protected override async Task<bool> UpdateDbMustOverrideAsync()
  //      {
  //          if (DBManager.OpenInstance != null) return await DBManager.OpenInstance.UpdateWalletsAsync(this).ConfigureAwait(false);
  //          else return false;
  //      }

        protected override bool IsEqualToMustOverride(DbBoundObservableData that)
        {
            var target = that as Wallet;

            return Date0 == target.Date0 &&
                Descr0 == target.Descr0 &&
                Name == target.Name &&
                Document.AreEqual(Documents, target.Documents);
        }

        //protected override void CopyMustOverride(ref DbBoundObservableData target)
        //{
        //    var tgt = (target as Wallet);

        //    tgt.Date0 = Date0;
        //    tgt.Descr0 = Descr0;
        //    tgt.IsSelected = IsSelected;
        //    tgt.Name = Name;
        //    tgt.Documents = Documents;
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
					var dbM = DBManager.OpenInstance;
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
        public Task<bool> AddDocumentAsync(Document doc)
        {
            return RunFunctionWhileOpenAsyncTB(async delegate
            {
                return await AddDocument2Async(doc).ConfigureAwait(false);
            });
        }
        private async Task<bool> RemoveDocument2Async(Document doc)
        {
            if (doc != null && doc.ParentId == Id)
            {
				var dbM = DBManager.OpenInstance;
				if (dbM != null)
				{
					await dbM.DeleteFromDocumentsAsync(doc);

					int countBefore = _documents.Count;
					RunInUiThread(delegate { _documents.Remove(doc); });

					try
					{
						if (!string.IsNullOrWhiteSpace(doc.Uri0))
						{
							var file = await StorageFile.GetFileFromPathAsync(doc.Uri0).AsTask().ConfigureAwait(false);
							if (file != null) await file.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
						}
					}
					catch (Exception ex)
					{
						await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename);
					}

					await doc.CloseAsync().ConfigureAwait(false);
					return _documents.Count < countBefore;
				}
            }
            return false;
        }
        public Task<bool> RemoveDocumentAsync(Document doc)
        {
            return RunFunctionWhileOpenAsyncTB(async delegate
            {
                return await RemoveDocument2Async(doc).ConfigureAwait(false);
            });
        }
        public Task<bool> RemoveAllDocumentsAsync()
        {
            return RunFunctionWhileOpenAsyncTB(async delegate
            {
                bool isOk = true;
                while (_documents.Count > 0) // do not use foreach to avoid error with enumeration
                {
                    var doc = _documents[0];
                    isOk = isOk & await RemoveDocument2Async(doc).ConfigureAwait(false);
                }
                return isOk;
            });
        }

        public Task<bool> ImportMediaFileAsync(StorageFile file, bool copyFile)
        {
            return RunFunctionWhileOpenAsyncTB(async delegate
            {
                if (Binder.OpenInstance != null && file != null)
                {
                    var newDocument = new Document();

                    StorageFile newFile = null;
                    if (copyFile)
                    {
                        var directory = await Binder.OpenInstance.GetDirectoryAsync();
                        newFile = await file.CopyAsync(directory, file.Name, NameCollisionOption.GenerateUniqueName);
                        newDocument.Uri0 = newFile.Path;
                    }
                    else
                    {
                        newDocument.Uri0 = file.Path;
                    }

                    if (await AddDocument2Async(newDocument))
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
