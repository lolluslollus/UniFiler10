using System;
using System.Diagnostics;
using UniFiler10.Data.Constants;
using UniFiler10.Data.Runtime;
using Utilz;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
    public sealed partial class AboutPanel : UserControl
    {
        public string AppName { get { return ConstantData.AppName; } }
        public string AppVersion { get { return ConstantData.Version; } }

        public AboutPanel()
        {
            InitializeComponent();
            DataContext = RuntimeData.Instance;
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
                await Logger.SendEmailWithLogsAsync(ConstantData.MYMAIL).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR: OnSendMailWithLog_Click caused an exception: " + ex.ToString());
            }
        }
    }
}
