using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UniFiler10.Converters;
using UniFiler10.Data.Metadata;
using UniFiler10.ViewModels;
using Utilz.Controlz;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;


// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class MetaBriefcaseView : ObservableControl
	{
		public double MetaItemTextWidth
		{
			get { return (double)GetValue(MetaItemTextWidthProperty); }
			set { SetValue(MetaItemTextWidthProperty, value); }
		}
		public static readonly DependencyProperty MetaItemTextWidthProperty =
			DependencyProperty.Register("MetaItemTextWidth", typeof(double), typeof(MetaBriefcaseView), new PropertyMetadata(150.0));

		public SettingsVM VM
		{
			get { return (SettingsVM)GetValue(VMProperty); }
			set { SetValue(VMProperty, value); }
		}
		public static readonly DependencyProperty VMProperty =
			DependencyProperty.Register("VM", typeof(SettingsVM), typeof(MetaBriefcaseView), new PropertyMetadata(null));


		public MetaBriefcaseView()
		{
			double metaItemTextWidth = 0.0;
			double.TryParse(Application.Current.Resources["MetaItemTextWidth"].ToString(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowThousands | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out metaItemTextWidth);
			MetaItemTextWidth = metaItemTextWidth;

			DataContextChanged += OnDataContextChanged;
			InitializeComponent();
		}

		private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
		{
			Task ccc = RunInUiThreadAsync(() => VM?.OnDataContextChanged());
		}

		private void OnCategoryListView_ItemClick(object sender, ItemClickEventArgs e)
		{
			Task upd = VM?.SetCurrentCategoryAsync(e.ClickedItem as Category);
		}

		private void OnSelectCategory_Click(object sender, RoutedEventArgs e)
		{
			Task upd = VM?.SetCurrentCategoryAsync((sender as FrameworkElement)?.DataContext as Category);
		}

		private void OnUnassignedFieldDescriptionsListView_ItemClick(object sender, ItemClickEventArgs e)
		{
			Task sel = SelectUnaFldDsc(e?.ClickedItem as FieldDescription);
		}

		private void OnSelectUnassignedFldDsc_Click(object sender, RoutedEventArgs e)
		{
			Task sel = SelectUnaFldDsc((sender as FrameworkElement)?.DataContext as FieldDescription);
		}

		private async Task SelectUnaFldDsc(FieldDescription fldDsc)
		{
			var vm = VM;
			if (vm != null)
			{
				await vm.SetCurrentFieldDescriptionAsync(fldDsc);
				AssignedLV.DeselectRange(new ItemIndexRange(AssignedLV.SelectedIndex, 1));
			}
		}

		private void OnAssignedFieldDescriptionsListView_ItemClick(object sender, ItemClickEventArgs e)
		{
			Task sel = SelectAssFldDsc(e?.ClickedItem as FieldDescription);
		}

		private void OnSelectAssignedFldDsc_Click(object sender, RoutedEventArgs e)
		{
			Task sel = SelectAssFldDsc((sender as FrameworkElement)?.DataContext as FieldDescription);
		}

		private async Task SelectAssFldDsc(FieldDescription fldDsc)
		{
			var vm = VM;
			if (vm != null)
			{
				await vm.SetCurrentFieldDescriptionAsync(fldDsc);
				UnassignedLV.DeselectRange(new ItemIndexRange(UnassignedLV.SelectedIndex, 1));
			}
		}

		private void OnAssignFieldToCurrentCat_Click(object sender, RoutedEventArgs e)
		{
			Task ass = VM?.AssignFieldDescriptionToCurrentCategoryAsync((sender as FrameworkElement)?.DataContext as FieldDescription);
		}
		private void OnAssignFieldFromCurrentCategory_Click(object sender, RoutedEventArgs e)
		{
			Task una = VM?.UnassignFieldDescriptionFromCurrentCategoryAsync((sender as FrameworkElement)?.DataContext as FieldDescription);
		}

		private void OnAddField_Click(object sender, RoutedEventArgs e)
		{
			Task add = VM?.AddFieldDescriptionAsync();
		}

		private void OnDeleteField_Click(object sender, RoutedEventArgs e)
		{
			Task del = VM?.RemoveFieldDescriptionAsync((sender as FrameworkElement)?.DataContext as FieldDescription);
		}

		private void OnAddFieldValue_Click(object sender, RoutedEventArgs e)
		{
			Task add = VM?.AddPossibleValueToCurrentFieldDescriptionAsync();
		}
		private void OnDeletePossibleValue_Click(object sender, RoutedEventArgs e)
		{
			Task del = VM?.RemovePossibleValueFromCurrentFieldDescriptionAsync((sender as FrameworkElement)?.DataContext as FieldValue);
		}

		private void OnAddCategory_Click(object sender, RoutedEventArgs e)
		{
			Task add = VM?.AddCategoryAsync();
		}

		private void OnDeleteCategory_Click(object sender, RoutedEventArgs e)
		{
			Task del = VM?.RemoveCategoryAsync((sender as FrameworkElement)?.DataContext as Category);
		}

		private void OnFieldUnassignCommand_Loaded(object sender, RoutedEventArgs e)
		{
			var mbc = MetaBriefcase.OpenInstance;
			if (mbc != null)
			{
				var fldDsc = (sender as FrameworkElement)?.DataContext as FieldDescription;
				if (fldDsc != null)
				{
					string currCatId = mbc.CurrentCategoryId;
					if (!string.IsNullOrEmpty(currCatId))
					{
						if (fldDsc.JustAssignedToCats.Contains(currCatId)) Allow(sender);
						else AllowIfElevated(sender);
					}
					else Allow(sender);
				}
				else Allow(sender);
			}
			else Forbid(sender);
		}

		private void OnFieldDeleteCommand_Loaded(object sender, RoutedEventArgs e)
		{
			var mbc = MetaBriefcase.OpenInstance;
			if (mbc != null)
			{
				var fldDsc = (sender as FrameworkElement)?.DataContext as FieldDescription;
				if (fldDsc != null)
				{
					// LOLLO TODO the following line was buggy, check it
					var catsWhereThisFieldWasAssignedBefore = mbc.Categories.Where(cat => cat?.FieldDescriptionIds != null && !fldDsc.JustAssignedToCats.Contains(cat.Id) && cat.FieldDescriptionIds.Contains(fldDsc.Id));

					if (catsWhereThisFieldWasAssignedBefore?.Any() == true) AllowIfElevated(sender);
					else Allow(sender);
				}
				else Allow(sender);
			}
			else Forbid(sender);
		}

		private static void Forbid(object sender)
		{
			(sender as FrameworkElement).Visibility = Visibility.Collapsed;
			(sender as ButtonBase).Foreground = (Brush)(new FalseToFlashyConverter().Convert(false, null, null, string.Empty));
		}

		private static void AllowIfElevated(object sender)
		{
			(sender as FrameworkElement).Visibility = SettingsVM.GetIsElevated() ? Visibility.Visible : Visibility.Collapsed;

			(sender as ButtonBase).Foreground = (Brush)(new FalseToFlashyConverter().Convert(false, null, null, string.Empty));
		}

		private static void Allow(object sender)
		{
			(sender as FrameworkElement).Visibility = Visibility.Visible;
			(sender as ButtonBase).Foreground = (Brush)(new FalseToFlashyConverter().Convert(true, null, null, string.Empty));
		}
	}
}