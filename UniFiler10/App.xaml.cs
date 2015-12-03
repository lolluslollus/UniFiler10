using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UniFiler10.Data.Constants;
using UniFiler10.Data.Model;
using UniFiler10.Views;
using Utilz;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace UniFiler10
{
	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
	sealed partial class App : Application
	{
		public const string LAST_NAVIGATED_PAGE_REG_KEY = "LastNavigatedPage";
		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App()
		{
			Microsoft.ApplicationInsights.WindowsAppInitializer.InitializeAsync(
				Microsoft.ApplicationInsights.WindowsCollectors.Metadata |
				Microsoft.ApplicationInsights.WindowsCollectors.Session);
			InitializeComponent();
			Suspending += OnSuspending;
			Resuming += OnResuming;
			UnhandledException += OnUnhandledException;
		}

		/// <summary>
		/// Invoked when the application is launched normally by the end user.  Other entry points
		/// will be used such as when the application is launched to open a specific file.
		/// </summary>
		/// <param name="e">Details about the launch request and process.</param>
		protected override void OnLaunched(LaunchActivatedEventArgs e)
		{

			//#if DEBUG
			//            if (System.Diagnostics.Debugger.IsAttached)
			//            {
			//                DebugSettings.EnableFrameRateCounter = true;
			//            }
			//#endif
			Logger.Add_TPL("App started", Logger.ForegroundLogFilename, Logger.Severity.Info);
			Frame rootFrame = Window.Current.Content as Frame;

			// Do not repeat app initialization when the Window already has content,
			// just ensure that the window is active
			if (rootFrame == null)
			{
				// Create a Frame to act as the navigation context and navigate to the first page
				rootFrame = new Frame();

				rootFrame.NavigationFailed += OnNavigationFailed;

				// Set the default language
				//rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];
				rootFrame.Language = Windows.Globalization.Language.CurrentInputMethodLanguageTag; //this is important and decides for the whole app

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

		private async void OnResuming(object sender, object e)
		{
			// LOLLO NOTE this is the first OnResuming to fire
			// throw new NotImplementedException();
			var briefcase = Briefcase.GetOrCreateInstance();
			if (briefcase != null)
			{
				await briefcase.OpenAsync().ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Invoked when application execution is being suspended.  Application state is saved
		/// without knowing whether the application will be terminated or resumed with the contents
		/// of memory still intact.
		/// </summary>
		/// <param name="sender">The source of the suspend request.</param>
		/// <param name="e">Details about the suspend request.</param>
		private async void OnSuspending(object sender, SuspendingEventArgs e)
		{
			// LOLLO NOTE this is the first OnSuspending to fire
			var deferral = e.SuspendingOperation.GetDeferral();
			// Save application state and stop any background activity
			var briefcase = Briefcase.GetCurrentInstance();
			if (briefcase != null)
			{
				await briefcase.CloseAsync().ConfigureAwait(false);
				briefcase.Dispose();
				briefcase = null;
			}

			deferral.Complete();
		}
		/// <summary>
		/// Invoked when Navigation to a certain page fails
		/// </summary>
		/// <param name="sender">The Frame which failed navigation</param>
		/// <param name="e">Details about the navigation failure</param>
		void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
		{
			throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
		}
		private async void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			await Logger.AddAsync(e?.Exception?.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
		}
	}
}
