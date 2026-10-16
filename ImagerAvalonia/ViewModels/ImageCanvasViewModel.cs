using CommunityToolkit.Mvvm.ComponentModel;


namespace ImagerAvalonia.ViewModels
{
    /// <summary>
    /// One display slot of the image grid: the image acquired with one
    /// acquisition/detector pair. Owns the slot's user-facing state (header text,
    /// selection); the pixels themselves are rendered by the tile view
    /// (ImageCanvas), which keeps the bitmap and the last received frame.
    /// </summary>
    public partial class ImageCanvasViewModel : ViewModelBase
    {
        [ObservableProperty] private string _headerText = string.Empty;

        /// <summary>True while this slot is the grid's selected tile. Exactly one
        /// slot is selected at a time (see ImageDisplayViewModel.SelectedSlot).
        /// Purely logical state for now: the tile border highlight it used to drive
        /// was visually distracting and is suppressed pending a better indicator;
        /// selection still governs which tile the ROI tools act on.</summary>
        [ObservableProperty] private bool _isSelected = false;

        /// <summary>Whether this tile's contrast/histogram overlay is shown. The
        /// tile binds its overlay's visibility to this, and the display loop only
        /// feeds the (expensive) histogram while the overlay is visible.</summary>
        [ObservableProperty] private bool _isContrastPanelVisible = false;

        public ContrastAdjViewModel ContrastSettings = new();

        public string AcqName { get; }
        public string DetName { get; }

        public ImageCanvasViewModel(string acq, string det)
        {
            AcqName = acq;
            DetName = det;
            HeaderText = $"{acq}/{det}";
        }
    }
}
