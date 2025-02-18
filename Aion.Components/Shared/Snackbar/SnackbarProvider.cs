using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Aion.Components.Shared.Snackbar;

public class SnackbarProvider : ISnackbarProvider
{
    private readonly IServiceProvider _serviceProvider;
    
    private ISnackbar? _snackbar;

    public SnackbarProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ISnackbar GetSnackbar()
    {
        if (_snackbar == null)
        {
            using var scope = _serviceProvider.CreateScope();
            
            return scope.ServiceProvider.GetRequiredService<ISnackbar>();   
        }
        return _snackbar ?? _serviceProvider.GetRequiredService<ISnackbar>();   
    }
}