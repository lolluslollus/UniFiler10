using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Utilz;

namespace UniFiler10.Data.Metadata
{
    [DataContract]
    public sealed class Category : ObservableData
    {
        private static readonly string DEFAULT_ID = string.Empty;

        #region properties
        private string _id = DEFAULT_ID;
        [DataMember]
        public string Id { get { return _id; } set { _id = value; RaisePropertyChanged_UI(); } }

        private string _name = string.Empty;
        [DataMember]
        public string Name { get { return _name; } set { _name = value; RaisePropertyChanged_UI(); } }

        private bool _isCustom = false;
        [DataMember]
        public bool IsCustom { get { return _isCustom; } set { _isCustom = value; RaisePropertyChanged_UI(); } }

        private bool _isJustAdded = false;
        [IgnoreDataMember]
        public bool IsJustAdded { get { return _isJustAdded; } set { _isJustAdded = value; RaisePropertyChanged_UI(); } }

        private SwitchableObservableCollection<FieldDescription> _fieldDescriptions = new SwitchableObservableCollection<FieldDescription>();
        [DataMember]
        public SwitchableObservableCollection<FieldDescription> FieldDescriptions { get { return _fieldDescriptions; } set { _fieldDescriptions = value; RaisePropertyChanged_UI(); } }
        #endregion properties

        public static void Copy(Category source, ref Category target)
        {
            if (source != null && target != null)
            {
                FieldDescription.Copy(source.FieldDescriptions, target.FieldDescriptions);
                target.Id = source.Id;
                target.IsCustom = source.IsCustom;
                // target.IsJustAdded = source.IsJustAdded; // we don't actually need this
                target.Name = source.Name;
            }
        }
        public static void Copy(SwitchableObservableCollection<Category> source, SwitchableObservableCollection<Category> target)
        {
            if (source != null && target != null)
            {
                target.IsObserving = false;
                foreach (var sourceRecord in source)
                {
                    var targetRecord = new Category();
                    Copy(sourceRecord, ref targetRecord);
                    target.Add(targetRecord);
                }
                target.IsObserving = true;
            }
        }

        public Category()
        {
            Id = Guid.NewGuid().ToString();
        }

        public bool AddFieldDescription(FieldDescription newFldDsc)
        {
            if (newFldDsc != null && !FieldDescriptions.Any(fds => fds.Caption == newFldDsc.Caption || fds.Id == newFldDsc.Id))
            {
                FieldDescriptions.Add(newFldDsc);
                newFldDsc.AddToJustAssignedToCats(this);
                return true;
            }
            return false;
        }
        public bool RemoveFieldDescription(FieldDescription fdToBeRemoved)
        {
            if (fdToBeRemoved != null)
            {
                fdToBeRemoved.RemoveFromJustAssignedToCats(this);
                return FieldDescriptions.Remove(fdToBeRemoved);
            }
            return false;
        }

        public static bool Check(Category cat)
        {
            return cat != null && cat.Id != DEFAULT_ID && cat.FieldDescriptions != null && !string.IsNullOrWhiteSpace(cat.Name);
        }

        //public class EqComparer : IEqualityComparer<Category>
        //{
        //    bool IEqualityComparer<Category>.Equals(Category x, Category y)
        //    {
        //        return x.Id == y.Id;
        //    }

        //    int IEqualityComparer<Category>.GetHashCode(Category obj)
        //    {
        //        return obj.GetHashCode();
        //    }
        //}
    }
}
