using CommunityToolkit.Mvvm.ComponentModel;


namespace ImagerAvalonia.ViewModels
{
    public partial class StageLoopTemplateTypeViewModel : ViewModelBase
    {
        [ObservableProperty] string _templateName = "96 Well plate";

        public StageLoopTemplateTypeViewModel() { }
    }
}
