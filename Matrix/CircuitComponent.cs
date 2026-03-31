using CommunityToolkit.Mvvm.ComponentModel;

namespace Matrix.Models
{
    public enum ComponentType
    {
        SeriesInductor,
        ShuntInductor,
        SeriesCapacitor,
        ShuntCapacitor,
        TransmissionLine
    }

    public partial class CircuitComponent : ObservableObject
    {
        public ComponentType Type { get; set; }

        // L(nH), C(pF) 등의 물리적인 값
        [ObservableProperty]
        private double _value;
    }
}