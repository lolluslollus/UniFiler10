using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using Utilz;
using Windows.ApplicationModel.Resources.Core;
using Windows.Storage;
using Windows.Storage.Streams;

namespace UniFiler10.Data.Metadata
{
    [DataContract]
    public sealed class MetaBriefcase : OpenableObservableData
    {
        #region properties
        private static volatile MetaBriefcase _instance = null;
        [IgnoreDataMember]
        public static MetaBriefcase OpenInstance { get { if (_instance != null && _instance._isOpen) return _instance; else return null; } }

        //private bool _isEditingCategories = true;
        //[DataMember]
        //public bool IsEditingCategories { get { return _isEditingCategories; } set { if (_isEditingCategories != value) { _isEditingCategories = value; RaisePropertyChanged_UI(); } } }

        private string _currentCategoryId = null;
        [DataMember]
        public string CurrentCategoryId
        {
            get { return _currentCategoryId; }
            set
            {
                if (_currentCategoryId != value)
                {
                    _currentCategoryId = value;
                    RefreshCurrentCategory();
                    RaisePropertyChanged_UI();
                }
                else if (_currentCategory == null)
                {
                    RefreshCurrentCategory();
                }
            }
        }

        private void RefreshCurrentCategory()
        {
            if (_categories != null && _currentCategoryId != null)
            {
                CurrentCategory = _categories.FirstOrDefault(cat => cat.Id == _currentCategoryId);
            }
            else
            {
                CurrentCategory = null;
            }
        }

        private Category _currentCategory = null;
        [IgnoreDataMember]
        public Category CurrentCategory { get { return _currentCategory; } private set { if (_currentCategory != value) { _currentCategory = value; RaisePropertyChanged_UI(); } } }

        private string _currentFieldDescriptionId = null;
        [DataMember]
        public string CurrentFieldDescriptionId
        {
            get { return _currentFieldDescriptionId; }
            set
            {
                if (_currentFieldDescriptionId != value)
                {
                    _currentFieldDescriptionId = value;
                    RefreshCurrentFieldDescription();
                    RaisePropertyChanged_UI();
                }
                else if (_currentFieldDescription == null)
                {
                    RefreshCurrentFieldDescription();
                }
            }
        }
        private void RefreshCurrentFieldDescription()
        {
            if (_fieldDescriptions != null && _currentFieldDescriptionId != null)
            {
                CurrentFieldDescription = _fieldDescriptions.FirstOrDefault(fd => fd.Id == _currentFieldDescriptionId);
            }
            else
            {
                CurrentFieldDescription = null;
            }
        }

        private FieldDescription _currentFieldDescription = null;
        [IgnoreDataMember]
        public FieldDescription CurrentFieldDescription { get { return _currentFieldDescription; } private set { if (_currentFieldDescription != value) { _currentFieldDescription = value; RaisePropertyChanged_UI(); } } }

        private SwitchableObservableCollection<Category> _categories = new SwitchableObservableCollection<Category>();
        [DataMember]
        public SwitchableObservableCollection<Category> Categories { get { return _categories; } private set { _categories = value; RaisePropertyChanged_UI(); } }

        private SwitchableObservableCollection<FieldDescription> _fieldDescriptions = new SwitchableObservableCollection<FieldDescription>();
        [DataMember]
        public SwitchableObservableCollection<FieldDescription> FieldDescriptions { get { return _fieldDescriptions; } private set { _fieldDescriptions = value; RaisePropertyChanged_UI(); } }
        #endregion properties

        #region construct and dispose
        private static readonly object _instanceLock = new object();
        internal static MetaBriefcase CreateInstance()
        {
            lock (_instanceLock)
            {
                if (_instance == null || _instance._isDisposed)
                {
                    _instance = new MetaBriefcase();
                }
                return _instance;
            }
        }
        private MetaBriefcase() { }
        #endregion construct and dispose

        #region open and close
        protected override async Task OpenMayOverrideAsync()
        {
            await LoadAsync().ConfigureAwait(false);
        }
        protected override async Task CloseMayOverrideAsync()
        {
            await Save2Async().ConfigureAwait(false);
        }
        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            _categories?.Dispose();
            _categories = null;

            _fieldDescriptions?.Dispose();
            _fieldDescriptions = null;
        }
        #endregion open and close

        //private async Task LoadAsync()
        //{
        //    var fieldValue00 = new FieldValue() { Vaalue = "2015" };
        //    var fieldValue01 = new FieldValue() { Vaalue = "2016" };
        //    var fieldValue02 = new FieldValue() { Vaalue = "2017" };

