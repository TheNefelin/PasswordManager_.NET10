using Microsoft.Maui.Controls;

namespace PasswordManager_.NET10.Behaviors.PasswordDetails;

/// <summary>
/// Behavior que anima la aparición/desaparición del menú lateral
/// </summary>
public class MenuAnimationBehavior : Behavior<Border>
{
    private Border? _border;

    protected override void OnAttachedTo(Border border)
    {
        _border = border;
        base.OnAttachedTo(border);

        // Suscribirse a cambios de propiedades
        border.PropertyChanged += OnBorderPropertyChanged;
    }

    protected override void OnDetachingFrom(Border border)
    {
        border.PropertyChanged -= OnBorderPropertyChanged;
        _border = null;
        base.OnDetachingFrom(border);
    }

    private async void OnBorderPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_border == null) return;

        // Detectar cambio en la propiedad IsVisible
        if (e.PropertyName == VisualElement.IsVisibleProperty.PropertyName)
        {
            if (_border.IsVisible)
            {
                // Animar apertura del menú
                await AnimateMenuOpen();
            }
            else
            {
                // Animar cierre del menú
                await AnimateMenuClose();
            }
        }
    }

    private async Task AnimateMenuOpen()
    {
        if (_border == null) return;

        _border.TranslationX = -280;
        await _border.TranslateToAsync(0, 0, 300, Easing.CubicOut);
    }

    private async Task AnimateMenuClose()
    {
        if (_border == null) return;

        await _border.TranslateToAsync(-280, 0, 300, Easing.CubicOut);
    }
}
