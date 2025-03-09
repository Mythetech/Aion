using MudBlazor;

namespace Aion.Components.Theme;

public static class AionIcons
{
    public static string Rounded => "material-symbols-rounded/";
        
    public static string Round(string icon) => Rounded + icon;

    public static string Account => Icons.Material.TwoTone.AccountCircle;
    
    public static string LightMode => Round("light_mode");

    public static string DarkMode => Round("dark_mode");

    public static string Search => Round("search");

    public static string Close => Round("close");
    
    public static string Success => Round("check_circle");
    
    public static string Back => Round("arrow_back");
    
    public static string CollapseLeft => Round("chevron_left");

    public static string ExpandRight => Round("chevron_right");

    public static string Filter => Round("filter_list");

    public static string Add => Round("add");

    public static string Refresh => Round("refresh");

    public static string Settings => Round("settings");
    
    public static string Delete => Round("delete");

    public static string Copy => Round("content_copy");
    
    public static string Save => Round("save");
    
    public static string SaveAs => Round("save_as");
    
    public static string SaveFile => Round("save_file");
    
    public static string OpenFile => Round("file_open");
    
    public static string OpenInNew => Round("open_in_new");

    public static string Edit => Round("edit");

    public static string Connection => Round("database");
    public static string Query => Round("database_search");

    public static string Info => Round("info");

    public static string Run => Round("play_arrow");
    
    public static string Stop => Round("stop");
    
    public static string History => Round("history");

    public static string Aion =>
        "<svg fill=\"currentColor\" height=\"auto\" width=\"auto\" version=\"1.1\" id=\"Layer_1\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 512 512\" xml:space=\"preserve\">\n\n<g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"/>\n\n<g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>\n\n<g id=\"SVGRepo_iconCarrier\"> <g> <g> <path d=\"M256,0C114.616,0,0,114.614,0,256s114.616,256,256,256c141.386,0,256-114.614,256-256S397.386,0,256,0z M466.431,243.235 L351.08,216.615l62.743-100.388C445.27,151.641,463.625,195.957,466.431,243.235z M395.77,98.177l-100.387,62.741L268.765,45.57 C316.043,48.375,360.356,66.73,395.77,98.177z M243.235,45.568l-26.619,115.349l-100.387-62.74 C151.644,66.73,195.957,48.375,243.235,45.568z M98.176,116.23l62.743,100.387L45.57,243.235 C48.375,195.957,66.73,151.642,98.176,116.23z M45.57,268.765l115.349,26.62L98.176,395.772 C66.73,360.356,48.375,316.041,45.57,268.765z M116.229,413.823l100.388-62.743l26.62,115.352 C195.957,463.627,151.642,445.27,116.229,413.823z M206.452,256c0-33.217,27.022-60.235,60.235-60.235 c14.798,0,28.362,5.365,38.861,14.249c-22.054,3.703-38.861,22.879-38.861,45.987c0,23.106,16.807,42.284,38.861,45.984 c-10.497,8.886-24.063,14.252-38.861,14.252C233.472,316.235,206.452,289.214,206.452,256z M268.765,466.432l26.618-115.351 l100.388,62.743C360.358,445.27,316.043,463.625,268.765,466.432z M413.823,395.77L351.08,295.383l115.349-26.62 C463.625,316.043,445.27,360.358,413.823,395.77z\"/> </g> </g> </g>\n\n</svg>";

    public static string Table => Round("table_view");

    public static string EditTable => Round("table_edit");
    public static string Column => Round("view_column"); 
    public static string Index => Round("sort");

    public static string Key => Round("key");

    public static string Json => Round("file_json");

    public static string Csv => Round("csv");

    public static string Spreadsheet => Round("data_table");
    
    public static string TreeJson => Round("park");
    
    public static string PrettyJson => Round("data_object");

    public static string RawJson => Round("raw_on");
    
    public static string ExpandContent => Round("expand_content");

    public static string Transaction => Round("lock");

    public static string TransactionOff => Round("lock_open");

    public static string EstimatedQueryPlan => Round("query_stats");
    
    public static string ActualQueryPlan => Round("monitoring");

    public static string Format => Round("format_indent_increase");

    public static string Accept => Round("check");
}