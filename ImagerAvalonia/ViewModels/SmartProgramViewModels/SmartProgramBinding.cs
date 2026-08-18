using CommunityToolkit.Mvvm.ComponentModel;
using System;


namespace ImagerAvalonia.Services
{
    public partial class SmartProgramBinding<T> : SmartProgramInput
    {
        [ObservableProperty] T _smartProgramInputVM;
        private static readonly Random _random = new Random();

        public SmartProgramBinding(T smartProgramInputVM)
        {
            byte r = (byte)_random.Next(256);
            byte g = (byte)_random.Next(256);
            byte b = (byte)_random.Next(256);

            SmartProgramColor = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(r, g, b));
            SmartProgramInputVM = smartProgramInputVM;
        }
    }

    public partial class SmartProgramInput : ObservableObject
    {
        [ObservableProperty] Guid _smartProgramID;
        [ObservableProperty] Avalonia.Media.SolidColorBrush _smartProgramColor;
        private static readonly Random _random = new Random();

    }
}