        //    var fieldDescription0 = new FieldDescription();
        //    fieldDescription0.Id = "0";
        //    fieldDescription0.Caption = "Saldo for year";
        //    fieldDescription0.PossibleValues = new SwitchableObservableCollection<FieldValue>();
        //    fieldDescription0.PossibleValues.Add(fieldValue00);
        //    fieldDescription0.PossibleValues.Add(fieldValue01);
        //    fieldDescription0.PossibleValues.Add(fieldValue02);


        //    var fieldValue10 = new FieldValue() { Vaalue = "Commerzbank" };
        //    var fieldValue11 = new FieldValue() { Vaalue = "RaboBank" };
        //    var fieldValue12 = new FieldValue() { Vaalue = "Volksbank" };

        //    var fieldDescription1 = new FieldDescription();
        //    fieldDescription1.Id = "1";
        //    fieldDescription1.Caption = "Bank name";
        //    fieldDescription1.PossibleValues = new SwitchableObservableCollection<FieldValue>();
        //    fieldDescription1.PossibleValues.Add(fieldValue10);
        //    fieldDescription1.PossibleValues.Add(fieldValue11);
        //    fieldDescription1.PossibleValues.Add(fieldValue12);


        //    var fieldValue100 = new FieldValue() { Vaalue = "Ming" };
        //    var fieldValue101 = new FieldValue() { Vaalue = "Chan" };
        //    var fieldValue102 = new FieldValue() { Vaalue = "Li" };

        //    var fieldDescription2 = new FieldDescription();
        //    fieldDescription2.Id = "2";
        //    fieldDescription2.Caption = "Dynasty";
        //    fieldDescription2.PossibleValues = new SwitchableObservableCollection<FieldValue>();
        //    fieldDescription2.PossibleValues.Add(fieldValue100);
        //    fieldDescription2.PossibleValues.Add(fieldValue101);
        //    fieldDescription2.PossibleValues.Add(fieldValue102);

        //    FieldDescriptions.Add(fieldDescription0);
        //    FieldDescriptions.Add(fieldDescription1);
        //    FieldDescriptions.Add(fieldDescription2);

        //    var category0 = new Category();
        //    category0.Id = "0";
        //    category0.Name = "Banks";
        //    category0.FieldDescriptions = new SwitchableObservableCollection<FieldDescription>();
        //    category0.FieldDescriptions.Add(fieldDescription0);
        //    category0.FieldDescriptions.Add(fieldDescription1);

        //    var category1 = new Category();
        //    category1.Id = "1";
        //    category1.Name = "Vases";
        //    category1.FieldDescriptions = new SwitchableObservableCollection<FieldDescription>();
        //    category1.FieldDescriptions.Add(fieldDescription1);
        //    category1.FieldDescriptions.Add(fieldDescription2);

