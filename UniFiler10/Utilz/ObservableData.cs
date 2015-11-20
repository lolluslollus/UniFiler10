using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.Core;

namespace Utilz
{
    [DataContract]
    public abstract class ObservableData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void ClearListeners()
        {
            PropertyChanged = null;
        }
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
		/// <summary>
		/// Runs in the UI thread if available, otherwise queues the operation in it.
		/// </summary>
		/// <param name="propertyName"></param>
		protected void RaisePropertyChanged_UI([CallerMemberName] string propertyName = "")
		{
			RunInUiThread(delegate { RaisePropertyChanged(propertyName); });
		}
		//protected void RaisePropertyChanged_QUI([CallerMemberName] string propertyName = "")
		//{
		//	IAsyncAction ui = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, delegate { RaisePropertyChanged(propertyName); });
		//}
		#region UIThread
		public void RunInUiThread(DispatchedHandler action)
        {
            try
            {
                if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
                {
                    action();
                }
                else
                {
                    IAsyncAction ui = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, action);
                }
            }
            catch (Exception ex)
            {
                Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
            }
        }
        #endregion UIThread
    }
}
