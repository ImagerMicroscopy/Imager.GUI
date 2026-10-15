using Avalonia;
using Avalonia.Controls;

namespace ImagerAvalonia.Views;

/// <summary>
/// Shared element picker shown by the "+" action on the measurement program
/// tree view cards. The footer line states where the new element will land.
/// </summary>
public partial class ElementPickerView : UserControl
{
    public static readonly StyledProperty<string> FooterTextProperty =
        AvaloniaProperty.Register<ElementPickerView, string>(nameof(FooterText), string.Empty);

    public string FooterText
    {
        get => GetValue(FooterTextProperty);
        set => SetValue(FooterTextProperty, value);
    }

    public ElementPickerView()
    {
        InitializeComponent();
    }
}
