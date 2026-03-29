using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using System.Text.Json;
using System.Windows.Input;
using Matrix.Models;
using ReactiveUI;

namespace Matrix.ViewModels
{
    public class AvailableComponentItem
    {
        public string Name { get; set; } = string.Empty;
        public ComponentType Type { get; set; }
        public string IconPath { get; set; } = string.Empty;
    }

    public class NetworkAnalyzerViewModel : ReactiveObject
    {
        // 계측기 등에서 측정된 원본 S-Parameter 데이터
        private ObservableCollection<SParameterData> _measuredData;
        public ObservableCollection<SParameterData> MeasuredData
        {
            get => _measuredData;
            set => this.RaiseAndSetIfChanged(ref _measuredData, value);
        }

        // 사용자가 스미스 차트 매칭을 위해 추가한 소자 리스트
        public ObservableCollection<CircuitComponent> MatchingCircuit { get; }

        // 매칭 회로가 적용된 후의 최종 S-Parameter (스미스 차트 바인딩용)
        private ObservableCollection<SParameterData> _matchedData;
        public ObservableCollection<SParameterData> MatchedData
        {
            get => _matchedData;
            set => this.RaiseAndSetIfChanged(ref _matchedData, value);
        }

        // 매칭 회로의 각 단계를 거친 S-Parameter 경로 (스미스 차트 바인딩용)
        public ObservableCollection<List<SParameterData>> MatchingPaths { get; private set; }

        // UI에 표시할 주파수 목록
        public ObservableCollection<double> AvailableFrequencies { get; }

        // UI에 표시할 소자 추가 버튼 정보 목록 (아이콘 포함)
        public ObservableCollection<AvailableComponentItem> AvailableComponentItems { get; }

        // 사용자가 선택한 주파수
        private double? _selectedFrequency;
        public double? SelectedFrequency
        {
            get => _selectedFrequency;
            set => this.RaiseAndSetIfChanged(ref _selectedFrequency, value);
        }

        // VSWR 원 표시 여부
        private bool _showVswrCircle = true;
        public bool ShowVswrCircle
        {
            get => _showVswrCircle;
            set => this.RaiseAndSetIfChanged(ref _showVswrCircle, value);
        }

        // 기준 VSWR
        private double _targetVswr = 2.0;
        public double TargetVswr
        {
            get => _targetVswr;
            set => this.RaiseAndSetIfChanged(ref _targetVswr, value);
        }

        private string _vswrText = "VSWR: N/A";
        public string VswrText
        {
            get => _vswrText;
            set => this.RaiseAndSetIfChanged(ref _vswrText, value);
        }

        private string _returnLossText = "Return Loss: N/A";
        public string ReturnLossText
        {
            get => _returnLossText;
            set => this.RaiseAndSetIfChanged(ref _returnLossText, value);
        }

        private const double Z0 = 50.0; // Characteristic Impedance
        private readonly Dictionary<CircuitComponent, IDisposable> _componentSubscriptions = new();

        public ICommand AddComponentCommand { get; }
        public ICommand RemoveComponentCommand { get; }
        public ICommand ClearFrequencySelectionCommand { get; }

