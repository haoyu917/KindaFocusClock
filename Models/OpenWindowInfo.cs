namespace MyClock.Models;







public sealed class OpenWindowInfo
{
    public OpenWindowInfo(nint handle, string title)
    {
        
        
        Handle = handle;
        Title = title;
    }

    
    public nint Handle { get; }

    
    public string Title { get; }
}
