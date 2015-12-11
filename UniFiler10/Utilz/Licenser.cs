using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using UniFiler10;
using UniFiler10.Data.Constants;
using UniFiler10.Data.Runtime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Store;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Popups;

namespace Utilz
{
	internal class Licenser
	{
		public const int TrialLengthDays = 7;
		// private static RuntimeData _runtimeData = RuntimeData.GetCreateInstance();
		public static async Task<bool> CheckLicensedAsync()
		{
			// do not use persistent data across this class
			// coz you cannot be sure it has been read out yet.
			// I can only use license expiration date with win10 if I want the "try" button in the store
			try
			{
#if NOSTORE && !TRIALTESTING
				return true;
#endif
				// if (Package.Current.IsDevelopmentMode) return true; // only available with win10

				// if (Package.Current.IsBundle) return true; // don't use it https://msdn.microsoft.com/library/windows/apps/hh975357(v=vs.120).aspx#Appx
				//await LoadAppListingUriProxyFileAsync();
				LicenseInformation licenseInformation = await GetLicenseInformation();
				if (licenseInformation.IsActive)
				{
					if (licenseInformation.IsTrial)
					{
						RuntimeData.Instance.IsTrial = true;

						bool isCheating = false;
						bool isExpired = false;

						var installDate = await GetInstallDateAsync();
						CheckInstallDate(ref isCheating, installDate);

						var expiryDate = GetExpiryDate(licenseInformation, installDate);
						CheckExpiryDate(ref isCheating, ref isExpired, expiryDate, installDate);

						int usageDays = (DateTimeOffset.Now - installDate).Days;
						CheckUsageDays(ref isCheating, ref isExpired, installDate, usageDays);

						if (isCheating || isExpired)
						{
							RuntimeData.Instance.TrialResidualDays = -1;
						}
						else
						{
							RuntimeData.Instance.TrialResidualDays = (expiryDate - DateTimeOffset.Now).Days;
						}

						if (isCheating || isExpired
							|| RuntimeData.Instance.TrialResidualDays > TrialLengthDays
							|| RuntimeData.Instance.TrialResidualDays < 0)
						{
							return await AskQuitOrBuyAsync(RuntimeData.GetText("LicenserTrialExpiredLong"), RuntimeData.GetText("LicenserTrialExpiredShort"));
						}
					}
					else
					{
						RuntimeData.Instance.IsTrial = false;
					}
				}
				else
				{
					RuntimeData.Instance.IsTrial = true;
					return await AskQuitOrBuyAsync(RuntimeData.GetText("LicenserNoLicensesLong"), RuntimeData.GetText("LicenserNoLicensesShort"));
				}
				return true;
			}
			catch (Exception ex)
			{
				RuntimeData.Instance.IsTrial = true;
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				return await AskQuitOrBuyAsync(RuntimeData.GetText("LicenserErrorChecking"), RuntimeData.GetText("LicenserNoLicensesShort"));
			}
		}

		private static void CheckUsageDays(ref bool isCheating, ref bool isExpired, DateTimeOffset installDate, int usageDays)
		{
			if (usageDays >= 0)
			{
				if (usageDays >= LicenserData.LastNonNegativeUsageDays)
				{
					usageDays = Math.Max(usageDays, LicenserData.LastNonNegativeUsageDays);
					LicenserData.LastNonNegativeUsageDays = usageDays;
				}
				else
				{
					isCheating = true;
					Logger.Add_TPL(
						"CheckLicensedAsync() found a cheat because usageDays = " + usageDays
						+ " and installDate = " + installDate
						+ " and _runtimeData.LastNonNegativeUsageDays = " + LicenserData.LastNonNegativeUsageDays,
						Logger.ForegroundLogFilename,
						Logger.Severity.Info);
				}
				if (usageDays > TrialLengthDays)
				{
					isExpired = true;
				}
			}
			else
			{
				isCheating = true;
			}
		}

