using CommunityToolkit.Mvvm.ComponentModel;

namespace ImagerAvalonia.ViewModels
{
    public partial class ImageGridViewModel :ViewModelBase
    {
        [ObservableProperty] ImageCanvasViewModel? _SelectedImage;

        public ImageGridViewModel() { } 
    }
}
