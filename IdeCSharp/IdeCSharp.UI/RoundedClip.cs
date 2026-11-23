using System.Windows;
using System.Windows.Media;

namespace IdeCSharp.UI;

/// <summary>
/// Arrondit les coins d'un panneau en découpant tout son contenu.
/// Usage en XAML : local:RoundedClip.Radius="8"
/// </summary>
public static class RoundedClip
{
    public static readonly DependencyProperty RadiusProperty =
        DependencyProperty.RegisterAttached("Radius", typeof(double), typeof(RoundedClip),
            new PropertyMetadata(0.0, OnRadiusChanged));

    public static double GetRadius(DependencyObject d) => (double)d.GetValue(RadiusProperty);
    public static void SetRadius(DependencyObject d, double value) => d.SetValue(RadiusProperty, value);

    private static void OnRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;

        // On se désabonne d'abord pour ne jamais s'abonner deux fois
        element.SizeChanged -= OnSizeChanged;

        if ((double)e.NewValue > 0)
        {
            element.SizeChanged += OnSizeChanged;
            UpdateClip(element);
        }
        else
        {
            element.Clip = null;
        }
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateClip((FrameworkElement)sender);

    // Le découpage suit la taille réelle du panneau
    private static void UpdateClip(FrameworkElement element)
    {
        double radius = GetRadius(element);
        element.Clip = element.ActualWidth <= 0 || element.ActualHeight <= 0
            ? null
            : new RectangleGeometry(new Rect(0, 0, element.ActualWidth, element.ActualHeight), radius, radius);
    }
}