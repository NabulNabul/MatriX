namespace Matrix.Models
{
    public class SParameterData
    {
        public double Frequency { get; set; }
        public double Real { get; set; }
        public double Imaginary { get; set; }
        
        // 필요에 따라 Magnitude, Phase, VSWR 등을 계산하는 속성을 추가할 수 있습니다.
    }
}