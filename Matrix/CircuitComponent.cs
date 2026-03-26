using ReactiveUI;

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

    // ReactiveObject를 상속받아 값이 변경될 때마다 UI에 알릴 수 있도록 합니다.
    public class CircuitComponent : ReactiveObject
    {
        public ComponentType Type { get; set; }
        
        private double _value;
        // L(nH), C(pF) 등의 물리적인 값
        public double Value 
        {
            get => _value;
            set => this.RaiseAndSetIfChanged(ref _value, value);
        }
    }
}