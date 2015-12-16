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
	[TemplatePart(Name = "DropDownButton", Type = typeof(Button))]
	[TemplatePart(Name = "DeleteButton", Type = typeof(Button))]
	[TemplatePart(Name = "PopupBorder", Type = typeof(Border))]
	[TemplatePart(Name = "Flyout", Type = typeof(Flyout))]
	[TemplatePart(Name = "PopupListView", Type = typeof(ListView))]
	[TemplatePart(Name = "ContentElement", Type = typeof(ScrollViewer))]
	[TemplatePart(Name = "HeaderContentPresenter", Type = typeof(ContentPresenter))]
	public class LolloTextBox : TextBox
	{
		#region fields
		private ApplicationView _appView = null;
		Button _dropDownButton = null;
		Button _deleteButton = null;
		Border _popupBorder = null;
		Flyout _flyout = null;
		ListView _listView = null;
		ScrollViewer _contentElement = null;
		ContentPresenter _headerContentPresenter = null;
		#endregion fields


		#region properties
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
		public Visibility DeleteButtonVisibility
		{
			get { return (Visibility)GetValue(DeleteButtonVisibilityProperty); }
			protected set { SetValue(DeleteButtonVisibilityProperty, value); }
		}
		public static readonly DependencyProperty DeleteButtonVisibilityProperty =
			DependencyProperty.Register("DeleteButtonVisibility", typeof(Visibility), typeof(LolloTextBox), new PropertyMetadata(Visibility.Collapsed));

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
		#endregion properties


		#region on property changed
		private static void OnIsEmptyValueAllowedEvenIfNotInListChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			if (args == null || args.NewValue == args.OldValue) return;
			(obj as LolloTextBox)?.UpdateDeleteButtonVisibility();
		}

		private static void OnIsEditableDecoratorVisibleChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			if (args == null || args.NewValue == args.OldValue) return;
			(obj as LolloTextBox)?.UpdateEDV();
		}

		private static void OnIsDropDownButtonVisibleChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			if (args == null || args.NewValue == args.OldValue) return;
			(obj as LolloTextBox)?.UpdateDropDownButtonVisibility();
			(obj as LolloTextBox)?.UpdateDeleteButtonVisibility();
		}

		private void OnItemsSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			UpdateDropDownButtonVisibility();
			UpdateDeleteButtonVisibility();
		}

		private static void OnItemsSourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			if (args == null || args.NewValue == args.OldValue) return;

			var ltb = (obj as LolloTextBox);
			if (ltb != null)
			{
				if (args.OldValue is INotifyCollectionChanged) (args.OldValue as INotifyCollectionChanged).CollectionChanged -= ltb.OnItemsSource_CollectionChanged;
				if (args.NewValue is INotifyCollectionChanged) (args.NewValue as INotifyCollectionChanged).CollectionChanged += ltb.OnItemsSource_CollectionChanged;

				ltb.UpdateDropDownButtonVisibility();
				ltb.UpdateDeleteButtonVisibility();
			}
		}

		private static void OnIsReadOnlyChanged(DependencyObject obj, DependencyProperty prop)
		{
			(obj as LolloTextBox)?.UpdateEDV();
			(obj as LolloTextBox)?.UpdateDeleteButtonVisibility();
		}

		private static void OnIsEnabledChanged(DependencyObject obj, DependencyProperty prop)
		{
			(obj as LolloTextBox)?.UpdateEDV();
			(obj as LolloTextBox)?.UpdateDropDownButtonVisibility();
			(obj as LolloTextBox)?.UpdateDeleteButtonVisibility();
		}
		#endregion on property changed


		#region property update methods
		private void UpdateEDV()
		{
			if (!IsReadOnly && IsEnabled && IsEditableDecoratorVisible) EditableDecoratorVisibility = Visibility.Visible;
			else EditableDecoratorVisibility = Visibility.Collapsed;
		}
		private void UpdateDropDownButtonVisibility()
		{
			if (IsEnabled && IsDropDownButtonVisible && (ItemsSource as IList)?.Count > 0) DropDownVisibility = Visibility.Visible;
			else DropDownVisibility = Visibility.Collapsed;
		}
		private void UpdateDeleteButtonVisibility()
		{
			if (IsEnabled && (!IsReadOnly || (IsDropDownButtonVisible && (ItemsSource as IList)?.Count > 0 && IsEmptyValueAllowedEvenIfNotInList))) DeleteButtonVisibility = Visibility.Visible;
			else DeleteButtonVisibility = Visibility.Collapsed;
		}
		#endregion property update methods


		#region construct and init
		public LolloTextBox() : base()
		{
			DefaultStyleKey = "LolloTextBoxStyle";

			RegisterPropertyChangedCallback(TextBox.IsReadOnlyProperty, OnIsReadOnlyChanged);
			RegisterPropertyChangedCallback(TextBox.IsEnabledProperty, OnIsEnabledChanged);
		}

		protected override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			_dropDownButton = GetTemplateChild("DropDownButton") as Button;
			if (_dropDownButton != null)
			{
				_dropDownButton.Tapped += OnDropDownButton_Tapped;
			}

			_deleteButton = GetTemplateChild("DeleteButton") as Button;
			if (_deleteButton != null)
			{
				_deleteButton.Tapped += OnDeleteButton_Tapped;
			}

			_popupBorder = GetTemplateChild("PopupBorder") as Border;

			_flyout = GetTemplateChild("Flyout") as Flyout;

			_listView = GetTemplateChild("PopupListView") as ListView;
			if (_listView != null)
			{
				_listView.ItemClick += OnListView_ItemClick;
			}

			_contentElement = GetTemplateChild("ContentElement") as ScrollViewer;

			_headerContentPresenter = GetTemplateChild("HeaderContentPresenter") as ContentPresenter;

			UpdateEDV();
			UpdateDropDownButtonVisibility();
			UpdateDeleteButtonVisibility();

			//UpdateStates(false);
		}
		#endregion construct and init


		#region user actions
		private void OnDropDownButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			if (_listView == null || _flyout == null) return;

			if (ListItemTemplate != null) _listView.ItemTemplate = ListItemTemplate;
			_listView.ItemsSource = ItemsSource;

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
		}

		private void OnListView_ItemClick(object sender, ItemClickEventArgs e)
		{
			if (e == null || e.ClickedItem == null) return;

			string selItem = e.ClickedItem.GetType()?.GetProperties()?
				.FirstOrDefault(pro => pro.Name == DisplayMemberPath)?
				.GetValue(e.ClickedItem)?.ToString();

			_listView.ItemsSource = null; // reset before hiding, we don't want leftovers

			try
			{
				_flyout?.Hide();
				Debug.WriteLine("Flyout closed");
			}
			catch { }

			SetValue(TextProperty, selItem);
			Debug.WriteLine("new value set = " + selItem);

			var tb = GetBindingExpression(TextProperty);
			if (tb?.ParentBinding?.Mode == Windows.UI.Xaml.Data.BindingMode.TwoWay)
			{
				tb.UpdateSource();
				Debug.WriteLine("binding source updated");
			}
			else
			{
				Debug.WriteLine("binding source NOT updated");
			}
		}

		private void OnDeleteButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			// ClearValue(TextProperty); // NO! this breaks two-way bindings.
			SetValue(TextProperty, string.Empty);			
		}
		#endregion user actions

		protected override Size MeasureOverride(Size availableSize)
		{
			//bool stop = false;
			//if (Name == "Test" && stop) Debugger.Break();

			//_contentElement.Measure(availableSize);
			//_headerContentPresenter.Measure(availableSize);

			// foreach(var child in Chi)
			var measure = base.MeasureOverride(availableSize);
			Size output = Size.Empty;

			if (HorizontalContentAlignment == HorizontalAlignment.Stretch && !double.IsPositiveInfinity(availableSize.Width))
			{
				output.Width = availableSize.Width;
			}
			else
			{
				output.Width = measure.Width;
			}
			if (VerticalContentAlignment == VerticalAlignment.Stretch && !double.IsPositiveInfinity(availableSize.Height))
			{
				output.Height = availableSize.Height;
			}
			else
			{
				output.Height = measure.Height;
			}
			return output;
		}
		//protected override Size ArrangeOverride(Size finalSize)
		//{
		//	return base.ArrangeOverride(finalSize);
		//}
	}

	public class ButtonBaseExtension
	{
		public static DependencyProperty SymbolProperty =
			DependencyProperty.RegisterAttached("Symbol",
												typeof(Symbol),
												typeof(ButtonBaseExtension),
												new PropertyMetadata(Symbol.SolidStar));
		public static Symbol GetSymbol(DependencyObject target)
		{
			return (Symbol)target.GetValue(SymbolProperty);
		}
		public static void SetSymbol(DependencyObject target, Symbol value)
		{
			target.SetValue(SymbolProperty, value);
		}
	}
}