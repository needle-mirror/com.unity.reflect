using UnityEngine;

[CreateAssetMenu(fileName = "DisplayModeStatusParameters", menuName = "ScriptableObjects/DisplayModeStatusParameters")]
public class DisplayModeStatusParameters : ScriptableObject
{
    public enum Status
    {
        Default, 
        Fail, 
        Partial, 
        Success
    }

    public string statusFormat = "Status: <color=#{0}>{1}</color>";

    public Color defaultColor;
    public Color failColor;
    public Color partialColor;
    public Color successColor;

    protected Color GetStatusColor(Status status)
    {
        switch (status)
        {
            case Status.Fail: return failColor;
            case Status.Partial: return partialColor;
            case Status.Success: return successColor;
            default: return defaultColor;
        }
    }

    public string GetFormattedStatus(string message, Status status = Status.Default)
    {
        return string.Format(statusFormat, ColorUtility.ToHtmlStringRGB(GetStatusColor(status)), message);
    }
}
