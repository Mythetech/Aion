using MudBlazor;

namespace Aion.Components.Shared.Dialogs.Commands;

public record ShowDialog(Type Dialog, string Title, DialogOptions? Options = default, DialogParameters? Parameters = default);