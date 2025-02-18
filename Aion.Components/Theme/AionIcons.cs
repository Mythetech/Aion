namespace Aion.Components.Theme;

public static class AionIcons
{
    public static string Rounded => "material-symbols-rounded/";
        
    public static string Round(string icon) => Rounded + icon;
    
    public static string LightMode => Round("light_mode");

    public static string DarkMode => Round("dark_mode");

    public static string Close => Round("close");
    
    public static string Back => Round("arrow_back");
    
    public static string CollapseLeft => Round("chevron_left");

    public static string ExpandRight => Round("chevron_right");

    public static string Filter => Round("filter_list");

    public static string Add => Round("add");

    public static string Refresh => Round("refresh");

    public static string Settings => Round("settings");
    
    public static string Delete => Round("delete");

    public static string Copy => Round("content_copy");

    public static string Connection => Round("database");
    
    public static string Info => Round("info");
}