using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace UniFiler10.Controlz
{
	[TemplatePart(Name = "DropDownBorder", Type = typeof(Border))]
	[TemplatePart(Name = "DeleteBorder", Type = typeof(Border))]
	[TemplatePart(Name = "PopupBorder", Type = typeof(Border))]
	//[TemplatePart(Name = "Popup", Type = typeof(Popup))]
	[TemplatePart(Name = "Flyout", Type = typeof(Flyout))]
	[TemplatePart(Name = "PopupListView", Type = typeof(ListView))]

	public class LolloTextBox : TextBox
	{
		#region properties
		private ApplicationView _appView = null;
		Border _dropDownBorder = null;
		Border _deleteBorder = null;
		Border _popupBorder = null;
		//Popup _popup = null;
		Flyout _flyout = null;
		ListView _listView = null;

		public DataTemplate ListItemTemplate
		{
			get { return (DataTemplate)GetValue(ListItemTemplateProperty); }
			set { SetValue(ListItemTemplateProperty, value); }
		}
		public static readonly DependencyProperty ListItemTemplateProperty =
			DependencyProperty.Register("ListItemTemplate", typeof(DataTemplate), typeof(LolloTextBox), new PropertyMetadata(null));

		public bool IsEditableDecoratorVisible
		{
			get { return (bool)GetValue(IsEditableDecoratorVisibleProperty); }
			set { SetValue(IsEditableDecoratorVisibleProperty, value); }
		}
		public static readonly DependencyProperty IsEditableDecoratorVisibleProperty =
			DependencyProperty.Register("IsEditableDecoratorVisible", typeof(bool), typeof(LolloTextBox), new PropertyMetadata(true, OnIsEditableDecoratorVisibleChanged));

		public bool IsDropDownButtonVisible
		{
			get { return (bool)GetValue(IsDropDownButtonVisibleProperty); }
			set { SetValue(IsDropDownButtonVisibleProperty, value); }
		}
		public static readonly DependencyProperty IsDropDownButtonVisibleProperty =
			DependencyProperty.Register("IsDropDownButtonVisible", typeof(bool), typeof(LolloTextBox), new PropertyMetadata(true, OnIsDropDownButtonVisibleChanged));

		public Visibility EditableDecoratorVisibility
		{
			get { return (Visibility)GetValue(EditableDecoratorVisibilityProperty); }
			protected set { SetValue(EditableDecoratorVisibilityProperty, value); }
		}
		public static readonly DependencyProperty EditableDecoratorVisibilityProperty =
			DependencyProperty.Register("EditableDecoratorVisibility", typeof(Visibility), typeof(LolloTextBox), new PropertyMetadata(Visibility.Collapsed));
		public Visibility DropDownVisibility
		{
			get { return (Visibility)GetValue(DropDownVisibilityProperty); }
			protected set { SetValue(DropDownVisibilityProperty, value); }
		}
		public static readonly DependencyProperty DropDownVisibilityProperty =
			DependencyProperty.Register("DropDownVisibility", typeof(Visibility), typeof(LolloTextBox), new PropertyMetadata(Visibility.Collapsed));
		public Visibility DeleteVisibility
		{
			get { return (Visibility)GetValue(DeleteVisibilityProperty); }
			protected set { SetValue(DeleteVisibilityProperty, value); }
		}
		public static readonly DependencyProperty DeleteVisibilityProperty =
			DependencyProperty.Register("DeleteVisibility", typeof(Visibility), typeof(LolloTextBox), new PropertyMetadata(Visibility.Collapsed));

		public object ItemsSource
		{
			get { return GetValue(ItemsSourceProperty); }
			set { SetValue(ItemsSourceProperty, value); }
		}
		public static readonly DependencyProperty ItemsSourceProperty =
			DependencyProperty.Register("ItemsSource", typeof(object), typeof(LolloTextBox), new PropertyMetadata(null, OnItemsSourceChanged));

		public string DisplayMemberPath
		{
			get { return (string)GetValue(DisplayMemberPathProperty); }
			set { SetValue(DisplayMemberPathProperty, value); }
		}
		public static readonly DependencyProperty DisplayMemberPathProperty =
			DependencyProperty.Register("DisplayMemberPath", typeof(string), typeof(LolloTextBox), new PropertyMetadata(null));

		public bool IsEmptyValueAllowedEvenIfNotInList
		{
			get { return (bool)GetValue(IsEmptyValueAllowedEvenIfNotInListProperty); }
			set { SetValue(IsEmptyValueAllowedEvenIfNotInListProperty, value); }
		}
		public static readonly DependencyProperty IsEmptyValueAllowedEvenIfNotInListProperty =
			DependencyProperty.Register("IsEmptyValueAllowedEvenIfNotInList", typeof(bool), typeof(LolloTextBox), new PropertyMetadata(true, OnIsEmptyValueAllowedEvenIfNotInListChanged));

		private static void OnIsEmptyValueAllowedEvenIfNotInListChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			if (args == null || args.NewValue == args.OldValue) return;
			(obj as LolloTextBox)?.UpdateDeleteButton();
		}

		private static void OnIsEditableDecoratorVisibleChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			if (args == null || args.NewValue == args.OldValue) return;
			(obj as LolloTextBox)?.UpdateEDV();
		}

		private static void OnIsDropDownButtonVisibleChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			if (args == null || args.NewValue == args.OldValue) return;
			(obj as LolloTextBox)?.UpdateDropDownButton();
			(obj as LolloTextBox)?.UpdateDeleteButton();
		}

		private void OnItemsSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			//UpdateItemsSource();
			UpdateDropDownButton();
			UpdateDeleteButton();
		}

		private static void OnItemsSourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			if (args == null || args.NewValue == args.OldValue) return;

			var ltb = (obj as LolloTextBox);
			if (ltb != null)
			{
				if (args.OldValue is INotifyCollectionChanged) (args.OldValue as INotifyCollectionChanged).CollectionChanged -= ltb.OnItemsSource_CollectionChanged;
				if (args.NewValue is INotifyCollectionChanged) (args.NewValue as INotifyCollectionChanged).CollectionChanged += ltb.OnItemsSource_CollectionChanged;

				//ltb.UpdateItemsSource();
				ltb.UpdateDropDownButton();
				ltb.UpdateDeleteButton();
			}
		}

		private static void OnIsReadOnlyChanged(DependencyObject obj, DependencyProperty prop)
		{
			(obj as LolloTextBox)?.UpdateEDV();
			(obj as LolloTextBox)?.UpdateDeleteButton();
		}

		private static void OnIsEnabledChanged(DependencyObject obj, DependencyProperty prop)
		{
			(obj as LolloTextBox)?.UpdateEDV();
			(obj as LolloTextBox)?.UpdateDropDownButton();
			(obj as LolloTextBox)?.UpdateDeleteButton();
		}
		#endregion properties

		#region construct and init
		public LolloTextBox() : base()
		{
			DefaultStyleKey = "LolloTextBoxFieldValueStyle";

			RegisterPropertyChangedCallback(TextBox.IsReadOnlyProperty, OnIsReadOnlyChanged);
			RegisterPropertyChangedCallback(TextBox.IsEnabledProperty, OnIsEnabledChanged);
		}

		protected override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			_dropDownBorder = GetTemplateChild("DropDownBorder") as Border;
			if (_dropDownBorder != null)
			{
				_dropDownBorder.Tapped += OnDropDownBorder_Tapped;
			}

			_deleteBorder = GetTemplateChild("DeleteBorder") as Border;
			if (_deleteBorder != null)
			{
				_deleteBorder.Tapped += OnDeleteBorder_Tapped;
			}

			_popupBorder = GetTemplateChild("PopupBorder") as Border;
			//_popup = GetTemplateChild("Popup") as Popup;
			//if (_popup != null)
			//{
			//	_popup.Closed += OnPopup_Closed;
			//	_popup.Opened += OnPopup_Opened;
			//}

			_flyout = GetTemplateChild("Flyout") as Flyout;

			_listView = GetTemplateChild("PopupListView") as ListView;
			if (_listView != null)
			{
				if (ListItemTemplate != null) _listView.ItemTemplate = ListItemTemplate;
				_listView.SelectionChanged += OnListView_SelectionChanged;
			}


			UpdateEDV();
			//UpdateItemsSource();
			UpdateDropDownButton();
			UpdateDeleteButton();

			//UpdateStates(false);
		}
		#endregion construct and init

		#region user actions
		private void OnDropDownBorder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Debug.WriteLine("DropDownBorder Tapped");

			if (_listView == null) return;

			_listView.ItemsSource = ItemsSource;

			//if (_popup != null)
			//{
			//	if (_popup.IsOpen == false)
			//	{
			//		//_popup.HorizontalOffset = ActualWidth; // LOLLO this may get out of the window. The flyout is smarter.
			//		//_popup.MinWidth = ActualWidth;
			//		_popup.IsOpen = true;
			//	}
			//	else
			//	{
			//		_popup.IsOpen = false;
			//	}
			//}

			if (_flyout != null)
			{
				if (_popupBorder != null)
				{
					if (_appView == null) _appView = ApplicationView.GetForCurrentView();
					_popupBorder.MaxHeight = _appView.VisibleBounds.Height;
					_popupBorder.MaxWidth = _appView.VisibleBounds.Width;
				}
				try
				{
					_flyout.ShowAt(this);
				}
				catch { }
				Debug.WriteLine("Flyout open");
			}
		}

		private void OnListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			//if (_popup?.IsOpen == true) _popup.IsOpen = false;
			// LOLLO this is harmless coz it changes DynamicField.FieldValue, which is not reflected in the DB. The DB change comes outside this class.
			// when this changes a value, which goes straight into the DB, we must check it.
			if (_listView == null || _listView.SelectedItem == null) return;

			string selItem = _listView?.SelectedItem?.GetType()?.GetProperties()?.FirstOrDefault(pro => pro.Name == DisplayMemberPath)?.GetValue(_listView?.SelectedItem)?.ToString();

			_listView.ItemsSource = null;

			try
			{
				_flyout?.Hide();
			}
			catch { }
			Debug.WriteLine("Flyout closed");

			//var textBinding = GetBindingExpression(TextBox.TextProperty)?.ParentBinding;
			//var tb = GetBindingExpression(TextBox.TextProperty);
			//tb.ParentBinding.SetValue(TextProperty, selItem); // no

			SetValue(TextProperty, selItem);
			Debug.WriteLine("new value set = " + selItem);

			GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
			Debug.WriteLine("binding source updated");
		}
		private void OnDeleteBorder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			// LOLLO this is harmless coz it changes DynamicField.FieldValue, which is not reflected in the DB. The DB change comes outside this class.
			// when this changes a value, which goes straight into the DB, and it cannot be empty or null, we must check it.
			ClearValue(TextProperty);
		}

		//private void OnPopup_Closed(object sender, object e)
		//{
		//	// throw new NotImplementedException();
		//}

		//private void OnPopup_Opened(object sender, object e)
		//{
		//	//((sender as Popup).Child as FrameworkElement); LOLLO if the popup is full of items, they spill out of the window border and become invisible, instead of scrolling
		//	// throw new NotImplementedException();
		//	//if (_appView == null) _appView = ApplicationView.GetForCurrentView();
		//	//var transform = TransformToVisual(_listView);
		//	//var relativePoint = transform.TransformPoint(new Point(0.0, 0.0));
		//	//Debug.WriteLine("X= " + relativePoint.X + ", Y= " + relativePoint.Y);
		//	//Debug.WriteLine("The list is " + _listView.ActualHeight + " px high");


		//	//var transform0 = TransformToVisual(Application.Current.);
		//}
		#endregion user actions

		#region update methods
		private void UpdateEDV()
		{
			if (!IsReadOnly && IsEnabled && IsEditableDecoratorVisible) EditableDecoratorVisibility = Visibility.Visible;
			else EditableDecoratorVisibility = Visibility.Collapsed;
		}
		private void UpdateDropDownButton()
		{
			if (IsEnabled && IsDropDownButtonVisible && (ItemsSource as IList)?.Count > 0) DropDownVisibility = Visibility.Visible;
			else DropDownVisibility = Visibility.Collapsed;
		}
		private void UpdateDeleteButton()
		{
			if (IsEnabled && (!IsReadOnly || (IsDropDownButtonVisible && (ItemsSource as IList)?.Count > 0 && IsEmptyValueAllowedEvenIfNotInList))) DeleteVisibility = Visibility.Visible;
			else DeleteVisibility = Visibility.Collapsed;
		}
		//private void UpdateItemsSource()
		//{
		//	if (_listView != null)
		//	{
		//		_listView.ItemsSource = ItemsSource;
		//	}
		//}
		#endregion update methods
	}
}