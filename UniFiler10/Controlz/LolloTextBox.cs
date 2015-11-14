using System;
using System.Collections;
using System.Collections.Generic;
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
	[TemplatePart(Name = "Popup", Type = typeof(Popup))]
	[TemplatePart(Name = "PopupListView", Type = typeof(ListView))]
	public class LolloTextBox : TextBox
	{
		public bool IsEditableDecoratorVisible
		{
			get { return (bool)GetValue(IsEditableDecoratorVisibleProperty); }
			set { SetValue(IsEditableDecoratorVisibleProperty, value); }
		}
		public static readonly DependencyProperty IsEditableDecoratorVisibleProperty =
			DependencyProperty.Register("IsEditableDecoratorVisible", typeof(bool), typeof(LolloTextBox), new PropertyMetadata(true, OnIsEditableDecoratorVisibleChanged));
		private static void OnIsEditableDecoratorVisibleChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			(obj as LolloTextBox)?.UpdateEDV();
		}

		public bool IsDropDownVisible
		{
			get { return (bool)GetValue(IsDropDownVisibleProperty); }
			set { SetValue(IsDropDownVisibleProperty, value); }
		}
		public static readonly DependencyProperty IsDropDownVisibleProperty =
			DependencyProperty.Register("IsDropDownVisible", typeof(bool), typeof(LolloTextBox), new PropertyMetadata(true, OnIsDropDownVisibleChanged));
		private static void OnIsDropDownVisibleChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			(obj as LolloTextBox)?.UpdateDD();
		}

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

		public object ItemsSource
		{
			get { return GetValue(ItemsSourceProperty); }
			set { SetValue(ItemsSourceProperty, value); }
		}
		public static readonly DependencyProperty ItemsSourceProperty =
			DependencyProperty.Register("ItemsSource", typeof(object), typeof(LolloTextBox), new PropertyMetadata(null, OnItemsSourceChanged));
		private static void OnItemsSourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			(obj as LolloTextBox)?.UpdateItemsSource();
			(obj as LolloTextBox)?.UpdateDD();
		}

		public string DisplayMemberPath
		{
			get { return (string)GetValue(DisplayMemberPathProperty); }
			set { SetValue(DisplayMemberPathProperty, value); }
		}
		public static readonly DependencyProperty DisplayMemberPathProperty =
			DependencyProperty.Register("DisplayMemberPath", typeof(string), typeof(LolloTextBox), new PropertyMetadata(null));

		private static void OnIsReadOnlyChanged(DependencyObject obj, DependencyProperty prop)
		{
			(obj as LolloTextBox)?.UpdateEDV();
			(obj as LolloTextBox)?.UpdateDD();
		}

		Border _dropDownBorder = null;
		Popup _popup = null;
		ListView _listView = null;

		public LolloTextBox() : base()
		{
			DefaultStyleKey = "LolloTextBoxFieldValueStyle";

			RegisterPropertyChangedCallback(TextBox.IsReadOnlyProperty, OnIsReadOnlyChanged);
			RegisterPropertyChangedCallback(TextBox.IsEnabledProperty, OnIsReadOnlyChanged);
		}

		protected override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			_dropDownBorder = GetTemplateChild("DropDownBorder") as Border;
			if (_dropDownBorder != null)
			{
				_dropDownBorder.Tapped += OnDropDownBorder_Tapped;
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
				_listView.SelectionChanged += OnListView_SelectionChanged;
			}

			UpdateEDV();
			UpdateItemsSource();
			UpdateDD();
			//UpdateStates(false);
		}

		private void OnListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			string selItem = _listView.SelectedItem.ToString();

			if (!string.IsNullOrEmpty(selItem)) Text = selItem;
			else Text = PlaceholderText;

			if (_popup != null) _popup.IsOpen = false;
		}

		private void OnDropDownBorder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			if (_popup == null || _listView == null) return;

			if (_popup.IsOpen == false)
			{
				_popup.HorizontalOffset = ActualWidth;
				_popup.IsOpen = true;
			}
			else
			{
				_popup.IsOpen = false;
			}
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
		private void UpdateDD()
		{
			if (!IsReadOnly && IsEnabled && IsDropDownVisible && _listView != null && _listView.ItemsSource != null) DropDownVisibility = Visibility.Visible;
			else DropDownVisibility = Visibility.Collapsed;
		}
		private void UpdateItemsSource()
		{
			if (_listView != null)
			{
				if (ItemsSource is IList)
				{
					var newItemsSource = new List<string>();
					foreach (var item in (ItemsSource as IList))
					{
						var pi1 = item?.GetType()?.GetProperties()?.FirstOrDefault(pro => pro.Name == DisplayMemberPath)?.GetValue(item)?.ToString();
						if (pi1 != null) newItemsSource.Add(pi1);
					}
					_listView.ItemsSource = newItemsSource;
				}
				else
				{
					_listView.ItemsSource = null;
				}
			}
		}
	}
}