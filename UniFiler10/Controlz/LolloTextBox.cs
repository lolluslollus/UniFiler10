using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace UniFiler10.Controlz
{
	[TemplatePart(Name = "DropDownBorder", Type = typeof(Border))]
	[TemplatePart(Name = "DeleteBorder", Type = typeof(Border))]
	[TemplatePart(Name = "Popup", Type = typeof(Popup))]
	[TemplatePart(Name = "PopupListView", Type = typeof(ListView))]

	public class LolloTextBox : TextBox
	{
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

		public bool IsDropDownVisible
		{
			get { return (bool)GetValue(IsDropDownVisibleProperty); }
			set { SetValue(IsDropDownVisibleProperty, value); }
		}
		public static readonly DependencyProperty IsDropDownVisibleProperty =
			DependencyProperty.Register("IsDropDownVisible", typeof(bool), typeof(LolloTextBox), new PropertyMetadata(true, OnIsDropDownVisibleChanged));

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

		private static void OnIsEditableDecoratorVisibleChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			(obj as LolloTextBox)?.UpdateEDV();
		}

		private static void OnIsDropDownVisibleChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			(obj as LolloTextBox)?.UpdateDropDown();
			(obj as LolloTextBox)?.UpdateDeleteButton();
		}

		private void OnItemsSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			UpdateItemsSource();
			UpdateDropDown();
			UpdateDeleteButton();
		}

		private static void OnItemsSourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			var ltb = (obj as LolloTextBox);
			if (ltb != null)
			{
				if (args.OldValue is INotifyCollectionChanged) (args.OldValue as INotifyCollectionChanged).CollectionChanged -= ltb.OnItemsSource_CollectionChanged;
				if (args.NewValue is INotifyCollectionChanged) (args.NewValue as INotifyCollectionChanged).CollectionChanged += ltb.OnItemsSource_CollectionChanged;

				ltb.UpdateItemsSource();
				ltb.UpdateDropDown();
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
			(obj as LolloTextBox)?.UpdateDropDown();
			(obj as LolloTextBox)?.UpdateDeleteButton();
		}

		Border _dropDownBorder = null;
		Border _deleteBorder = null;
		Popup _popup = null;
		ListView _listView = null;

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

			_popup = GetTemplateChild("Popup") as Popup;
			if (_popup != null)
			{
				_popup.Closed += OnPopup_Closed;
				_popup.Opened += OnPopup_Opened;
			}

			_listView = GetTemplateChild("PopupListView") as ListView;
			if (_listView != null)
			{
				if (ListItemTemplate != null) _listView.ItemTemplate = ListItemTemplate;
				_listView.SelectionChanged += OnListView_SelectionChanged;
			}


			UpdateEDV();
			UpdateItemsSource();
			UpdateDropDown();
			UpdateDeleteButton();

			//UpdateStates(false);
		}


		private void OnListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// LOLLO this is harmless coz it changes DynamicField.FieldValue, which is not reflected in the DB. The DB change comes outside this class.
			// when this changes a value, which goes straight into the DB, we must check it.
			string selItem = _listView?.SelectedItem?.GetType()?.GetProperties()?.FirstOrDefault(pro => pro.Name == DisplayMemberPath)?.GetValue(_listView?.SelectedItem)?.ToString();

			//var textBinding = GetBindingExpression(TextBox.TextProperty)?.ParentBinding;
			//var tb = GetBindingExpression(TextBox.TextProperty);

			SetValue(TextProperty, selItem);

			if (_popup?.IsOpen == true) _popup.IsOpen = false;
		}

		private void OnDropDownBorder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			if (_popup == null || _listView == null) return;

			if (_popup.IsOpen == false)
			{
				//_popup.HorizontalOffset = ActualWidth; // LOLLO this may get out of the window: fix it
				//_popup.MinWidth = ActualWidth;
				_popup.IsOpen = true;
			}
			else
			{
				_popup.IsOpen = false;
			}
		}

		private void OnDeleteBorder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			// LOLLO this is harmless coz it changes DynamicField.FieldValue, which is not reflected in the DB. The DB change comes outside this class.
			// when this changes a value, which goes straight into the DB, and it cannot be empty or null, we must check it.
			ClearValue(TextProperty);
		}

		private void OnPopup_Closed(object sender, object e)
		{
			// throw new NotImplementedException();
		}

		private void OnPopup_Opened(object sender, object e)
		{
			// throw new NotImplementedException();
		}

		private void UpdateEDV()
		{
			if (!IsReadOnly && IsEnabled && IsEditableDecoratorVisible) EditableDecoratorVisibility = Visibility.Visible;
			else EditableDecoratorVisibility = Visibility.Collapsed;
		}
		private void UpdateDropDown()
		{
			if (IsEnabled && IsDropDownVisible && (_listView?.ItemsSource as IList)?.Count > 0) DropDownVisibility = Visibility.Visible;
			else DropDownVisibility = Visibility.Collapsed;
		}
		private void UpdateDeleteButton()
		{
			if (IsEnabled && (!IsReadOnly || (IsDropDownVisible && (_listView?.ItemsSource as IList)?.Count > 0))) DeleteVisibility = Visibility.Visible;
			else DeleteVisibility = Visibility.Collapsed;
		}
		private void UpdateItemsSource()
		{
			if (_listView != null)
			{
				_listView.ItemsSource = ItemsSource;
				//if (ItemsSource is IList)
				//{
				//	var newItemsSource = new List<string>();
				//	foreach (var item in (ItemsSource as IList))
				//	{
				//		var pi1 = item?.GetType()?.GetProperties()?.FirstOrDefault(pro => pro.Name == DisplayMemberPath)?.GetValue(item)?.ToString();
				//		if (pi1 != null) newItemsSource.Add(pi1);
				//	}
				//	_listView.ItemsSource = newItemsSource;
				//}
				//else
				//{
				//	_listView.ItemsSource = null;
				//}
			}
		}

		protected override void OnLostFocus(RoutedEventArgs e)
		{
			base.OnLostFocus(e);
		}

		protected override void OnPointerExited(PointerRoutedEventArgs e)
		{
			base.OnPointerExited(e);
			//base.OnLostFocus(e);
		}
	}
}