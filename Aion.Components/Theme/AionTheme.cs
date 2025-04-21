using MudBlazor;

namespace Aion.Components.Theme;

public class AionTheme : MudTheme
{
    public AionTheme()
    {
        PaletteLight = new PaletteLight()
        {
            Primary = Colors.Indigo.Lighten2,
            Secondary = Colors.Purple.Lighten4,
            Tertiary = Colors.DeepOrange.Default,
            Background = "#EEE",
            BackgroundGray = "#FFF",
            Surface = "#EEE",
            ActionDefault = Colors.Indigo.Lighten2,
            TableHover = Colors.Indigo.Lighten2 + "33",
        };
        PaletteDark = new PaletteDark()
        {
            Primary = Colors.Indigo.Default,
            Secondary = Colors.DeepPurple.Darken4,
            Tertiary = Colors.DeepOrange.Default,
            Surface = "#111",
            DrawerBackground = "#111",
            Background = "#111",
            GrayDarker = "#222",
            ActionDefault = Colors.Indigo.Default,
            BackgroundGray = "#222",
            TableHover = Colors.Indigo.Default + "33",
        };
        Typography = new Typography()
        {
            Default = new DefaultTypography()
            {
                FontFamily = new[] { "Tahoma", "Geneva", "Verdana", },
                TextTransform = "none",
                FontSize = "0.75",
            },

            Button = new ButtonTypography()
            {
                FontFamily = new[] { "Tahoma", "Geneva", "Verdana", },
                TextTransform = "none",
                FontSize = "1em",
            }
        };
        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "0.5em",
            AppbarHeight = "2rem",
            DrawerWidthLeft = "25vw",
            DrawerMiniWidthLeft = "3.25em"
        };
    }
}