		private static void CheckExpiryDate(ref bool isCheating, ref bool isExpired, DateTimeOffset expiryDate, DateTimeOffset installDate)
		{
			if (LicenserData.LastExpiryDate == LicenserData.Date_Default)
			{
				LicenserData.LastExpiryDate = expiryDate;
			}
			if (!LicenserData.IsDatesEqual(LicenserData.LastExpiryDate, expiryDate))
			{
				isCheating = true;
				Logger.Add_TPL(
					"CheckLicensedAsync() found a cheat because LastExpiryDate = " + LicenserData.LastExpiryDate
					+ " and expiryDate = " + expiryDate,
					Logger.ForegroundLogFilename,
					Logger.Severity.Info);
			}
			if (expiryDate.CompareTo(DateTimeOffset.Now) < 0)
			{
				isExpired = true;
			}
			if (expiryDate.CompareTo(installDate) < 0)
			{
				isCheating = true;
			}
		}

		private static void CheckInstallDate(ref bool isCheating, DateTimeOffset installDate)
		{
			if (LicenserData.LastInstallDate == LicenserData.Date_Default)
			{
				LicenserData.LastInstallDate = installDate;
			}
			if (!LicenserData.IsDatesEqual(LicenserData.LastInstallDate, installDate))
			{
				isCheating = true;
				Logger.Add_TPL(
					"CheckLicensedAsync() found a cheat because LastInstallDate = " + LicenserData.LastInstallDate
					+ " and installDate = " + installDate,
					Logger.ForegroundLogFilename,
					Logger.Severity.Info);
			}
		}

