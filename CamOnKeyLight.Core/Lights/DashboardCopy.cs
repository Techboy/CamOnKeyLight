namespace CamOnKeyLight.Core
{
    /// <summary>
    /// The words on the status dashboard. The light board still owns the
    /// detailed summary, including the relay note.
    /// </summary>
    public static class DashboardCopy
    {
        public static string Headline(LightSummaryKind kind, string summary)
        {
            string text = summary;
            const string suffix = " via relay";
            if (text.EndsWith(suffix, StringComparison.Ordinal))
                text = text[..^suffix.Length];

            return kind switch
            {
                LightSummaryKind.AllOn => "Lights are on",
                LightSummaryKind.AllOff => "Lights are off",
                _ => text
            };
        }

        public static string Reason(LightMode mode, bool cameraOn, bool quiet, bool anyLights)
        {
            if (!anyLights)
                return "Add a light and it will follow your camera.";

            if (mode == LightMode.On)
                return "because you asked the lights to stay on";

            if (mode == LightMode.Off)
                return "because you asked the lights to stay off";

            if (quiet && cameraOn)
                return "because quiet hours are on";

            return cameraOn ? "because your camera is on" : "because your camera is off";
        }

        public static string ModePill(LightMode mode)
        {
            return mode switch
            {
                LightMode.On => "On: lights stay on",
                LightMode.Off => "Off: lights stay off",
                _ => "Auto: lights follow the camera"
            };
        }
    }
}
