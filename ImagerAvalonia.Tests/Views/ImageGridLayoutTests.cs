using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views;
using Xunit;

namespace ImagerAvalonia.Tests.Views;

/// <summary>
/// Layout behavior of the image grid and its tiles: the grid prioritizes the
/// horizontal direction (two slots side by side, not stacked), and a tile centers
/// its world panel in the tile body at the fitted scale, so the image is centered
/// whatever the tile's size.
/// </summary>
public sealed class ImageGridLayoutTests
{
    private static ImageCanvasViewModel Slot(string acq, string det) => new(acq, det);

    private static ImageGrid GridWithSlots(params ImageCanvasViewModel[] slots)
    {
        var grid = new ImageGrid();
        grid.Initialize(slots, new ImageRegionDisplayViewModel());
        return grid;
    }

    [AvaloniaFact]
    public void Two_slots_are_laid_out_side_by_side()
    {
        var grid = GridWithSlots(Slot("Acq1", "Cam1"), Slot("Acq1", "Cam2"));

        Assert.Single(grid.RowDefinitions);
        Assert.Equal(2, grid.ColumnDefinitions.Count);
        Assert.All(grid.Children, child => Assert.Equal(0, Grid.GetRow((Control)child)));
        Assert.Equal(0, Grid.GetColumn((Control)grid.Children[0]));
        Assert.Equal(1, Grid.GetColumn((Control)grid.Children[1]));
    }

    [AvaloniaFact]
    public void Four_slots_fill_a_two_by_two_grid()
    {
        var grid = GridWithSlots(
            Slot("Acq1", "Cam1"), Slot("Acq1", "Cam2"),
            Slot("Acq2", "Cam1"), Slot("Acq2", "Cam2"));

        Assert.Equal(2, grid.RowDefinitions.Count);
        Assert.Equal(2, grid.ColumnDefinitions.Count);
        // Row-major fill: the first two slots share the first row.
        Assert.Equal(0, Grid.GetRow((Control)grid.Children[0]));
        Assert.Equal(0, Grid.GetRow((Control)grid.Children[1]));
        Assert.Equal(1, Grid.GetRow((Control)grid.Children[2]));
        Assert.Equal(1, Grid.GetRow((Control)grid.Children[3]));
    }

    [AvaloniaFact]
    public void Five_slots_prefer_columns_over_rows()
    {
        var grid = GridWithSlots(
            Slot("Acq1", "Cam1"), Slot("Acq1", "Cam2"), Slot("Acq2", "Cam1"),
            Slot("Acq2", "Cam2"), Slot("Acq3", "Cam1"));

        // floor(sqrt(5)) = 2 rows, ceil(5/2) = 3 columns.
        Assert.Equal(2, grid.RowDefinitions.Count);
        Assert.Equal(3, grid.ColumnDefinitions.Count);
    }

    [AvaloniaFact]
    public void A_tiles_world_panel_is_centered_at_the_fitted_scale()
    {
        var tile = new ImageCanvas(Slot("Acq1", "Cam1"), new ImageRegionDisplayViewModel());
        var window = new Window { Content = tile, Width = 1200, Height = 400 };
        window.Show();

        var body = tile.FindControl<Grid>("TileBody")!;
        var worldHost = tile.FindControl<Panel>("WorldHost")!;
        var transform = (TransformGroup)worldHost.RenderTransform!;
        var scale = (ScaleTransform)transform.Children[0];
        var translate = (TranslateTransform)transform.Children[1];

        // The world panel must be fitted to the (wide) tile body and centered in
        // it, both horizontally and vertically.
        double expectedScale = Math.Min(body.Bounds.Width, body.Bounds.Height) / 800;
        Assert.True(body.Bounds.Width > body.Bounds.Height, "the tile body is wide");
        Assert.Equal(expectedScale, scale.ScaleX, 3);
        Assert.Equal((body.Bounds.Width - 800 * expectedScale) / 2, translate.X, 1);
        Assert.Equal((body.Bounds.Height - 800 * expectedScale) / 2, translate.Y, 1);

        window.Close();
    }

