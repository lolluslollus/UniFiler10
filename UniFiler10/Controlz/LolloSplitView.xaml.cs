using Utilz.Controlz;
using Windows.UI.Xaml;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Controlz
{
	public sealed partial class LolloSplitView : ObservableControl
    {
        private const double OPEN_PANE_WIDTH_INITIAL = 100.0;
        private const double CLOSED_PANE_WIDTH_INITIAL = 50.0;
        private const bool IS_PANE_OPEN_INITIAL = false;

        private GridLength _paneWidth = new GridLength(CLOSED_PANE_WIDTH_INITIAL, GridUnitType.Pixel);
        public GridLength PaneWidth { get { return _paneWidth; } private set { if (_paneWidth != value) { _paneWidth = value; RaisePropertyChanged_UI(); } } }

        public UIElement PaneContent
        {
            get { return (UIElement)GetValue(PaneContentProperty); }
            set { SetValue(PaneContentProperty, value); }
        }
        public static readonly DependencyProperty PaneContentProperty =
            DependencyProperty.Register("PaneContent", typeof(UIElement), typeof(LolloSplitView), new PropertyMetadata(null, OnPaneContentChanged));
        private static void OnPaneContentChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue != args.OldValue)
            {
                var instance = obj as LolloSplitView;
	            if (instance != null)
	            {
		            var newValue = (UIElement) args.NewValue;

		            instance.PaneScrollViewer.Child = newValue;
		            // instance.PaneScrollViewer.Content = newValue;
		            // ReplaceColumnContent(instance, newValue, 0);
	            }
            }
        }

        public UIElement BodyContent
        {
            get { return (UIElement)GetValue(BodyContentProperty); }
            set { SetValue(BodyContentProperty, value); }
        }
        public static readonly DependencyProperty BodyContentProperty =
            DependencyProperty.Register("BodyContent", typeof(UIElement), typeof(LolloSplitView), new PropertyMetadata(null, OnBodyContentChanged));
        private static void OnBodyContentChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue != args.OldValue)
            {
                var instance = obj as LolloSplitView;
	            if (instance != null)
	            {
		            var newValue = (UIElement) args.NewValue;

		            instance.BodyScrollViewer.Child = newValue;
		            // instance.BodyScrollViewer.Content = newValue;
		            // ReplaceColumnContent(instance, newValue, 1);
	            }
            }
        }

        public double ClosedPaneLength
        {
            get { return (double)GetValue(ClosedPaneLengthProperty); }
            set { SetValue(ClosedPaneLengthProperty, value); }
        }
        public static readonly DependencyProperty ClosedPaneLengthProperty =
            DependencyProperty.Register("ClosedPaneLength", typeof(double), typeof(LolloSplitView), new PropertyMetadata(CLOSED_PANE_WIDTH_INITIAL, OnClosedPaneLengthChanged));
        private static void OnClosedPaneLengthChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue != args.OldValue)
            {
                var instance = obj as LolloSplitView;
	            if (instance != null)
	            {
		            var newValue = (double) args.NewValue;
		            if (CheckLength(newValue))
		            {
			            if (!instance.IsPaneOpen)
			            {
				            instance.PaneWidth = new GridLength(newValue, GridUnitType.Pixel);
			            }
		            }
	            }
            }
        }

        public double OpenPaneLength
        {
            get { return (double)GetValue(OpenPaneLengthProperty); }
            set { SetValue(OpenPaneLengthProperty, value); }
        }
        public static readonly DependencyProperty OpenPaneLengthProperty =
            DependencyProperty.Register("OpenPaneLength", typeof(double), typeof(LolloSplitView), new PropertyMetadata(OPEN_PANE_WIDTH_INITIAL, OnOpenPaneLengthChanged));
        private static void OnOpenPaneLengthChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue != args.OldValue)
            {
                var instance = obj as LolloSplitView;
	            if (instance != null)
	            {
		            var newValue = (double) args.NewValue;
		            if (CheckLength(newValue))
		            {
			            if (instance.IsPaneOpen)
			            {
				            instance.PaneWidth = new GridLength(newValue, GridUnitType.Pixel);
			            }
		            }
	            }
            }
        }

        public bool IsPaneOpen
        {
            get { return (bool)GetValue(IsPaneOpenProperty); }
            set { SetValue(IsPaneOpenProperty, value); }
        }
        public static readonly DependencyProperty IsPaneOpenProperty =
            DependencyProperty.Register("IsPaneOpen", typeof(bool), typeof(LolloSplitView), new PropertyMetadata(IS_PANE_OPEN_INITIAL, OnIsPaneOpenChanged));
        private static void OnIsPaneOpenChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue != args.OldValue)
            {
                var instance = obj as LolloSplitView;
	            if (instance != null)
	            {
		            var newValue = (bool) args.NewValue;
		            instance.PaneWidth = newValue ? new GridLength(instance.OpenPaneLength, GridUnitType.Pixel) : new GridLength(instance.ClosedPaneLength, GridUnitType.Pixel);
	            }
            }
        }

        public LolloSplitView()
        {
            InitializeComponent();
        }

        private static bool CheckLength(double length)
        {
            return length >= 0;
        }

        //private static void ReplaceColumnContent(LolloSplitView instance, UIElement newValue, int columnIndex)
        //{
        //    // remove current pane content
        //    List<UIElement> paneChildren = new List<UIElement>();
        //    foreach (var child in instance.LayoutRoot.Children)
        //    {
        //        if ((int)child.GetValue(Grid.ColumnProperty) == columnIndex)
        //        {
        //            paneChildren.Add(child);
        //        }
        //    }
        //    foreach (var child in paneChildren)
        //    {
        //        instance.LayoutRoot.Children.Remove(child);
        //    }
        //    // add new pane content
        //    if (newValue != null)
        //    {
        //        instance.LayoutRoot.Children.Add(newValue);
        //        newValue.SetValue(Grid.ColumnProperty, columnIndex);
        //    }
        //}

    }
}
