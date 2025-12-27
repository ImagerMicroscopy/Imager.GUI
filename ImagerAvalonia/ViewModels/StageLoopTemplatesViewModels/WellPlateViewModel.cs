using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;


namespace ImagerAvalonia.ViewModels
{
    public partial class WellPlateViewModel : StageLoopTemplateTypeViewModel
    {

        [ObservableProperty] ObservableCollection<string> _wellLetters = new() { "A", "B", "C", "D", "E", "F", "G", "H" };
        [ObservableProperty] ObservableCollection<string> _wellNumbers = new ObservableCollection<string>(Enumerable.Range(1, 12).Select(x => x.ToString()).ToList());

        [ObservableProperty] private string _startWellLetter = String.Empty ;
        [ObservableProperty] private string _startWellNumber = String.Empty ;

        public StageLoopViewModel StageLoopViewModel;



        public WellPlateViewModel(StageLoopViewModel vm) 
        {
            TemplateName = "96 Well plate";
            StageLoopViewModel = vm;   
        }
    }
}
