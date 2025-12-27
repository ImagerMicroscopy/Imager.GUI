using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels
{
    public partial class ImageInputViewModel : DagNodeViewModel
    {
        [ObservableProperty] string _imagePath;

        public ImageInputViewModel(NodeInfo nodeinfo) : base(nodeinfo)
        {

        }
    }
}
