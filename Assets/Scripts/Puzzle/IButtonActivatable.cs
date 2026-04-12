/// <summary>
/// Interface for any object that can be activated/deactivated by a
/// <see cref="ButtonController"/> (pressure plate).
/// Implement on MonoBehaviours such as SwitchDoor, MovingPlatform, etc.
/// </summary>
public interface IButtonActivatable
{
    /// <summary>Called when the button is pressed.</summary>
    void Activate();

    /// <summary>Called when the button is released.</summary>
    void Deactivate();
}
