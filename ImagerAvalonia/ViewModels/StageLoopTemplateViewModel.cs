using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;


namespace ImagerAvalonia.ViewModels
{
    public partial class StageLoopTemplateViewModel : ViewModelBase
    {
        [ObservableProperty]
        public ObservableCollection<StageLoopTemplateTypeViewModel> _stageLoopTemplates = new ObservableCollection<StageLoopTemplateTypeViewModel>()
        {
        };

        [ObservableProperty]
        private ViewModelBase? _selectedTemplate;

        public StageLoopTemplateViewModel(StageLoopViewModel vm) 
        {
            _stageLoopTemplates.Add(new WellPlateViewModel(vm));
        }
    }
}
