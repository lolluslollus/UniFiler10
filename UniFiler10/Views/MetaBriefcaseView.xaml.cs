using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UniFiler10.Data.Metadata;
using UniFiler10.ViewModels;
using Utilz.Controlz;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

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

			InitializeComponent();
		}

		private void OnCategoryListView_ItemClick(object sender, ItemClickEventArgs e)
		{
			Task upd = VM?.SetCurrentCategoryAsync(e.ClickedItem as Category);
		}

		private void OnUnassignedFieldDescriptionsListView_ItemClick(object sender, ItemClickEventArgs e)
		{
			Task sel = SelectUnaFldDsc((e?.ClickedItem as SettingsVM.FieldDescriptionPlus)?.FieldDescription);
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
			Task sel = SelectAssFldDsc((e?.ClickedItem as SettingsVM.FieldDescriptionPlus)?.FieldDescription);
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
			Task ass = VM?.AssignFieldDescriptionToCurrentCategoryAsync(((sender as FrameworkElement)?.DataContext as SettingsVM.FieldDescriptionPlus)?.FieldDescription);
		}
		private void OnUnassignFieldFromCurrentCategory_Click(object sender, RoutedEventArgs e)
		{
			Task una = VM?.UnassignFieldDescriptionFromCurrentCategoryAsync(((sender as FrameworkElement)?.DataContext as SettingsVM.FieldDescriptionPlus)?.FieldDescription);
		}

		private void OnAddField_Click(object sender, RoutedEventArgs e)
		{
			Task add = VM?.AddFieldDescriptionAsync();// LOLLO TODO select the newly added item
		}

		private void OnDeleteField_Click(object sender, RoutedEventArgs e)
		{
			Task del = VM?.RemoveFieldDescriptionAsync(((sender as FrameworkElement)?.DataContext as SettingsVM.FieldDescriptionPlus)?.FieldDescription);
		}

		private void OnAddFieldValue_Click(object sender, RoutedEventArgs e)
		{
			Task add = VM?.AddPossibleValueToCurrentFieldDescriptionAsync();// LOLLO TODO select the newly added item
		}
		private void OnDeletePossibleValue_Click(object sender, RoutedEventArgs e)
		{
			Task del = VM?.RemovePossibleValueFromCurrentFieldDescriptionAsync((sender as FrameworkElement)?.DataContext as FieldValue);
		}

		private void OnAddCategory_Click(object sender, RoutedEventArgs e)
		{
			Task add = VM?.AddCategoryAsync();          // LOLLO TODO select the newly added item
		}

		private void OnDeleteCategory_Click(object sender, RoutedEventArgs e)
		{
			Task del = VM?.RemoveCategoryAsync((sender as FrameworkElement)?.DataContext as Category);
		}

		public void Refresh()
		{
			VM?.Refresh();
		}
	}
}