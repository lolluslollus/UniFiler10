using System;
using System.Threading;
using System.Threading.Tasks;
using UniFiler10.ViewModels;
using UniFiler10.Views;
using Utilz.Data;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace UniFiler10.Controlz
{
	public class UserConfirmationPopup : ObservableData
	{
		private const int DELAY = 50;
		private static UserConfirmationPopup _instance = null;
		private static readonly object _instanceLocker = new object();
		public static UserConfirmationPopup GetInstance()
		{
			lock (_instanceLocker)
			{
				return _instance ?? (_instance = new UserConfirmationPopup());
			}
		}
		private UserConfirmationPopup() { }

		private bool _isHasUserAnswered = false;

		public async Task<Tuple<bool, bool>> GetUserConfirmationBeforeDeletingBinderAsync(CancellationToken cancToken)
		{
			var result = new Tuple<bool, bool>(false, false);
			Flyout dialog = null;
			ConfirmationBeforeDeletingBinder dialogContent = null;

			await RunInUiThreadAsync(delegate
			{
				dialog = new Flyout();
				dialogContent = new ConfirmationBeforeDeletingBinder();

				dialog.Closed += OnDialog_Closed;

				dialog.Content = dialogContent;
				dialogContent.UserAnswered += OnYesNoDialogContent_UserAnswered;

				_isHasUserAnswered = false;
				dialog.ShowAt(Window.Current.Content as FrameworkElement);
			}).ConfigureAwait(false);

			while (!_isHasUserAnswered && !cancToken.IsCancellationRequested)
			{
				await Task.Delay(DELAY, cancToken).ConfigureAwait(false);
			}

			await RunInUiThreadAsync(delegate
			{
				dialog.Closed -= OnDialog_Closed;
				dialogContent.UserAnswered -= OnYesNoDialogContent_UserAnswered;
				dialog.Hide();
				result = new Tuple<bool, bool>(dialogContent.YesNo, dialogContent.IsHasUserInteracted);
			}).ConfigureAwait(false);

			return result;
		}

		public async Task<Tuple<bool, bool>> GetUserConfirmationBeforeExportingBinderAsync(CancellationToken cancToken)
		{
			var result = new Tuple<bool, bool>(false, false);
			Flyout dialog = null;
			ConfirmationBeforeExportingBinder dialogContent = null;

			await RunInUiThreadAsync(delegate
			{
				dialog = new Flyout();
				dialogContent = new ConfirmationBeforeExportingBinder();

				dialog.Closed += OnDialog_Closed;

				dialog.Content = dialogContent;
				dialogContent.UserAnswered += OnYesNoDialogContent_UserAnswered;

				_isHasUserAnswered = false;
				dialog.ShowAt(Window.Current.Content as FrameworkElement);
			}).ConfigureAwait(false);

			while (!_isHasUserAnswered && !cancToken.IsCancellationRequested)
			{
				await Task.Delay(DELAY, cancToken).ConfigureAwait(false);
			}

			await RunInUiThreadAsync(delegate
			{
				dialog.Closed -= OnDialog_Closed;
				dialogContent.UserAnswered -= OnYesNoDialogContent_UserAnswered;
				dialog.Hide();
				result = new Tuple<bool, bool>(dialogContent.YesNo, dialogContent.IsHasUserInteracted);
			}).ConfigureAwait(false);

			return result;
		}

		private void OnDialog_Closed(object sender, object e)
		{
			_isHasUserAnswered = true;
		}

		private void OnYesNoDialogContent_UserAnswered(object sender, bool e)
		{
			_isHasUserAnswered = true;
		}


		public async Task<Tuple<BriefcaseVM.ImportBinderOperations, bool>> GetUserConfirmationBeforeImportingBinderAsync(CancellationToken cancToken)
		{
			var result = new Tuple<BriefcaseVM.ImportBinderOperations, bool>(BriefcaseVM.ImportBinderOperations.Cancel, false);
			Flyout dialog = null;
			ConfirmationBeforeImportingBinder dialogContent = null;

			await RunInUiThreadAsync(delegate
			{
				dialog = new Flyout();
				dialogContent = new ConfirmationBeforeImportingBinder();

				dialog.Closed += OnDialog_Closed;

				dialog.Content = dialogContent;
				dialogContent.UserAnswered += OnThreeBtnDialogContent_UserAnswered;

				_isHasUserAnswered = false;
				dialog.ShowAt(Window.Current.Content as FrameworkElement);
			}).ConfigureAwait(false);

			while (!_isHasUserAnswered && !cancToken.IsCancellationRequested)
			{
				await Task.Delay(DELAY, cancToken).ConfigureAwait(false);
			}

			await RunInUiThreadAsync(delegate
			{
				dialog.Closed -= OnDialog_Closed;
				dialogContent.UserAnswered -= OnThreeBtnDialogContent_UserAnswered;
				dialog.Hide();
				result = new Tuple<BriefcaseVM.ImportBinderOperations, bool>(dialogContent.Operation, dialogContent.IsHasUserInteracted);
			}).ConfigureAwait(false);

			return result;
		}

		//private void OnThreeBtnDialog_Closed(object sender, object e)
		//{
		//	_isHasUserAnswered = true;
		//}

		private void OnThreeBtnDialogContent_UserAnswered(object sender, BriefcaseVM.ImportBinderOperations e)
		{
			_isHasUserAnswered = true;
		}

		public async Task<Tuple<bool, bool>> GetUserChoiceBeforeChangingMetadataSourceAsync(CancellationToken cancToken)
		{
			var result = new Tuple<bool, bool>(false, false);
			Flyout dialog = null;
			ChoiceBeforeChangingMetadataSource dialogContent = null;

			await RunInUiThreadAsync(delegate
			{
				dialog = new Flyout();
				dialogContent = new ChoiceBeforeChangingMetadataSource();

				dialog.Closed += OnDialog_Closed;

				dialog.Content = dialogContent;
				dialogContent.UserAnswered += OnYesNoDialogContent_UserAnswered;

				_isHasUserAnswered = false;
				dialog.ShowAt(Window.Current.Content as FrameworkElement);
			}).ConfigureAwait(false);

			while (!_isHasUserAnswered && !cancToken.IsCancellationRequested)
			{
				await Task.Delay(DELAY, cancToken).ConfigureAwait(false);
			}

			await RunInUiThreadAsync(delegate
			{
				dialog.Closed -= OnDialog_Closed;
				dialogContent.UserAnswered -= OnYesNoDialogContent_UserAnswered;
				dialog.Hide();
				result = new Tuple<bool, bool>(dialogContent.YesNo, dialogContent.IsHasUserInteracted);
			}).ConfigureAwait(false);

			return result;
		}

		public async Task<Tuple<BriefcaseVM.ImportBinderOperations, string>> GetUserChoiceBeforeImportingBinderAsync(string targetBinderName, CancellationToken cancToken)
		{
			var result = new Tuple<BriefcaseVM.ImportBinderOperations, string>(BriefcaseVM.ImportBinderOperations.Cancel, string.Empty);
			Flyout dialog = null;
			ChoiceBeforeImportingBinder dialogContent = null;

			await RunInUiThreadAsync(delegate
			{
				dialog = new Flyout();
				dialogContent = new ChoiceBeforeImportingBinder(targetBinderName);

				dialog.Closed += OnDialog_Closed;

				dialog.Content = dialogContent;
				dialogContent.UserAnswered += OnThreeBtnDialogContent_UserAnswered;

				_isHasUserAnswered = false;
				dialog.ShowAt(Window.Current.Content as FrameworkElement);
			}).ConfigureAwait(false);

			while (!_isHasUserAnswered && !cancToken.IsCancellationRequested)
			{
				await Task.Delay(DELAY, cancToken).ConfigureAwait(false);
			}

			await RunInUiThreadAsync(delegate
			{
				dialog.Closed -= OnDialog_Closed;
				dialogContent.UserAnswered -= OnThreeBtnDialogContent_UserAnswered;
				dialog.Hide();
				result = new Tuple<BriefcaseVM.ImportBinderOperations, string>(dialogContent.Operation, dialogContent.DBName);
			}).ConfigureAwait(false);

			return result;
		}

		public async Task ShowTextAsync(string text, CancellationToken cancToken)
		{
			Flyout dialog = null;
			TextViewer tv = null;

			await RunInUiThreadAsync(delegate
			{
				dialog = new Flyout();
				tv = new TextViewer(text);

				dialog.Closed += OnDialog_Closed;
				dialog.Content = tv;
				tv.UserAnswered += OnTextViewer_UserAnswered; ;

				_isHasUserAnswered = false;
				dialog.ShowAt(Window.Current.Content as FrameworkElement);
			}).ConfigureAwait(false);

			while (!_isHasUserAnswered && !cancToken.IsCancellationRequested)
			{
				await Task.Delay(DELAY, cancToken).ConfigureAwait(false);
			}

			await RunInUiThreadAsync(delegate
			{
				dialog.Closed -= OnDialog_Closed;
				tv.UserAnswered -= OnTextViewer_UserAnswered;
				dialog.Hide();
			}).ConfigureAwait(false);
		}

		private void OnTextViewer_UserAnswered(object sender, bool e)
		{
			_isHasUserAnswered = true;
		}
	}
}