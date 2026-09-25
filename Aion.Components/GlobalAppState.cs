namespace Aion.Components;

public class GlobalAppState
{
    private bool _sidebarOpen = false;

    public bool SideBarOpen
    {
        get => _sidebarOpen;
        set
        {
            _sidebarOpen = value;
            NotifySideBarChanged();
            NotifyAppStateChanged();
        }
    }

    public event Action? SideBarChanged;

    public event Action<GlobalAppState>? AppStateChanged;

    private void NotifySideBarChanged() => SideBarChanged?.Invoke();

    private void NotifyAppStateChanged() => AppStateChanged?.Invoke(this);

    public void ToggleSideBar()
    {
        SideBarOpen = !SideBarOpen;

        NotifyAppStateChanged();
    }
}
