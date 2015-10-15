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
    public class Wallet : DbBoundObservableData
    {
        #region properties
        private string _name = string.Empty;
        [DataMember]
        public string Name { get { return _name; } set { if (_name != value) { _name = value; RaisePropertyChanged_UI(); Task upd = UpdateDbAsync(); } } }

        private string _descr0 = string.Empty;
        [DataMember]
        public string Descr0 { get { return _descr0; } set { if (_descr0 != value) { _descr0 = value; RaisePropertyChanged_UI(); Task upd = UpdateDbAsync(); } } }

        private DateTime _date0 = default(DateTime);
        [DataMember]
        public DateTime Date0 { get { return _date0; } set { if (_date0 != value) { _date0 = value; RaisePropertyChanged_UI(); Task upd = UpdateDbAsync(); } } }

        private bool _isSelected = false;
        [DataMember]
        public bool IsSelected { get { return _isSelected; } set { if (_isSelected != value) { _isSelected = value; RaisePropertyChanged_UI(); Task upd = UpdateDbAsync(); } } }

        private SwitchableObservableCollection<Document> _documents = new SwitchableObservableCollection<Document>();
        [IgnoreDataMember]
        [Ignore]
        public SwitchableObservableCollection<Document> Documents { get { return _documents; } set { if (_documents != value) { _documents = value; RaisePropertyChanged_UI(); } } }
        #endregion properties

        protected override async Task OpenMayOverrideAsync()
        {
            if (_documents != null)
            {
                if (_documents.Count == 0)
                {
                    await AddDocument2Async(new Document() { ParentId = Id }).ConfigureAwait(false);
                }
                else
                {
                    foreach (var doc in _documents)
                    {
                        await doc.OpenAsync();
                    }
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
        protected override async Task<bool> UpdateDbMustOverrideAsync()
        {
            if (DBManager.OpenInstance != null) return await DBManager.OpenInstance.UpdateWalletsAsync(this).ConfigureAwait(false);
            else return false;
        }

        protected override bool IsEqualToMustOverride(DbBoundObservableData that)
        {
            var target = that as Wallet;

            return Date0 == target.Date0 &&
                Descr0 == target.Descr0 &&
                Name == target.Name &&
                Document.AreEqual(Documents, target.Documents);
        }

        protected override void CopyMustOverride(ref DbBoundObservableData target)
        {
            var tgt = (target as Wallet);

            tgt.Date0 = Date0;
            tgt.Descr0 = Descr0;
            tgt.IsSelected = IsSelected;
            tgt.Name = Name;
            tgt.Documents = Documents;
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
                doc.ParentId = Id;

                if (Document.Check(doc))
                {
                    if (await DBManager.OpenInstance?.InsertIntoDocumentsAsync(doc, true))
                    {
                        Documents.Add(doc);
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
                if (await DBManager.OpenInstance?.DeleteFromDocumentsAsync(doc))
                {
                    int countBefore = _documents.Count;
                    if (CoreApplication.MainView.Dispatcher.HasThreadAccess) _documents.Remove(doc);
                    else await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate { _documents.Remove(doc); }).AsTask().ConfigureAwait(false);

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
        #endregion loaded methods
    }
}
