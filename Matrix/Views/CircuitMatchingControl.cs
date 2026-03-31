using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Matrix.ViewModels;

namespace Matrix.Views
{
    public partial class CircuitMatchingControl : UserControl
    {
        public CircuitMatchingControl()
        {
            InitializeComponent();
        }

        private async void OnLoadTouchstoneClick(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Touchstone File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Touchstone files (.s1p)") { Patterns = new[] { "*.s1p" } },
                    new FilePickerFileType("All files") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count >= 1 && DataContext is NetworkAnalyzerViewModel vm)
            {
                await using var stream = await files[0].OpenReadAsync();
                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                vm.ParseTouchstone(lines);
            }
        }

        private async void OnSaveCircuitClick(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null || !(DataContext is NetworkAnalyzerViewModel vm)) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Matching Circuit",
                DefaultExtension = ".json",
                FileTypeChoices = new[] { new FilePickerFileType("JSON files") { Patterns = new[] { "*.json" } } }
            });

            if (file != null)
            {
                var json = vm.GetCircuitAsJson();
                await using var stream = await file.OpenWriteAsync();
                using var writer = new StreamWriter(stream);
                await writer.WriteAsync(json);
            }
        }

        private async void OnLoadCircuitClick(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null || !(DataContext is NetworkAnalyzerViewModel vm)) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Load Matching Circuit",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("JSON files") { Patterns = new[] { "*.json" } } }
            });

            if (files.Count >= 1)
            {
                await using var stream = await files[0].OpenReadAsync();
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();
                vm.LoadCircuitFromJson(json);
            }
        }
    }
}