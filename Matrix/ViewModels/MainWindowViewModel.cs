using ReactiveUI;
using System.Reactive;
using System.Collections.ObjectModel;
using Matrix.Models;

namespace Matrix.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private string _greeting = "Welcome to Avalonia!";
        public string Greeting
        {
            get => _greeting;
            set => this.RaiseAndSetIfChanged(ref _greeting, value);
        }

        public ObservableCollection<SParameterData> MeasuredData { get; } = new ObservableCollection<SParameterData>
        {
            new SParameterData { Frequency = 1, Real = 0.5, Imaginary = 0.2 },
            new SParameterData { Frequency = 2, Real = 0.3, Imaginary = 0.4 },
            new SParameterData { Frequency = 3, Real = -0.2, Imaginary = 0.6 },
            new SParameterData { Frequency = 4, Real = -0.5, Imaginary = 0.1 },
            new SParameterData { Frequency = 5, Real = -0.7, Imaginary = -0.3 }
        };

        public ObservableCollection<SParameterData> MatchedData { get; } = new ObservableCollection<SParameterData>
        {
            new SParameterData { Frequency = 1, Real = 0.7, Imaginary = -0.1 },
            new SParameterData { Frequency = 2, Real = 0.4, Imaginary = -0.3 },
            new SParameterData { Frequency = 3, Real = 0.0, Imaginary = -0.5 },
            new SParameterData { Frequency = 4, Real = -0.3, Imaginary = -0.6 },
            new SParameterData { Frequency = 5, Real = -0.6, Imaginary = -0.2 }
        };
    }
}
