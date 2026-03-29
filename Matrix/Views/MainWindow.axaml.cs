using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Matrix.ViewModels;

namespace Matrix.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new NetworkAnalyzerViewModel();
        }

        private async void OnSaveChartClick(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Chart as PNG",
                DefaultExtension = ".png",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } }
                }
            });

            if (file != null)
            {
                var chartControl = this.FindControl<Matrix.Controls.SmithChartControl>("MainSmithChart");
                if (chartControl != null)
                {
                    using var stream = await file.OpenWriteAsync();
                    chartControl.SaveAsPng(stream);
                }
            }
        }
    }
}
