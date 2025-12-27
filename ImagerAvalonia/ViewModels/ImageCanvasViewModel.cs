using CommunityToolkit.Mvvm.ComponentModel;


namespace ImagerAvalonia.ViewModels
{
    public partial class ImageCanvasViewModel : ViewModelBase
    {
        [ObservableProperty] private string _headerText = string.Empty;

        public ContrastAdjViewModel ContrastSettings = new();

        public string AcqName;
        public string DetName;
        

        public ImageCanvasViewModel(string acq, string det) 
        {
            AcqName = acq;
            DetName = det;
            HeaderText = $"{acq}/{det}";

        }



        //public void SetHeader(string acq, string det)
        //{
        //    HeaderText = $"{acq}/{det}";
        //    AcqName = acq;
        //    DetName = det;
        //}

    }
}