		private async static Task<LicenseInformation> GetLicenseInformation()
		{
#if DEBUG && TRIALTESTING
			StorageFolder proxyDataFolder = await Package.Current.InstalledLocation.GetFolderAsync("Assets");
			StorageFile proxyFile = await proxyDataFolder.GetFileAsync("WindowsStoreProxy.xml");
			await CurrentAppSimulator.ReloadSimulatorAsync(proxyFile).AsTask();

			LicenseInformation licenseInformation = CurrentAppSimulator.LicenseInformation;
#else
			LicenseInformation licenseInformation = CurrentApp.LicenseInformation;
#endif
			return licenseInformation;
		}
		private static async Task<DateTimeOffset> GetInstallDateAsync()
		{
			// LOLLO TODO check this method
			StorageFolder installLocationFolder = Package.Current.InstalledLocation;
			// Package.Current.InstalledLocation always has a very low install date, like 400 years ago...
			// but its descendants don't, so we take one that is bound to be there, such as Assets.
			//Debug.WriteLine(installLocationFolder.DateCreated);
			//var folders = await installLocationFolder.GetFoldersAsync().AsTask<IReadOnlyList<StorageFolder>>();
			//foreach (var item in folders)
			//{
			//    Debug.WriteLine(item.DateCreated);
			//    Debug.WriteLine(item.DisplayName);
			//    Debug.WriteLine(item.Name);
			//}
			var assetsFolder = await installLocationFolder.GetFolderAsync("Assets").AsTask().ConfigureAwait(false);
			DateTimeOffset folderCreateDate = assetsFolder.DateCreated;

			if (Package.Current.InstalledDate.CompareTo(folderCreateDate) < 0) return Package.Current.InstalledDate;
			else return folderCreateDate;
		}
		private static DateTimeOffset GetExpiryDate(LicenseInformation licenseInformation, DateTimeOffset installDate)
		{
			var expiryDate = licenseInformation.ExpirationDate;
			var expiryDateTemp = installDate.AddDays(Convert.ToDouble(TrialLengthDays));
			if (expiryDate.CompareTo(expiryDateTemp) > 0) expiryDate = expiryDateTemp;

			return expiryDate;
		}
		private static async Task<bool> AskQuitOrBuyAsync(string message1, string message2)
		{
			var dialog = new MessageDialog(message1, message2);
			UICommand quitCommand = new UICommand(RuntimeData.GetText("LicenserQuit"), (command) => { });
			dialog.Commands.Add(quitCommand);
			UICommand buyCommand = new UICommand(RuntimeData.GetText("LicenserBuy"), (command) => { });
			dialog.Commands.Add(buyCommand);
			// Set the command that will be invoked by default
			dialog.DefaultCommandIndex = 1;

			// Show the message dialog
			Task<IUICommand> taskReply = null;
			await RunInUiThreadAsync(delegate
			{
				taskReply = dialog.ShowAsync().AsTask();
			}).ConfigureAwait(false);
			IUICommand reply = await taskReply.ConfigureAwait(false);

			bool isAlreadyBought = false;
			if (reply == buyCommand)
			{
				isAlreadyBought = await BuyAsync();
			}
			if (isAlreadyBought)
			{
				//_runtimeData.IsTrial = false; // LOLLO this would be better but risky. I should never arrive here anyway. What then?
				//_runtimeData.TrialResidualDays = 0; // idem
				return true;
			}
			else
			{
				await (App.Current as App).Quit().ConfigureAwait(false);
				return false;
			}
		}
		/// <summary>
		/// Opens the store to buy the app and returns false if the app must quit.
		/// </summary>
		/// <returns></returns>
		public static async Task<bool> BuyAsync()
		{
			LicenseInformation licenseInformation = await GetLicenseInformation();

			if (licenseInformation.IsTrial)
			{
				try
				{
					// go to the store and quit instead of calling RequestAppPurchaseAsync, which is not supported 
					// LOLLO TODO MAYBE see if it works with win10
					// https://msdn.microsoft.com/en-us/library/windows/apps/mt228343.aspx
					var uri = new Uri(ConstantData.BUY_URI);

					Debug.WriteLine("Store uri = " + uri.ToString());
					await Launcher.LaunchUriAsync(uri).AsTask();
					return false; // must quit and restart to verify the purchase

					////string receipt = await CurrentAppSimulator.RequestAppPurchaseAsync(true).AsTask<String>();
					//string receipt = await CurrentApp.RequestAppPurchaseAsync(true).AsTask<String>();
					//XElement receiptXml = XElement.Parse(receipt);
					//var appReceipt = receiptXml.Element("AppReceipt");
					//if (appReceipt.Attribute("LicenseType").Value == "Full" && !licenseInformation.IsTrial && licenseInformation.IsActive)
					//{
					//    await NotifyAsync("Your purchase was successful.", "Done");
					//    return true;
					//}
					//else
					//{
					//    await NotifyAsync("You still have a trial license for this app.", "Sorry");
					//    return false;
					//}
				}
				catch (Exception)
				{
					await NotifyAsync(RuntimeData.GetText("LicenserUpgradeFailedLong"), RuntimeData.GetText("LicenserUpgradeFailedShort"));
					return false;
				}
			}
			else
			{
				Logger.Add_TPL("ERROR: Licenser.BuyAsync() was called after the product had been bought already", Logger.ForegroundLogFilename);
				await NotifyAsync(RuntimeData.GetText("LicenserAlreadyBoughtLong"), RuntimeData.GetText("LicenserAlreadyBoughtShort"));
				return true;
			}
		}

