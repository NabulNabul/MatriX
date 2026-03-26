using System.Collections.ObjectModel;
using Matrix.Models;
using ReactiveUI;

namespace Matrix.ViewModels
{
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

        public NetworkAnalyzerViewModel()
        {
            _measuredData = new ObservableCollection<SParameterData>();
            _matchedData = new ObservableCollection<SParameterData>();
            MatchingCircuit = new ObservableCollection<CircuitComponent>();
            
            // TODO: MatchingCircuit 내의 소자가 추가/삭제되거나 소자 값(Value)이 변경될 때마다
            // Cascade 방식의 S-Parameter 계산을 수행하여 _matchedData를 실시간 업데이트하는 로직이 필요합니다.
        }
    }
}