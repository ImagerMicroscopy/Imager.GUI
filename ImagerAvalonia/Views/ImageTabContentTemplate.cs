using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ImagerAvalonia.ViewModels;
using System.Runtime.CompilerServices;

namespace ImagerAvalonia.Views;

/// <summary>
/// Builds the content view for a tab. Avalonia rebuilds the selected tab's content on
/// every tab switch, which would recreate the display views - they subscribe to view
/// model events and start a background update task, so recreating them leaks and resets
/// zoom and contrast. This template therefore caches one view per content view model;
/// the cache key is weak, so a closed tab's view is collected together with its view
/// model. Define one instance as a resource and share it (tab strip and pop-out
/// windows) so a popped-out tab keeps its view.
/// </summary>
public sealed class ImageTabContentTemplate : IDataTemplate
{
    private readonly ConditionalWeakTable<object, Control> _views = new();

    public bool Match(object? data) => data is ImageTabViewModel;

    public Control? Build(object? param)
    {
        if (param is not ImageTabViewModel tab || tab.Content is not { } content)
            return null;
        return _views.GetValue(content, BuildView);
    }

    /// <summary>Builds and caches the tab's view without presenting it, so the view is
    /// subscribed to its view model from the start.</summary>
    public void EnsureBuilt(ImageTabViewModel tab) => Build(tab);

    internal bool TryGetBuiltView(object content, out Control? view) =>
        _views.TryGetValue(content, out view);

    private static Control BuildView(object content) => content switch
    {
        ImageDisplayViewModel => new ImageDisplayView { DataContext = content },
        FieldViewerViewModel => new FieldViewerView { DataContext = content },
        SmartProcessingRegisterViewModel => new SmartProgramView { DataContext = content },
        _ => new TextBlock { Text = content.ToString() },
    };
}