        //    Categories.Add(category0);
        //    Categories.Add(category1);
        //}
        #region loading methods
        private const string FILENAME = "LolloSessionDataMetaBriefcase.xml";
        private async Task LoadAsync()
        {
            string errorMessage = string.Empty;
            MetaBriefcase newMetaBriefcase = null;

            try
            {
                StorageFile file = await ApplicationData.Current.LocalFolder
                    .CreateFileAsync(FILENAME, CreationCollisionOption.OpenIfExists)
                    .AsTask().ConfigureAwait(false);

                //String ssss = null; //this is useful when you debug and want to see the file as a string
                //using (IInputStream inStream = await file.OpenSequentialReadAsync())
                //{
                //    using (StreamReader streamReader = new StreamReader(inStream.AsStreamForRead()))
                //    {
                //      ssss = streamReader.ReadToEnd();
                //    }
                //}

                using (IInputStream inStream = await file.OpenSequentialReadAsync().AsTask().ConfigureAwait(false))
                {
                    using (var iinStream = inStream.AsStreamForRead())
                    {
                        DataContractSerializer serializer = new DataContractSerializer(typeof(MetaBriefcase));
                        iinStream.Position = 0;
                        newMetaBriefcase = (MetaBriefcase)(serializer.ReadObject(iinStream));
                        await iinStream.FlushAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (FileNotFoundException ex) //ignore file not found, this may be the first run just after installing
            {
                errorMessage = "starting afresh";
                await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename);
            }
            catch (Exception ex)                 //must be tolerant or the app might crash when starting
            {
                errorMessage = "could not restore the data, starting afresh";
                await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename);
            }
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                if (newMetaBriefcase != null) Copy(newMetaBriefcase);
            }

            Debug.WriteLine("ended method MetaBriefcase.LoadAsync()");
        }
        private async Task Save2Async()
        {
            MetaBriefcase metaBriefcaseClone = Clone();
            //for (int i = 0; i < 100000000; i++) //wait a few seconds, for testing
            //{
            //    String aaa = i.ToString();
            //}

            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    DataContractSerializer sessionDataSerializer = new DataContractSerializer(typeof(MetaBriefcase));
                    sessionDataSerializer.WriteObject(memoryStream, metaBriefcaseClone);

                    var file = await ApplicationData.Current.LocalFolder
                        .CreateFileAsync(FILENAME, CreationCollisionOption.ReplaceExisting)
                        .AsTask().ConfigureAwait(false);
                    using (Stream fileStream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
                    {
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        await memoryStream.CopyToAsync(fileStream).ConfigureAwait(false);
                        await memoryStream.FlushAsync().ConfigureAwait(false);
                        await fileStream.FlushAsync().ConfigureAwait(false);
                    }
                }
                Debug.WriteLine("ended method MetaBriefcase.SaveAsync()");
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.FileErrorLogFilename);
            }
        }
        private bool Copy(MetaBriefcase source)
        {
            if (source == null) return false;

            //IsEditingCategories = source.IsEditingCategories;

            FieldDescription.Copy(source.FieldDescriptions, FieldDescriptions);
            RaisePropertyChanged_UI(nameof(FieldDescriptions));
            Category.Copy(source.Categories, Categories, FieldDescriptions);
            RaisePropertyChanged_UI(nameof(Categories));
            CurrentCategoryId = source.CurrentCategoryId; // must come after setting the categories
            CurrentFieldDescriptionId = source.CurrentFieldDescriptionId;

            return true;
        }
        private MetaBriefcase Clone()
        {
            // LOLLO it may seem that these Clone...() methods should run under a semaphore.
            // However, they are only called when IsOpen == false, so we are good.
            MetaBriefcase target = new MetaBriefcase();

            //target.IsEditingCategories = _isEditingCategories;
            target.CurrentCategoryId = _currentCategoryId;
            target.CurrentFieldDescriptionId = _currentFieldDescriptionId;
            target.Categories = _categories;
            target.FieldDescriptions = _fieldDescriptions;

            return target;
        }
        #endregion loading methods

        #region loaded methods
        public Task<bool> AddCategoryAsync()
        {
            return RunFunctionWhileOpenAsyncB(delegate
            {
                string name = ResourceManager.Current.MainResourceMap
                    .GetValue("Resources/NewCategory/Text", ResourceContext.GetForCurrentView()).ValueAsString;
                var newCat = new Category() { Name = name, IsCustom = true, IsJustAdded = true };

                if (Category.Check(newCat) && !Categories.Any(cat => cat.Name == newCat.Name || cat.Id == newCat.Id))
                {
                    Categories.Add(newCat);
                    return true;
                }
                return false;
            });
        }
        public Task<bool> RemoveCategoryAsync(Category cat)
        {
            return RunFunctionWhileOpenAsyncB(delegate
            {
                if (cat != null && cat.IsJustAdded)
                {
                    return Categories.Remove(cat);
                }
                else
                {
                    return false;
                }
            });
        }

        public Task<bool> AddFieldDescriptionAsync()
        {
            return RunFunctionWhileOpenAsyncB(delegate
            {
                string name = ResourceManager.Current.MainResourceMap
                    .GetValue("Resources/NewFieldDescription/Text", ResourceContext.GetForCurrentView()).ValueAsString;
                var newFieldDesc = new FieldDescription() { Caption = name, IsCustom = true, IsJustAdded = true };

                if (FieldDescription.Check(newFieldDesc) && !FieldDescriptions.Any(fd => fd.Caption == newFieldDesc.Caption || fd.Id == newFieldDesc.Id))
                {
                    FieldDescriptions.Add(newFieldDesc);
                    return true;
                }
                return false;
            });
        }

        public Task<bool> RemoveFieldDescription(FieldDescription fldDesc)
        {
            return RunFunctionWhileOpenAsyncB(delegate
            {
                if (fldDesc != null && fldDesc.IsJustAdded)
                {
                    foreach (var cat in _categories)
                    {
                        cat.RemoveFieldDescription(fldDesc);
                    }
                    return FieldDescriptions.Remove(fldDesc);
                }
                else return false;
            });
        }
        public Task SaveAsync()
        {
            return RunFunctionWhileOpenAsyncT(Save2Async);
        }
        #endregion loaded methods
    }
}
