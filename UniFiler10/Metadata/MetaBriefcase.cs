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
        public static MetaBriefcase OpenInstance { get { var instance = _instance; if (instance != null && instance._isOpen) return instance; else return null; } }

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
                    UpdateCurrentCategory();
                    RaisePropertyChanged_UI();
                }
                else if (_currentCategory == null)
                {
                    UpdateCurrentCategory();
                }
            }
        }

        private void UpdateCurrentCategory()
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
                    UpdateCurrentFieldDescription();
                    RaisePropertyChanged_UI();
                }
                else if (_currentFieldDescription == null)
                {
                    UpdateCurrentFieldDescription();
                }
            }
        }
        private void UpdateCurrentFieldDescription()
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

		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_categories?.Dispose();
			_categories = null;

			_fieldDescriptions?.Dispose();
			_fieldDescriptions = null;
		}
		#endregion construct and dispose

		#region open and close
		protected override async Task OpenMayOverrideAsync()
        {
            await LoadAsync().ConfigureAwait(false);
        }
        protected override async Task CloseMayOverrideAsync()
        {
            await SaveAsync().ConfigureAwait(false);
        }
        #endregion open and close


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
        public async Task SaveAsync()
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
            return RunFunctionWhileEnabledAsyncB(delegate
            {
                string name = ResourceManager.Current.MainResourceMap
                    .GetValue("Resources/NewCategory/Text", ResourceContext.GetForCurrentView()).ValueAsString;
                var newCat = new Category() { Name = name, IsCustom = true, IsJustAdded = true };

                if (Category.Check(newCat) && !Categories.Any(cat => cat.Name == newCat.Name || cat.Id == newCat.Id))
                {
                    _categories.Add(newCat);
                    return true;
                }
                return false;
            });
        }
        public Task<bool> RemoveCategoryAsync(Category cat)
        {
            return RunFunctionWhileEnabledAsyncB(delegate
            {
                if (cat != null && cat.IsJustAdded)
                {
                    return _categories.Remove(cat);
                }
                else
                {
                    return false;
                }
            });
        }

        public Task<bool> AddFieldDescriptionAsync()
        {
            return RunFunctionWhileEnabledAsyncB(delegate
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
            return RunFunctionWhileEnabledAsyncB(delegate
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
        #endregion loaded methods
    }
}
