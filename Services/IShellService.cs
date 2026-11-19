namespace MyClock.Services;

// Windows shell interactions, kept out of view models.
public interface IShellService
{
    // Shows a Yes/No warning dialog; returns true for Yes.
    bool Confirm(string title, string message);

    // Opens File Explorer with the file selected, or its folder if the file does
    // not exist yet. Throws Win32Exception, IOException, or UnauthorizedAccessException on failure.
    void RevealInExplorer(string filePath);
}
