using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Matrix.Models;

namespace Matrix.ViewModels
{
    public partial class NetworkAnalyzerViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<SParameterData> _measuredData;

        [ObservableProperty]
        private ObservableCollection<SParameterData> _matchedData;

        [ObservableProperty]
        private double? _selectedFrequency;

        [ObservableProperty]
        private bool _showVswrCircle = true;

        [ObservableProperty]
        private double _targetVswr = 2.0;

        [ObservableProperty]
        private string _vswrText = "VSWR: N/A";

        [ObservableProperty]
        private string _returnLossText = "Return Loss: N/A";

        public ObservableCollection<CircuitComponent> MatchingCircuit { get; }
        public ObservableCollection<List<SParameterData>> MatchingPaths { get; private set; }
        public ObservableCollection<double> AvailableFrequencies { get; }
        public ObservableCollection<AvailableComponentItem> AvailableComponentItems { get; }

        private const double Z0 = 50.0;
        private readonly Dictionary<CircuitComponent, PropertyChangedEventHandler> _componentHandlers = new();

        public NetworkAnalyzerViewModel()
        {
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
                AvailableFrequencies.Add(data.Frequency);

            MatchingCircuit.CollectionChanged += OnMatchingCircuitChanged;

            RecalculateMatchedData();
        }

        partial void OnMeasuredDataChanged(ObservableCollection<SParameterData> value)
        {
            RecalculateMatchedData();
        }

        partial void OnSelectedFrequencyChanged(double? value)
        {
            RecalculateMatchedData();
        }

        [RelayCommand]
        private void AddComponent(ComponentType type)
        {
            double defaultValue = (type == ComponentType.SeriesInductor || type == ComponentType.ShuntInductor) ? 10 : 1;
            MatchingCircuit.Add(new CircuitComponent { Type = type, Value = defaultValue });
        }

        [RelayCommand]
        private void RemoveComponent(CircuitComponent component)
        {
            MatchingCircuit.Remove(component);
        }

        [RelayCommand]
        private void ClearFrequencySelection()
        {
            SelectedFrequency = null;
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
                        MatchingCircuit.Add(comp);
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
            double freqMultiplier = 1e9;
            string format = "MA";

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
                    if (_componentHandlers.TryGetValue(item, out var handler))
                    {
                        item.PropertyChanged -= handler;
                        _componentHandlers.Remove(item);
                    }
                }
            }
            if (e.NewItems != null)
                SubscribeToComponentChanges();

            RecalculateMatchedData();
        }

        private void SubscribeToComponentChanges()
        {
            foreach (var component in MatchingCircuit)
            {
                if (!_componentHandlers.ContainsKey(component))
                {
                    PropertyChangedEventHandler handler = (s, e) =>
                    {
                        if (e.PropertyName == nameof(CircuitComponent.Value))
                            RecalculateMatchedData();
                    };
                    component.PropertyChanged += handler;
                    _componentHandlers[component] = handler;
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
                pointsToProcess = MeasuredData.Where(p => Math.Abs(p.Frequency - SelectedFrequency.Value) < 1e-9);

            var newMatchedData = new List<SParameterData>();
            var newMatchingPaths = new List<List<SParameterData>>();

            foreach (var measuredPoint in pointsToProcess)
            {
                var pathForFrequency = new List<SParameterData>();
                var gamma = new Complex(measuredPoint.Real, measuredPoint.Imaginary);

                pathForFrequency.Add(new SParameterData { Frequency = measuredPoint.Frequency, Real = gamma.Real, Imaginary = gamma.Imaginary });

                if (MatchingCircuit.Any())
                {
                    double f = measuredPoint.Frequency * 1e9;
                    double omega = 2 * Math.PI * f;
                    var z = (1.0 + gamma) / (1.0 - gamma);

                    foreach (var component in MatchingCircuit)
                    {
                        switch (component.Type)
                        {
                            case ComponentType.SeriesInductor:
                                z += new Complex(0, omega * (component.Value * 1e-9) / Z0);
                                break;
                            case ComponentType.SeriesCapacitor:
                                z += new Complex(0, -1.0 / (omega * (component.Value * 1e-12) * Z0));
                                break;
                            case ComponentType.ShuntInductor:
                                var y_l = 1.0 / z;
                                y_l += new Complex(0, -Z0 / (omega * (component.Value * 1e-9)));
                                z = 1.0 / y_l;
                                break;
                            case ComponentType.ShuntCapacitor:
                                var y_c = 1.0 / z;
                                y_c += new Complex(0, omega * (component.Value * 1e-12) * Z0);
                                z = 1.0 / y_c;
                                break;
                        }
                    }
                    gamma = (z - 1.0) / (z + 1.0);
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
