using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace ImagerAvalonia.ViewModels;

public abstract class ViewModelBase : ObservableObject, IDisposable
{
    public virtual void Dispose()
    {

    }
}
