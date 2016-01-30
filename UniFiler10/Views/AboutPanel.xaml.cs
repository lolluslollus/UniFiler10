using System;
using System.Diagnostics;
using UniFiler10.Data.Constants;
using UniFiler10.Data.Runtime;
using Utilz;
using Utilz.Controlz;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
    public sealed partial class AboutPanel : ObservableControl
    {
		#region properties
		public string AppName { get { return ConstantData.AppName; } }
        public string AppVersion { get { return ConstantData.Version; } }

		private string _logText;
		public string LogText { get { return _logText; } set { _logText = value; RaisePropertyChanged_UI(); } }

		private RuntimeData _runtimeData = null;
		public RuntimeData RuntimeData { get { return _runtimeData; } private set { _runtimeData = value;  RaisePropertyChanged_UI(); } }
		#endregion properties


		public AboutPanel()
        {
            InitializeComponent();
            RuntimeData = RuntimeData.Instance;
#if NOSTORE
			LogsGrid.Visibility = Visibility.Visible;
#endif
		}
        private async void OnBuy_Click(object sender, RoutedEventArgs e)
        {
            bool isAlreadyBought = await Licenser.BuyAsync();
			if (!isAlreadyBought) await (App.Current as App).Quit().ConfigureAwait(false);
		}

        private async void OnRate_Click(object sender, RoutedEventArgs e)
        {
            await Licenser.RateAsync();
        }
        private async void OnSendMail_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string uri = "mailto:" + ConstantData.MYMAIL + "?subject=" + ConstantData.APPNAME + " feedback";
                await Launcher.LaunchUriAsync(new Uri(uri, UriKind.Absolute));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR: OnSendMail_Click caused an exception: " + ex.ToString());
            }
        }

        private async void OnSendMailWithLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Logger.SendEmailWithLogsAsync(ConstantData.MYMAIL, ConstantData.APPNAME).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR: OnSendMailWithLog_Click caused an exception: " + ex.ToString());
            }
        }

		private async void OnLogButton_Click(object sender, RoutedEventArgs e)
		{
			String cnt = (sender as Button).Content.ToString();
			if (cnt == "FileError")
			{
				LogText = await Logger.ReadAsync(Logger.FileErrorLogFilename);
			}
			else if (cnt == "MyPersistentData")
			{
				LogText = await Logger.ReadAsync(Logger.PersistentDataLogFilename);
			}
			else if (cnt == "Fgr")
			{
				LogText = await Logger.ReadAsync(Logger.ForegroundLogFilename);
			}
			else if (cnt == "Bgr")
			{
				LogText = await Logger.ReadAsync(Logger.BackgroundLogFilename);
			}
			else if (cnt == "BgrCanc")
			{
				LogText = await Logger.ReadAsync(Logger.BackgroundCancelledLogFilename);
			}
			else if (cnt == "AppExc")
			{
				LogText = await Logger.ReadAsync(Logger.AppExceptionLogFilename);
			}
			else if (cnt == "AppEvents")
			{
				LogText = await Logger.ReadAsync(Logger.AppEventsLogFilename);
			}
			else if (cnt == "Clear")
			{
				Logger.ClearAll();
			}
		}
		private void OnLogText_Unloaded(object sender, RoutedEventArgs e)
		{
			LogText = string.Empty;
		}
	}
}
