using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;


namespace ImagerAvalonia.ViewModels
{


    public partial class ImageElementCollectionsViewModel :ViewModelBase
    {
        [ObservableProperty] ObservableCollection<ElementPlotViewModel> _plotElements = new();
        [ObservableProperty] private int _NumberOfCollections = 1;
        public ImageElementCollectionsViewModel() 
        { 
            
        }

        public void RemovePlotElementCommand(object plotElement)
        {
            if(plotElement is ElementPlotViewModel plot)
            {
                PlotElements.Remove(plot);
            }
        }
    }
}
