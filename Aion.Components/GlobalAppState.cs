using MudBlazor;

namespace Aion.Components;

public class GlobalAppState
{
    private MudThemeProvider? _themeProvider = default;
        
        public void SetThemeProvider(MudThemeProvider provider)
            => _themeProvider = provider;
        
        private bool _sidebarOpen = false;

        public bool SideBarOpen
        {
            get => _sidebarOpen;
            set
            {
                _sidebarOpen = value;
                AppBarChanged();
                AppStateChanged();
            }
        }

        private bool _isDarkMode = false;

        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                _isDarkMode = value;
                IsDarkModeChanged();
                AppStateChanged();
            }
        }
    
        public event Action? OnChange;
        
        public event Action<GlobalAppState>? OnAppStateChanged;

        public event Action<bool>? OnDarkModeChanged;
        
        private void AppBarChanged() => OnChange?.Invoke();

        private void IsDarkModeChanged() => OnDarkModeChanged?.Invoke(IsDarkMode);
        
        private void AppStateChanged() => OnAppStateChanged?.Invoke(this);

        public void ToggleSideBar()
        {
            SideBarOpen = !SideBarOpen;

            AppStateChanged();
        }

        public async Task SetSystemColorMode()
        {
            if (_themeProvider == null)
                return;

            IsDarkMode = await _themeProvider.GetSystemPreference();
        }
    }
