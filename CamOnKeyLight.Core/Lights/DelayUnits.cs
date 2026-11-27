namespace CamOnKeyLight.Core
{
    /// <summary>
    /// The window shows delays in seconds, in steps of a tenth. The settings file
    /// still stores milliseconds, so a value saved by an older version still loads.
    /// </summary>
    public static class DelayUnits
    {
        public const decimal StepSeconds = 0.1m;
        public const decimal MaxOnMilliseconds = 10000m;
        public const decimal MaxOffMilliseconds = 60000m;

        public static decimal MaxOnSeconds => MaxOnMilliseconds / 1000m;

        public static decimal MaxOffSeconds => MaxOffMilliseconds / 1000m;

        public static decimal MillisecondsToSeconds(decimal milliseconds)
        {
            if (milliseconds < 0)
                milliseconds = 0;

            return Math.Round(milliseconds / 1000m, 1, MidpointRounding.AwayFromZero);
        }

        public static decimal SecondsToMilliseconds(decimal seconds)
        {
            if (seconds < 0)
                seconds = 0;

            return Math.Round(seconds * 1000m, 0, MidpointRounding.AwayFromZero);
        }

        public static decimal ClampMilliseconds(decimal milliseconds, decimal maximum)
        {
            if (milliseconds < 0)
                return 0;

            if (milliseconds > maximum)
                return maximum;

            return milliseconds;
        }
    }
}
