using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace UniFiler10.Data.Model
{
    [DataContract]
    public abstract class DbBoundObservableData : OpenableObservableData
    {
        protected static readonly string DEFAULT_ID = string.Empty;

        #region properties
        // SQLite does not like a private set here
        protected string _id = DEFAULT_ID;
        [DataMember]
        [PrimaryKey]
        public string Id { get { return _id; } set { string newValue = value ?? DEFAULT_ID;  if (_id != newValue) { _id = newValue; RaisePropertyChanged_UI(); Task upd = UpdateDbAsync(); } } }

        protected string _parentId = DEFAULT_ID;
        [DataMember]
        [Indexed(Unique = false)]
        public string ParentId { get { return _parentId; } set { string newValue = value ?? DEFAULT_ID; if (_parentId != newValue) { _parentId = newValue; RaisePropertyChanged_UI(); Task upd = UpdateDbAsync(); } } }
        #endregion properties

        #region construct and dispose
        public DbBoundObservableData() : base()
        {
            _id = Guid.NewGuid().ToString(); // LOLLO copying Id from a DBIndex assigned by the DB is tempting,
                                             // but it fails because DbIndex is only set when the record is put into the DB,
                                             // which may be too late. So we get a GUID in the constructor, hoping it doesn't get too slow.
        }
        #endregion construct and dispose

        public async Task CopyAsync(DbBoundObservableData target)
        {
            if (target != null)
            {
                // prevent things happening, just copy the fields one by one
                bool wasEnabled = false;
                if (target._isEnabled)
                {
                    await target.SetIsEnabledAsync(false).ConfigureAwait(false);
                    wasEnabled = true;
                }

                if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
                {
                    CopyUI(target);
                }
                else
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
                    {
                        CopyUI(target);
                    }).AsTask().ConfigureAwait(false);
                }
                // restore IsEnabled if required
                if (wasEnabled)
                {
                    await target.SetIsEnabledAsync(true).ConfigureAwait(false);
                }
            }
        }
        private void CopyUI(DbBoundObservableData target)
        {
            if (target != null)
            {
                target.Id = Id;
                target.ParentId = ParentId;

                CopyMustOverride(ref target);
            }
        }
        protected abstract void CopyMustOverride(ref DbBoundObservableData target);

        public static bool AreEqual(IEnumerable<DbBoundObservableData> one, IEnumerable<DbBoundObservableData> two)
        {
            if (one != null && two != null && one.Count() == two.Count())
            {
                for (int i = 0; i < one.Count(); i++)
                {
                    if (!(one.ElementAt(i).IsEqualTo(two.ElementAt(i)))) return false;
                }
                return true;
            }
            return false;
        }
        public bool IsEqualTo(DbBoundObservableData compTarget)
        {
            if (compTarget != null)
                return
                    //DbIndex == that.DbIndex &&
                    Id == compTarget.Id &&
                    ParentId == compTarget.ParentId &&
                    //IsUpdateDb == that.IsUpdateDb &&
                    IsEqualToMustOverride(compTarget);
            else return false;
        }
        protected abstract bool IsEqualToMustOverride(DbBoundObservableData that);

        protected abstract bool CheckMeMustOverride();
        public static bool Check(DbBoundObservableData item)
        {
            if (item == null) return false;
            else return item.CheckMeMustOverride();
        }
        public static bool Check(IEnumerable<DbBoundObservableData> items)
        {
            if (items == null) return false;
            foreach (var item in items)
            {
                if (!item.CheckMeMustOverride()) return false;
            }
            return true;
        }

        protected Task UpdateDbAsync()
        {
            return RunFunctionWhileOpenAsyncTB(UpdateDbMustOverrideAsync);

            //if (IsOpen && _isEnabled && Binder.OpenInstance != null)
            //{
            //    await Task.Run(() =>
            //    {
            //        // if (Environment.StackTrace.Contains("CustomPropertyImpl")) // coming from the UI
            //        if (IsOpen && _isEnabled && Binder.OpenInstance != null)
            //        {
            //            try
            //            {
            //                _isOpenSemaphore.Wait();
            //                UpdateDbWithinSemaphoreMustOverride();
            //            }
            //            catch (Exception ex)
            //            {
            //                if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
            //                    Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
            //            }
            //            finally
            //            {
            //                _isOpenSemaphore.Release();
            //            }
            //        }
            //    }).ConfigureAwait(false);
            //}
        }
        // protected abstract bool UpdateDbWithinSemaphoreMustOverride();
        protected abstract Task<bool> UpdateDbMustOverrideAsync();
    }
}
