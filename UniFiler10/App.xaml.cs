using System;
using System.Threading.Tasks;
using UniFiler10.Data.Constants;
using UniFiler10.Data.Model;
using UniFiler10.Views;
using Utilz;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Phone.Devices.Notification;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace UniFiler10
{
	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
	sealed partial class App : Application
	{
		public const string LAST_NAVIGATED_PAGE_REG_KEY = "LastNavigatedPage";
		private static readonly bool _isVibrationDevicePresent = Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.Devices.Notification.VibrationDevice");
		//private static SemaphoreSlimSafeRelease _resumingActivatingSemaphore = new SemaphoreSlimSafeRelease(1, 1);

		#region lifecycle
		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App()
		{
			Microsoft.ApplicationInsights.WindowsAppInitializer.InitializeAsync(
				Microsoft.ApplicationInsights.WindowsCollectors.Metadata |
				Microsoft.ApplicationInsights.WindowsCollectors.Session);

			UnhandledException += OnUnhandledException;
			Suspending += OnSuspending;
			Resuming += OnResuming;

			InitializeComponent();

			Logger.Add_TPL("App ctor ended OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		}
		private static async Task OpenAsync()
		{
			var briefcase = Briefcase.GetCreateInstance();
			if (briefcase != null)
			{
				await briefcase.OpenAsync().ConfigureAwait(false);
			}
		}

		private static async Task CloseAsync()
		{
			var briefcase = Briefcase.GetCurrentInstance();
			if (briefcase != null)
			{
				await briefcase.CloseAsync().ConfigureAwait(false);
				briefcase.Dispose();
				briefcase = null;
			}
		}
		#endregion lifecycle


		#region event handlers
		/// <summary>
		/// Invoked when the application is launched normally by the end user.  Other entry points
		/// will be used such as when the application is launched to open a specific file, to display
		/// search results, and so forth.
		/// This is also invoked when the app is resumed after being terminated.
		/// </summary>
		protected override void OnLaunched(LaunchActivatedEventArgs e)
		{
			Logger.Add_TPL("OnLaunched started with " + " arguments = " + e.Arguments + " and kind = " + e.Kind.ToString() + " and prelaunch activated = " + e.PrelaunchActivated + " and prev exec state = " + e.PreviousExecutionState.ToString(),
				Logger.AppEventsLogFilename,
				Logger.Severity.Info,
				false);

			var rootFrame = Window.Current.Content as Frame;

			// Do not repeat app initialization when the Window already has content,
			// just ensure that the window is active
			if (rootFrame == null)
			{
				// Create a Frame to act as the navigation context and navigate to the first page
				rootFrame = new Frame() { UseLayoutRounding = true };

				rootFrame.NavigationFailed += OnNavigationFailed;

				// Set the default language
				//rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];
				rootFrame.Language = Windows.Globalization.Language.CurrentInputMethodLanguageTag; //LOLLO NOTE this is important and decides for the whole app

				if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
				{
					// MS: Load state from previously suspended application
				}

				// Place the frame in the current Window
				Window.Current.Content = rootFrame;
			}

			if (rootFrame.Content == null)
			{
				// When the navigation stack isn't restored navigate to the first page,
				// configuring the new page by passing required information as a navigation
				// parameter
				// rootFrame.Navigate(typeof(BriefcasePage), e.Arguments); // was
				string lastNavigatedType = RegistryAccess.GetValue(LAST_NAVIGATED_PAGE_REG_KEY);
				if (string.IsNullOrWhiteSpace(lastNavigatedType))
				{
					lastNavigatedType = nameof(BriefcasePage);
				}
				rootFrame.Navigate(Type.GetType(ConstantData.VIEWS_NAMESPACE + lastNavigatedType + "," + ConstantData.ASSEMBLY_NAME), e.Arguments);
			}
			// Ensure the current window is active
			Window.Current.Activate();
		}

		/// Invoked when the app is resumed without being terminated.
		/// You should handle the Resuming event only if you need to refresh any displayed content that might have changed while the app is suspended. 
		/// You do not need to restore other app state when the app resumes.
		/// LOLLO NOTE this is the first OnResuming to fire. The other OnResuming() fire later.
		private static async void OnResuming(object sender, object e)
		{
			Logger.Add_TPL("OnResuming started", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
			try
			{
				//await _resumingActivatingSemaphore.WaitAsync();
				Logger.Add_TPL("OnResuming started is in the semaphore", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

				await OpenAsync().ConfigureAwait(false);
				Logger.Add_TPL("OnResuming ended proc OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.AppEventsLogFilename);
			}
			//finally
			//{
			//	SemaphoreSlimSafeRelease.TryRelease(_resumingActivatingSemaphore);
			//	Logger.Add_TPL("OnResuming ended", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
			//}
		}

		/// <summary>
		/// Invoked when application execution is being suspended.  Application state is saved
		/// without knowing whether the application will be terminated or resumed with the contents
		/// of memory still intact.
		/// LOLLO NOTE this is the first OnSuspending to fire
		/// </summary>
		private static async void OnSuspending(object sender, SuspendingEventArgs e)
		{
			var deferral = e.SuspendingOperation.GetDeferral();
			Logger.Add_TPL("OnSuspending started with suspending operation deadline = " + e.SuspendingOperation.Deadline.ToString(),
				Logger.AppEventsLogFilename,
				Logger.Severity.Info,
				false);

			// Save application state and stop any background activity
			await CloseAsync().ConfigureAwait(false);
			Logger.Add_TPL("OnSuspending ended OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

			deferral.Complete();
		}

		/// <summary>
		/// Invoked when Navigation to a certain page fails
		/// </summary>
		private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
		{
			throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
		}

		private static async void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			await Logger.AddAsync("UnhandledException: " + e.Exception.ToString(), Logger.AppExceptionLogFilename);
		}

		//public async Task RunAfterResumingAsync(Func<Task> funcAsync)
		//{
		//	try
		//	{
		//		await _resumingActivatingSemaphore.WaitAsync();
		//		Logger.Add_TPL("RunUnderSemaphoreAsync() is in the semaphore", Logger.AppEventsLogFilename, Logger.Severity.Info, false);

		//		await funcAsync().ConfigureAwait(false);
		//		Logger.Add_TPL("RunUnderSemaphoreAsync() ended its task OK", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		//	}
		//	finally
		//	{
		//		SemaphoreSlimSafeRelease.TryRelease(_resumingActivatingSemaphore);
		//	}
		//	Logger.Add_TPL("RunUnderSemaphoreAsync() ended", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
		//}
		#endregion event handlers


		#region services
		public async Task Quit()
		{
			await CloseAsync().ConfigureAwait(false);
			Exit();
		}

		public static void ShortVibration()
		{
			if (!_isVibrationDevicePresent) return;
			var myDevice = VibrationDevice.GetDefault();
			myDevice.Vibrate(TimeSpan.FromSeconds(.12));
		}
		#endregion services
	}
}