        public NetworkAnalyzerViewModel()
        {
            // Sample data for demonstration
            _measuredData = new ObservableCollection<SParameterData>
            {
                new SParameterData { Frequency = 1, Real = 0.5, Imaginary = 0.2 },
                new SParameterData { Frequency = 2, Real = 0.3, Imaginary = 0.4 },
                new SParameterData { Frequency = 3, Real = -0.2, Imaginary = 0.6 },
                new SParameterData { Frequency = 4, Real = -0.5, Imaginary = 0.1 },
                new SParameterData { Frequency = 5, Real = -0.7, Imaginary = -0.3 }
            };
            _matchedData = new ObservableCollection<SParameterData>();
            MatchingPaths = new ObservableCollection<List<SParameterData>>();
            MatchingCircuit = new ObservableCollection<CircuitComponent>();
            AvailableFrequencies = new ObservableCollection<double>();
            
            AvailableComponentItems = new ObservableCollection<AvailableComponentItem>
            {
                new AvailableComponentItem { Name = "Series L", Type = ComponentType.SeriesInductor, IconPath = "M 5,20 C 5,5 15,5 15,20 C 15,5 25,5 25,20 C 25,5 35,5 35,20" },
                new AvailableComponentItem { Name = "Series C", Type = ComponentType.SeriesCapacitor, IconPath = "M 5,20 L 17,20 M 17,10 L 17,30 M 23,10 L 23,30 M 23,20 L 35,20" },
                new AvailableComponentItem { Name = "Shunt L", Type = ComponentType.ShuntInductor, IconPath = "M 20,5 C 35,5 35,15 20,15 C 35,15 35,25 20,25 C 35,25 35,35 20,35" },
                new AvailableComponentItem { Name = "Shunt C", Type = ComponentType.ShuntCapacitor, IconPath = "M 20,5 L 20,17 M 10,17 L 30,17 M 10,23 L 30,23 M 20,23 L 20,35" }
            };

            foreach (var data in _measuredData)
            {
                AvailableFrequencies.Add(data.Frequency);
            }

            // Subscribe to changes
            MatchingCircuit.CollectionChanged += OnMatchingCircuitChanged;
            this.WhenAnyValue(vm => vm.MeasuredData).Subscribe(_ => RecalculateMatchedData());
            this.WhenAnyValue(vm => vm.SelectedFrequency).Subscribe(_ => RecalculateMatchedData());

            ClearFrequencySelectionCommand = ReactiveCommand.Create(() => { SelectedFrequency = null; });


            AddComponentCommand = ReactiveCommand.Create<ComponentType>(type =>
            {
                // Use a default value that makes sense
                double defaultValue = (type == ComponentType.SeriesInductor || type == ComponentType.ShuntInductor) ? 10 : 1;
                MatchingCircuit.Add(new CircuitComponent { Type = type, Value = defaultValue });
            });

            RemoveComponentCommand = ReactiveCommand.Create<CircuitComponent>(component => { MatchingCircuit.Remove(component); });


            // Initial calculation
            RecalculateMatchedData();
        }

        public string GetCircuitAsJson()
        {
            return JsonSerializer.Serialize(MatchingCircuit, new JsonSerializerOptions { WriteIndented = true });
        }

        public void LoadCircuitFromJson(string json)
        {
            try
            {
                var components = JsonSerializer.Deserialize<List<CircuitComponent>>(json);
                if (components != null)
                {
                    MatchingCircuit.Clear();
                    foreach (var comp in components)
                    {
                        MatchingCircuit.Add(comp);
                    }
                }
            }
            catch
            {
                // TODO: JSON 파싱 실패 시 예외 처리 (예: 알림창)
            }
        }

        public void ParseTouchstone(IEnumerable<string> lines)
        {
            var newData = new List<SParameterData>();
            double freqMultiplier = 1e9; // Default GHz
            string format = "MA"; // Default Magnitude/Angle

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("!"))
                    continue;

                if (trimmed.StartsWith("#"))
                {
                    var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        string upper = part.ToUpper();
                        if (upper == "HZ") freqMultiplier = 1.0;
                        else if (upper == "KHZ") freqMultiplier = 1e3;
                        else if (upper == "MHZ") freqMultiplier = 1e6;
                        else if (upper == "GHZ") freqMultiplier = 1e9;
                        else if (upper == "MA" || upper == "DB" || upper == "RI") format = upper;
                    }
                    continue;
                }

