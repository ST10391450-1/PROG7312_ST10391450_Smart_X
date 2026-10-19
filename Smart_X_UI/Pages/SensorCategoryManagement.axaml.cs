
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Smart_X_UI.Models;
using Smart_X_UI.Services;

namespace Smart_X_UI.Pages;

public partial class SensorCategoryManagement : UserControl
{
    private readonly SensorCategoryService _categoryService =
        new(ApiClient.HttpClient);

    private readonly ObservableCollection<SensorCategory> _categories = new();

    private Guid? _editingCategoryId;
    private bool _saving;

    public event EventHandler? BackRequested;

    public SensorCategoryManagement()
    {
        InitializeComponent();

        CategoriesItemsControl.ItemsSource = _categories;

        _ = LoadCategoriesAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _categoryService.GetAllAsync();

            _categories.Clear();

            foreach (var category in categories.OrderBy(
                         category => category.Name,
                         StringComparer.OrdinalIgnoreCase))
            {
                _categories.Add(category);
            }

            CategoryCountText.Text =
                $"{_categories.Count} {(_categories.Count == 1 ? "category" : "categories")}";

            EmptyCategoriesText.IsVisible = _categories.Count == 0;

            SetStatus(
                $"Loaded {_categories.Count} categories.",
                Brushes.LightGreen);
        }
        catch (Exception ex)
        {
            SetStatus(
                $"Could not load categories: {ex.Message}",
                Brushes.OrangeRed);
        }
    }

    private async void SaveCategoryButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (_saving)
            return;

        var name = CategoryNameTextBox.Text?.Trim() ?? string.Empty;
        var description =
            CategoryDescriptionTextBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Enter a category name.", Brushes.OrangeRed);
            CategoryNameTextBox.Focus();
            return;
        }

        if (name.Length > 100)
        {
            SetStatus(
                "The category name cannot exceed 100 characters.",
                Brushes.OrangeRed);
            return;
        }

        if (description.Length > 500)
        {
            SetStatus(
                "The description cannot exceed 500 characters.",
                Brushes.OrangeRed);
            return;
        }

        var duplicate = _categories.Any(category =>
            string.Equals(
                category.Name.Trim(),
                name,
                StringComparison.OrdinalIgnoreCase)
            && category.Id != _editingCategoryId);

        if (duplicate)
        {
            SetStatus(
                "A category with that name already exists.",
                Brushes.OrangeRed);
            return;
        }

        _saving = true;
        SaveCategoryButton.IsEnabled = false;

        try
        {
            var category = new SensorCategory
            {
                Name = name,
                Description = description
            };

            if (_editingCategoryId.HasValue)
            {
                category.Id = _editingCategoryId.Value;

                await _categoryService.UpdateAsync(
                    _editingCategoryId.Value,
                    category);

                ResetForm();

                await LoadCategoriesAsync();

                SetStatus("Category updated successfully.", Brushes.LightGreen);
            }
            else
            {
                await _categoryService.CreateAsync(category);

                ResetForm();

                await LoadCategoriesAsync();

                SetStatus("Category created successfully.", Brushes.LightGreen);
            }
        }
        catch (HttpRequestException ex)
        {
            SetStatus(
                $"API request failed: {ex.Message}",
                Brushes.OrangeRed);
        }
        catch (Exception ex)
        {
            SetStatus(
                $"Could not save category: {ex.Message}",
                Brushes.OrangeRed);
        }
        finally
        {
            _saving = false;
            SaveCategoryButton.IsEnabled = true;
        }
    }

    private void EditCategoryButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SensorCategory category })
            return;

        _editingCategoryId = category.Id;
        CategoryNameTextBox.Text = category.Name;
        CategoryDescriptionTextBox.Text = category.Description;

        CategoryFormTitleText.Text = "EDIT CATEGORY";
        SaveCategoryButton.Content = "Update Category";

        SetStatus(
            $"Editing {category.Name}.",
            Brushes.LightBlue);

        CategoryNameTextBox.Focus();
    }

    private async void DeleteCategoryButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SensorCategory category })
            return;

        try
        {
            await _categoryService.DeleteAsync(category.Id);

            if (_editingCategoryId == category.Id)
                ResetForm();

            await LoadCategoriesAsync();

            SetStatus(
                $"Category '{category.Name}' deleted.",
                Brushes.LightGreen);
        }
        catch (HttpRequestException ex)
        {
            SetStatus(
                $"Could not delete '{category.Name}': {ex.Message}",
                Brushes.OrangeRed);
        }
        catch (Exception ex)
        {
            SetStatus(
                $"Could not delete category: {ex.Message}",
                Brushes.OrangeRed);
        }
    }

    private async void RefreshButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        await LoadCategoriesAsync();
    }

    private void ClearButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ResetForm();
        SetStatus("Form cleared.", Brushes.LightBlue);
    }

    private void ResetForm()
    {
        _editingCategoryId = null;
        CategoryNameTextBox.Clear();
        CategoryDescriptionTextBox.Clear();
        CategoryFormTitleText.Text = "CREATE CATEGORY";
        SaveCategoryButton.Content = "Save Category";
    }

    private void SetStatus(string message, IBrush foreground)
    {
        CategoryStatusText.Text = message;
        CategoryStatusText.Foreground = foreground;
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}
