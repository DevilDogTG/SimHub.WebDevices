using System.Windows;
using System.Windows.Media;

namespace DevDogs.WebDevices.UI;

/// <summary>
/// The left-menu icon: a tablet outline, drawn as vectors so no image file has to ship with the plugin.
/// </summary>
internal static class PluginIcon
{
    /// <summary>
    /// Creates the icon.
    /// </summary>
    /// <returns>A frozen image.</returns>
    public static ImageSource Create()
    {
        var pen = new Pen(Brushes.White, 1.6);
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(null, pen, new RectangleGeometry(new Rect(3, 5, 18, 14), 2, 2)));
        drawing.Children.Add(new GeometryDrawing(Brushes.White, null, new EllipseGeometry(new Point(18.5, 12), 0.9, 0.9)));
        drawing.Children.Add(new GeometryDrawing(null, new Pen(Brushes.White, 1.2), new RectangleGeometry(new Rect(6, 8, 9.5, 8))));
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }
}
