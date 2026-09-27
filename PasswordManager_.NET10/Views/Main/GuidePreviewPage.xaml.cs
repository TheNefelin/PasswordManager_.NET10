using PasswordManager_.NET10.ViewModels;

namespace PasswordManager_.NET10.Views.Main;

public partial class GuidePreviewPage : ContentPage
{
    public GuidePreviewPage(GuidePreviewViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // El guide se pide al abrir la pantalla, no al construirla, para que el
        // ActivityIndicator llegue a verse y el error se pueda mostrar en la pagina.
        viewModel.LoadGuideCommand.Execute(null);
    }
}
