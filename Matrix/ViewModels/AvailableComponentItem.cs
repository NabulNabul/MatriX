using Matrix.Models;

namespace Matrix.ViewModels
{
    public class AvailableComponentItem
    {
        public string Name { get; set; } = string.Empty;
        public ComponentType Type { get; set; }
        public string IconPath { get; set; } = string.Empty;
    }
}
