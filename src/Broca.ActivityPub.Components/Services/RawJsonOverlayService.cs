namespace Broca.ActivityPub.Components.Services;

public class RawJsonOverlayService
{
    public string? Json { get; private set; }
    public bool IsVisible => Json != null;

    public event Action? OnChanged;

    public void Show(string json)
    {
        Json = json;
        OnChanged?.Invoke();
    }

    public void Hide()
    {
        Json = null;
        OnChanged?.Invoke();
    }
}