    [AvaloniaFact]
    public void A_tiles_contrast_overlay_follows_the_header_toggle()
    {
        var slot = Slot("Acq1", "Cam1");
        var tile = new ImageCanvas(slot, new ImageRegionDisplayViewModel());
        var window = new Window { Content = tile, Width = 600, Height = 600 };
        window.Show();

        var overlay = tile.FindControl<Border>("ContrastOverlay")!;
        var toggle = tile.FindControl<ToggleButton>("ContrastToggle")!;

        // Hidden by default.
        Assert.False(slot.IsContrastPanelVisible);
        Assert.False(overlay.IsVisible);

        // Toggling the triangle opens the slot's overlay (two-way to the slot,
        // one-way back to the overlay).
        toggle.IsChecked = true;

        Assert.True(slot.IsContrastPanelVisible);
        Assert.True(overlay.IsVisible);

        // And closing it from the view model hides the overlay again.
        slot.IsContrastPanelVisible = false;

        Assert.False(overlay.IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void A_square_world_centered_in_a_square_tile_sits_at_the_origin()
    {
        var tile = new ImageCanvas(Slot("Acq1", "Cam1"), new ImageRegionDisplayViewModel());
        var window = new Window { Content = tile, Width = 800, Height = 800 };
        window.Show();

        var body = tile.FindControl<Grid>("TileBody")!;
        var worldHost = tile.FindControl<Panel>("WorldHost")!;
        var transform = (TransformGroup)worldHost.RenderTransform!;
        var scale = (ScaleTransform)transform.Children[0];
        var translate = (TranslateTransform)transform.Children[1];

        // A square tile body fits the square world panel exactly at scale 1,
        // centered at the origin.
        Assert.Equal(body.Bounds.Height / 800, scale.ScaleX, 3);
        Assert.Equal((body.Bounds.Width - 800 * scale.ScaleX) / 2, translate.X, 1);
        Assert.Equal(0, translate.Y, 1);

        window.Close();
    }
}


/// <summary>
/// The tile viewport's zoom and pan constraints: zooming in and back out to the
/// fitted scale always restores the fitted, centered state exactly (however the
/// image was zoomed or panned in between), and while zoomed in the image can be
/// panned but never pushed off the tile; at fit scale it cannot be shifted at
/// all.
/// </summary>
public sealed class ImageTileViewportTests
{
    private static (ScaleTransform scale, TranslateTransform translate) Transforms(ImageCanvas tile)
    {
        var worldHost = tile.FindControl<Panel>("WorldHost")!;
        var t = (TransformGroup)worldHost.RenderTransform!;
        return ((ScaleTransform)t.Children[0], (TranslateTransform)t.Children[1]);
    }

    [AvaloniaFact]
    public void Zooming_in_and_back_out_restores_the_fitted_state_exactly()
    {
        var tile = new ImageCanvas(new ImageCanvasViewModel("Acq1", "Cam1"), new ImageRegionDisplayViewModel());
        var window = new Window { Content = tile, Width = 640, Height = 640 };
        window.Show();

        var (scale, translate) = Transforms(tile);
        double fitScale = scale.ScaleX;
        double fitX = translate.X;
        double fitY = translate.Y;

        // Zoom in hard at an off-center point, wander around, then zoom all the
        // way back out.
        tile.ZoomAt(new Point(100, 500), 4);
        tile.PanBy(new Vector(500, -300));
        tile.ZoomAt(new Point(600, 100), 1.25);
        for (int i = 0; i < 20; i++)
        {
            tile.ZoomAt(new Point(320, 300), 1.0 / 1.1);
        }

        Assert.Equal(fitScale, scale.ScaleX, 9);
        Assert.Equal(fitX, translate.X, 6);
        Assert.Equal(fitY, translate.Y, 6);

        window.Close();
    }

    [AvaloniaFact]
    public void The_image_cannot_be_shifted_at_the_fitted_scale()
    {
        var tile = new ImageCanvas(new ImageCanvasViewModel("Acq1", "Cam1"), new ImageRegionDisplayViewModel());
        var window = new Window { Content = tile, Width = 640, Height = 640 };
        window.Show();

        var (scale, translate) = Transforms(tile);
        double fitX = translate.X;
        double fitY = translate.Y;

        // Panning while not zoomed in has nothing to show for it: the clamps
        // keep the fitted, centered position.
        tile.PanBy(new Vector(200, 200));

        Assert.Equal(fitX, translate.X, 6);
        Assert.Equal(fitY, translate.Y, 6);

        window.Close();
    }

    [AvaloniaFact]
    public void Panning_while_zoomed_in_keeps_the_image_covering_the_tile()
    {
        var tile = new ImageCanvas(new ImageCanvasViewModel("Acq1", "Cam1"), new ImageRegionDisplayViewModel());
        var window = new Window { Content = tile, Width = 640, Height = 640 };
        window.Show();

        var body = tile.FindControl<Grid>("TileBody")!;
        var (scale, translate) = Transforms(tile);

        tile.ZoomAt(new Point(320, 300), 8);

        // Pan far beyond the image: the clamps hold the world rect over the body.
        // Panning towards the top-left parks the world's bottom-right corner at
        // the body's; panning towards the bottom-right parks its top-left corner
        // at the body's.
        tile.PanBy(new Vector(-100000, -100000));
        Assert.Equal(body.Bounds.Width - 800 * scale.ScaleX, translate.X, 3);
        Assert.Equal(body.Bounds.Height - 800 * scale.ScaleY, translate.Y, 3);

        tile.PanBy(new Vector(100000, 100000));
        Assert.Equal(0, translate.X, 3);
        Assert.Equal(0, translate.Y, 3);

        window.Close();
    }
}