                // Data line: freq, param1, param2
                var dataParts = trimmed.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (dataParts.Length >= 3 && double.TryParse(dataParts[0], out double f) &&
                    double.TryParse(dataParts[1], out double p1) && double.TryParse(dataParts[2], out double p2))
                {
                    double freqGHz = (f * freqMultiplier) / 1e9;
                    double real = 0, imag = 0;

                    if (format == "RI") { real = p1; imag = p2; }
                    else if (format == "MA")
                    {
                        double angleRad = p2 * Math.PI / 180.0;
                        real = p1 * Math.Cos(angleRad); imag = p1 * Math.Sin(angleRad);
                    }
                    else if (format == "DB")
                    {
                        double mag = Math.Pow(10, p1 / 20.0);
                        double angleRad = p2 * Math.PI / 180.0;
                        real = mag * Math.Cos(angleRad); imag = mag * Math.Sin(angleRad);
                    }
                    newData.Add(new SParameterData { Frequency = freqGHz, Real = real, Imaginary = imag });
                }
            }
            if (newData.Any())
            {
                MeasuredData = new ObservableCollection<SParameterData>(newData);
                AvailableFrequencies.Clear();
                foreach (var freq in MeasuredData.Select(d => d.Frequency))
                    AvailableFrequencies.Add(freq);
                SelectedFrequency = null;
            }
        }

        private void OnMatchingCircuitChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (CircuitComponent item in e.OldItems)
                {
                    if (_componentSubscriptions.TryGetValue(item, out var subscription))
                    {
                        subscription.Dispose();
                        _componentSubscriptions.Remove(item);
                    }
                }
            }
            if (e.NewItems != null)
            {
                SubscribeToComponentChanges();
            }
            RecalculateMatchedData();
        }

        private void SubscribeToComponentChanges()
        {
            foreach (var component in MatchingCircuit)
            {
                if (!_componentSubscriptions.ContainsKey(component))
                {
                    _componentSubscriptions[component] = component.WhenAnyValue(x => x.Value)
                        .Throttle(TimeSpan.FromMilliseconds(50), RxApp.MainThreadScheduler)
                        .Subscribe(_ => RecalculateMatchedData());
                }
            }
        }

        private void RecalculateMatchedData()
        {
            if (MeasuredData == null)
            {
                MatchedData.Clear();
                MatchingPaths.Clear();
                return;
            }

            var pointsToProcess = MeasuredData.AsEnumerable();
            if (SelectedFrequency.HasValue)
            {
                pointsToProcess = MeasuredData.Where(p => Math.Abs(p.Frequency - SelectedFrequency.Value) < 1e-9);
            }

            var newMatchedData = new List<SParameterData>();
            var newMatchingPaths = new List<List<SParameterData>>();

            foreach (var measuredPoint in pointsToProcess)
            {
                var pathForFrequency = new List<SParameterData>();
                var gamma = new Complex(measuredPoint.Real, measuredPoint.Imaginary);

                // Add starting point to the path
                pathForFrequency.Add(new SParameterData { Frequency = measuredPoint.Frequency, Real = gamma.Real, Imaginary = gamma.Imaginary });

                if (MatchingCircuit.Any())
                {
                    double f = measuredPoint.Frequency * 1e9; // Frequency in Hz (assuming GHz input)
                    double omega = 2 * Math.PI * f;
                    var z = (1.0 + gamma) / (1.0 - gamma); // Convert to normalized impedance

                    foreach (var component in MatchingCircuit)
                    {
                        switch (component.Type)
                        {
                            case ComponentType.SeriesInductor:
                                z += new Complex(0, omega * (component.Value * 1e-9) / Z0); // L in nH
                                break;
                            case ComponentType.SeriesCapacitor:
                                z += new Complex(0, -1.0 / (omega * (component.Value * 1e-12) * Z0)); // C in pF
                                break;
                            case ComponentType.ShuntInductor:
                                var y_l = 1.0 / z;
                                y_l += new Complex(0, -Z0 / (omega * (component.Value * 1e-9))); // L in nH
                                z = 1.0 / y_l;
                                break;
                            case ComponentType.ShuntCapacitor:
                                var y_c = 1.0 / z;
                                y_c += new Complex(0, omega * (component.Value * 1e-12) * Z0); // C in pF
                                z = 1.0 / y_c;
                                break;
                        }
                    }
                    gamma = (z - 1.0) / (z + 1.0); // Convert back to gamma for the final point
                    // Add intermediate point to the path
                    pathForFrequency.Add(new SParameterData { Frequency = measuredPoint.Frequency, Real = gamma.Real, Imaginary = gamma.Imaginary });
                }
                newMatchedData.Add(pathForFrequency.Last());
                newMatchingPaths.Add(pathForFrequency);
            }

            MatchedData.Clear();
            foreach (var item in newMatchedData) MatchedData.Add(item);

            MatchingPaths.Clear();
            foreach (var path in newMatchingPaths) MatchingPaths.Add(path);

            if (newMatchedData.Any())
            {
                double maxGammaMag = newMatchedData.Max(p => Math.Sqrt(p.Real * p.Real + p.Imaginary * p.Imaginary));
                double vswr = maxGammaMag >= 0.9999 ? double.PositiveInfinity : (1 + maxGammaMag) / (1 - maxGammaMag);
                double rl = maxGammaMag <= 1e-9 ? double.PositiveInfinity : -20 * Math.Log10(maxGammaMag);

                string prefix = SelectedFrequency.HasValue ? $"@ {SelectedFrequency.Value} GHz | " : "Worst Case (Max Γ) | ";
                VswrText = $"{prefix}VSWR: {(double.IsInfinity(vswr) ? "∞" : vswr.ToString("F2"))}";
                ReturnLossText = $"Return Loss: {(double.IsInfinity(rl) ? "∞" : rl.ToString("F2"))} dB";
            }
            else
            {
                VswrText = "VSWR: N/A";
                ReturnLossText = "Return Loss: N/A";
            }
        }
    }
}