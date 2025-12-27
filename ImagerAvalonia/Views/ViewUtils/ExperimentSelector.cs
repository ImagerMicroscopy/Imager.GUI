using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.Views.ViewUtils
{
    public partial class ExperimentSelector : UserControl
    {
        protected ExperimentalPanelViewControl expPanel;
        protected Window window;
        public object? DataContextSender;

        public ExperimentSelector()
        {

        }

        protected async void OnSelectButtonClick(object? sender, RoutedEventArgs e)
        {
            MainViewModel mainVM = App.Container.Resolve<MainViewModel>();
            if (sender is Control control)
                DataContextSender = control.DataContext;


            window = new Window();
            if (mainVM.SelectedExperiment != null)
            {
                expPanel = new ExperimentalPanelViewControl(mainVM.SelectedExperiment);

                window = new Window
                {
                    Content = expPanel,
                    Title = "Double tap on acquisition to select",
                    Width = 290,
                    Height = 400,
                    CanResize = false
                };

                window.Closed += Window_Closed;

                var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

                expPanel.DetectionDoubleTapped += ExpPanel_DetectionDoubleTapped;

                await window.ShowDialog(mainWindow);
            }
        }

        protected void Window_Closed(object? sender, System.EventArgs e)
        {
            expPanel.DetectionDoubleTapped -= ExpPanel_DetectionDoubleTapped;
        }


        protected virtual void ExpPanel_DetectionDoubleTapped(object? sender, Services.NodeBase e) { }


    }

}
