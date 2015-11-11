using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Model;

namespace UniFiler10.ViewModels
{
    public class DocumentVM : OpenableObservableData
    {
        private Document _document = null;
        public Document Document { get { return _document; } private set { _document = value; RaisePropertyChanged_UI(); } }

        private string _uri = null;
        public string Uri { get { return _uri; } private set { if (_uri!=value) { _uri = value; RaisePropertyChanged_UI(); } } }

        #region construct dispose open close
        public DocumentVM(Document doc)
        {
            if (doc == null) throw new ArgumentNullException("DocumentVM ctor: doc may not be null");

            Document = doc;
            //RuntimeData = RuntimeData.Instance;
            //UpdateCurrentFolderCategories();
            UpdateOpenClose();
            UpdateUri();
        }

        protected override Task OpenMayOverrideAsync()
        {
            _document.PropertyChanged += OnDocument_PropertyChanged;
            return Task.CompletedTask;
        }

        protected override Task CloseMayOverrideAsync()
        {
            if (_document != null) _document.PropertyChanged -= OnDocument_PropertyChanged;
            return Task.CompletedTask;
        }
        private void OnDocument_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Document.IsOpen))
            {
                UpdateOpenClose();
            }
            else if (e.PropertyName == nameof(Document.Uri0))
            {
                UpdateUri();
            }
        }
        private void UpdateOpenClose()
        {
            if (_document.IsOpen)
            {
                Task open = OpenAsync();
            }
            else
            {
                Task close = CloseAsync();
            }
        }
        private void UpdateUri()
        {
            if (!string.IsNullOrWhiteSpace(_document?.Uri0))
            {

            }
        }
        #endregion construct dispose open close
    }


}
