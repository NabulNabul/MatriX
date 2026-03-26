using ReactiveUI;
using System.Reactive;

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
    }
}