		/// <summary>
		/// Opens the store to rate the app. Returns true if the operation succeeded.
		/// </summary>
		/// <returns></returns>
		public static async Task<bool> RateAsync()
		{
			try
			{
				var uri = new Uri(ConstantData.RATE_URI);
				await Launcher.LaunchUriAsync(uri).AsTask();
				return true;
			}
			catch (Exception)
			{
				await NotifyAsync(RuntimeData.GetText("LicenserCannotOpenStoreLong"), RuntimeData.GetText("LicenserCannotOpenStoreShort"));
				return false;
			}
		}
		private static async Task NotifyAsync(string message1, string message2)
		{
			var dialog = new MessageDialog(message1, message2);
			UICommand okCommand = new UICommand(RuntimeData.GetText("Ok"), (command) => { });
			dialog.Commands.Add(okCommand);

			Task<IUICommand> taskReply = null;
			await RunInUiThreadAsync(delegate
			{
				taskReply = dialog.ShowAsync().AsTask();
			}).ConfigureAwait(false);
			await taskReply.ConfigureAwait(false);
		}
		private static async Task RunInUiThreadAsync(DispatchedHandler action)
		{
			try
			{
				if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
				{
					action();
				}
				else
				{
					await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, action).AsTask().ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
		}
		private class LicenserData
		{
			private const string LastNonNegativeUsageDaysKey = "LastNonNegativeUsageDays";
			private const string LastInstallDateKey = "LastInstallDate";
			private const string LastExpiryDateKey = "LastExpiryDate";

			public const int LastNonNegativeUsageDays_Default = 0;
			public static readonly DateTimeOffset Date_Default = default(DateTimeOffset);

			private static int _lastNonNegativeUsageDays = LastNonNegativeUsageDays_Default;
			public static int LastNonNegativeUsageDays
			{
				get { return _lastNonNegativeUsageDays; }
				set
				{
					if (_lastNonNegativeUsageDays != value)
					{
						_lastNonNegativeUsageDays = value;
						SaveLastNonNegativeUsageDays();
					}
				}
			}

			private static DateTimeOffset _lastInstallDate = default(DateTimeOffset);
			public static DateTimeOffset LastInstallDate
			{
				get { return _lastInstallDate; }
				set
				{
					if (_lastInstallDate != value)
					{
						_lastInstallDate = value;
						SaveLastInstallDate();
					}
				}
			}
			private static DateTimeOffset _lastExpiryDate = default(DateTimeOffset);
			public static DateTimeOffset LastExpiryDate
			{
				get { return _lastExpiryDate; }
				set
				{
					if (_lastExpiryDate != value)
					{
						_lastExpiryDate = value;
						SaveLastExpiryDate();
					}
				}
			}

			static LicenserData()
			{
				string lastNonNegativeUsageDaysString = RegistryAccess.GetValue(LastNonNegativeUsageDaysKey);
				try
				{
					_lastNonNegativeUsageDays = Convert.ToInt32(lastNonNegativeUsageDaysString, CultureInfo.InvariantCulture);
				}
				catch (Exception)
				{
					_lastNonNegativeUsageDays = LastNonNegativeUsageDays_Default;
				}

				string lastInstallDate = RegistryAccess.GetValue(LastInstallDateKey);
				try
				{
					_lastInstallDate = Convert.ToDateTime(lastInstallDate, CultureInfo.InvariantCulture);
				}
				catch (Exception)
				{
					_lastInstallDate = Date_Default;
				}

				string lastExpiryDate = RegistryAccess.GetValue(LastExpiryDateKey);
				try
				{
					_lastExpiryDate = Convert.ToDateTime(lastExpiryDate, CultureInfo.InvariantCulture);
				}
				catch (Exception)
				{
					_lastExpiryDate = Date_Default;
				}
			}

			private static void SaveLastNonNegativeUsageDays()
			{
				string lastNonNegativeUsageDaysString = _lastNonNegativeUsageDays.ToString(CultureInfo.InvariantCulture);
				RegistryAccess.SetValue(LastNonNegativeUsageDaysKey, lastNonNegativeUsageDaysString);
			}
			private static void SaveLastInstallDate()
			{
				string lastInstallDate = _lastInstallDate.ToString(ConstantData.DATE_TIME_FORMAT, CultureInfo.InvariantCulture);
				RegistryAccess.SetValue(LastInstallDateKey, lastInstallDate);
			}
			private static void SaveLastExpiryDate()
			{
				string lastExpiryDate = _lastExpiryDate.ToString(ConstantData.DATE_TIME_FORMAT, CultureInfo.InvariantCulture);
				RegistryAccess.SetValue(LastExpiryDateKey, lastExpiryDate);
			}
			public static bool IsDatesEqual(DateTimeOffset one, DateTimeOffset two)
			{
				return one.ToString(ConstantData.DATE_TIME_FORMAT, CultureInfo.InvariantCulture).Equals(two.ToString(ConstantData.DATE_TIME_FORMAT, CultureInfo.InvariantCulture));
			}
		}
	}
}