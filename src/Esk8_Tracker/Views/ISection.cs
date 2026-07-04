namespace Esk8_Tracker.Views;

public enum AppTab { Ride, History, Garage, Stats, Settings }

/// <summary>Implemented by the five root section views to receive tab lifecycle.</summary>
public interface ISection
{
    /// <summary>Called when the section becomes visible (tab selected or app resumed onto it).</summary>
    void OnShown();
}
