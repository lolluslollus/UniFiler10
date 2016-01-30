using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilz;
using Windows.Foundation.Metadata;
using Windows.Phone.UI.Input;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Utilz.Controlz
{
	// LOLLO this is all too complicated! I only keep it for reference.
	public abstract class BackableOpenableObservableControl : OpenableObservableControl
	{
		//// LOLLO NOTE test what happens when back is pressed and a backable control is hosted in another backable control: 
		//// which one responds first? The host! So a backable control is designed to ignore back pressed, if any backable children are registered. 

		//private bool _isBackHandlersRegistered = false;

		//private bool _isBackButtonAvailable = false;
		//public bool IsBackButtonAvailable { get { return _isBackButtonAvailable; } private set { _isBackButtonAvailable = value; RaisePropertyChanged_UI(); } }

		//private static SemaphoreSlimSafeRelease _backHandlerSemaphore = new SemaphoreSlimSafeRelease(1, 1);

		///// <summary>
		///// If the parent is made invisible, the child is not necessarily made invisible, so CloseAsync() may not fire.
		///// Use this property on a child to make it open and close, whenever the parent does.
		///// Leave it blank if there is no OpenableControl parent or if you have special needs.
		///// </summary>
		//public BackableOpenableObservableControl BackableParent
		//{
		//	get { return (BackableOpenableObservableControl)GetValue(BackableParentProperty); }
		//	set { SetValue(BackableParentProperty, value); }
		//}
		//public static readonly DependencyProperty BackableParentProperty =
		//	DependencyProperty.Register("BackableParent", typeof(BackableOpenableObservableControl), typeof(BackableOpenableObservableControl), new PropertyMetadata(null/*, OnParentChanged*/));
		////private static void OnParentChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		////{
		////	var instance = obj as BackableOpenableObservableControl;
		////	var parent = args?.OldValue as BackableOpenableObservableControl;
		////	if(instance!=null && parent != null)
		////	{
		////		Task remove = parent.Remo
		////		Task add = parent.AddBackableChildAsync(instance);
		////	}
		////	if (args?.OldValue is BackableOpenableObservableControl) instance?.UnregisterParentHandlers(args.OldValue as OpenableObservableControl);
		////	instance?.RegisterParentHandlers();
		////}

		////private BackableOpenableObservableControl _backableParent = null;
		//private static SemaphoreSlimSafeRelease _backableChildrenSemaphore = new SemaphoreSlimSafeRelease(1, 1);

		//// private List<BackableOpenableObservableControl> _backableChildren = new List<BackableOpenableObservableControl>();
		//private List<WeakReference<BackableOpenableObservableControl>> _backableChildren = new List<WeakReference<BackableOpenableObservableControl>>();
		//public async Task AddBackableChildAsync(BackableOpenableObservableControl child)
		//{
		//	try
		//	{
		//		await _backableChildrenSemaphore.WaitAsync();
		//		// if (!_backableChildren.Contains(child)) _backableChildren.Add(child);
		//		bool canAdd = true;
		//		foreach (var wrch in _backableChildren)
		//		{
		//			BackableOpenableObservableControl childInLoop = null;
		//			if (wrch.TryGetTarget(out childInLoop))
		//			{
		//				if (childInLoop == child)
		//				{
		//					canAdd = false;
		//					break;
		//				}
		//			}
		//		}
		//		if (canAdd) _backableChildren.Add(new WeakReference<BackableOpenableObservableControl>(child));
		//	}
		//	finally
		//	{
		//		SemaphoreSlimSafeRelease.TryRelease(_backableChildrenSemaphore);
		//	}
		//}
		//public async Task RemoveBackableChildAsync(BackableOpenableObservableControl child)
		//{
		//	try
		//	{
		//		await _backableChildrenSemaphore.WaitAsync();
		//		// _backableChildren.Remove(child);
		//		WeakReference<BackableOpenableObservableControl> wrch_found = null;
		//		foreach (var wrch in _backableChildren)
		//		{
		//			BackableOpenableObservableControl childInLoop = null;
		//			if (wrch.TryGetTarget(out childInLoop))
		//			{
		//				if (childInLoop == child)
		//				{
		//					wrch_found = wrch;
		//					break;
		//				}
		//			}
		//		}
		//		if (wrch_found != null) _backableChildren.Remove(wrch_found);
		//	}
		//	finally
		//	{
		//		SemaphoreSlimSafeRelease.TryRelease(_backableChildrenSemaphore);
		//	}
		//}
		//private async Task<bool> HasBackableChildrenAsync()
		//{
		//	try
		//	{
		//		await _backableChildrenSemaphore.WaitAsync();
		//		if (_backableChildren.Count <= 0) return false;
		//		BackableOpenableObservableControl child_found = null;
		//		foreach (var child in _backableChildren)
		//		{
		//			if (child.TryGetTarget(out child_found))
		//			{
		//				return true;
		//			}
		//		}
		//		return false;
		//	}
		//	finally
		//	{
		//		SemaphoreSlimSafeRelease.TryRelease(_backableChildrenSemaphore);
		//	}
		//}


		//#region construct open close
		//public BackableOpenableObservableControl()
		//{
		//	Loaded += OnLoaded;
		//	Unloaded += OnUnloaded;
		//}
		//protected override async Task<bool> OpenMayOverrideAsync()
		//{
		//	if (await base.OpenMayOverrideAsync().ConfigureAwait(false))
		//	{
		//		RegisterBackEventHandlers();
		//		return true;
		//	}
		//	else
		//	{
		//		return false;
		//	}
		//}
		//protected override async Task CloseMayOverrideAsync()
		//{
		//	await base.CloseMayOverrideAsync().ConfigureAwait(false);
		//	UnregisterBackEventHandlers();
		//}
		//#endregion construct open close


		//#region event helpers
		//private void RegisterBackEventHandlers()
		//{
		//	RunInUiThread(delegate
		//	{
		//		if (!_isBackHandlersRegistered)
		//		{
		//			_isBackHandlersRegistered = true;

		//			if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
		//			{
		//				HardwareButtons.BackPressed += OnHardwareButtons_BackPressed;
		//				IsBackButtonAvailable = true;
		//			}
		//			SystemNavigationManager.GetForCurrentView().BackRequested += OnTabletSoftwareButton_BackPressed;
		//			if (!_isBackButtonAvailable) IsBackButtonAvailable = SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility == AppViewBackButtonVisibility.Visible;
		//		}
		//	});
		//}
		//private void UnregisterBackEventHandlers()
		//{
		//	RunInUiThread(delegate
		//	{

		//		if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
		//		{
		//			HardwareButtons.BackPressed -= OnHardwareButtons_BackPressed;
		//		}
		//		SystemNavigationManager.GetForCurrentView().BackRequested -= OnTabletSoftwareButton_BackPressed;

		//		_isBackHandlersRegistered = false;
		//	});
		//}
		//#endregion event helpers


		//#region event handlers
		//private void OnLoaded(object sender, RoutedEventArgs e)
		//{
		//	//_backableParent = GetParent();
		//	Task add = BackableParent?.AddBackableChildAsync(this);
		//}
		////private BackableOpenableObservableControl GetParent() // this does not work within a Popup
		////{
		////	var parent0 = VisualTreeHelper.GetParent(this);
		////	while (parent0 != null && !(parent0 is BackableOpenableObservableControl))
		////	{
		////		var parent1 = VisualTreeHelper.GetParent(parent0);
		////		parent0 = parent1;
		////	}
		////	return parent0 as BackableOpenableObservableControl;
		////}
		//private void OnUnloaded(object sender, RoutedEventArgs e)
		//{
		//	Task remove = BackableParent?.RemoveBackableChildAsync(this);
		//}

		//public async void OnOwnBackButton_Tapped(object sender, TappedRoutedEventArgs e) // this method is public so XAML can see it
		//{
		//	try
		//	{
		//		await _backHandlerSemaphore.WaitAsync();
		//		if (!e.Handled) e.Handled = await GoBack();
		//	}
		//	finally
		//	{
		//		SemaphoreSlimSafeRelease.TryRelease(_backHandlerSemaphore);
		//	}
		//}
		//private async void OnHardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
		//{
		//	try
		//	{
		//		await _backHandlerSemaphore.WaitAsync();
		//		if (!e.Handled) e.Handled = await GoBack();
		//	}
		//	finally
		//	{
		//		SemaphoreSlimSafeRelease.TryRelease(_backHandlerSemaphore);
		//	}
		//}
		//private async void OnTabletSoftwareButton_BackPressed(object sender, BackRequestedEventArgs e)
		//{
		//	try
		//	{
		//		await _backHandlerSemaphore.WaitAsync();
		//		if (!e.Handled) e.Handled = await GoBack();
		//	}
		//	finally
		//	{
		//		SemaphoreSlimSafeRelease.TryRelease(_backHandlerSemaphore);
		//	}
		//}
		//#endregion event handlers


		//#region go back
		///// <summary>
		///// Returns true if it goes back or a child went back
		///// </summary>
		///// <returns></returns>
		//protected async Task<bool> GoBack()
		//{
		//	// if (_backableChildren.Count <= 0)
		//	if (!(await HasBackableChildrenAsync()))
		//	{
		//		return await RunFunctionIfOpenAsyncA(GoBackMustOverride).ConfigureAwait(false);
		//	}
		//	else
		//	{
		//		foreach (var child in _backableChildren)
		//		{
		//			// if (await child.GoBack()) return true;
		//			BackableOpenableObservableControl child_found = null;
		//			if (child.TryGetTarget(out child_found))
		//			{
		//				if (await child_found.GoBack()) return true;
		//			}
		//		}
		//		return await RunFunctionIfOpenAsyncA(GoBackMustOverride).ConfigureAwait(false);
		//	}
		//}
		//protected abstract void GoBackMustOverride();
		//#endregion go back
	}
}
