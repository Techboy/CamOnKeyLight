using CamOnKeyLight.Core;

namespace CamOnKeyLight.Core.Tests
{
    public class DelayUnitsTests
    {
        [Theory]
        [InlineData(0, "0.0")]
        [InlineData(100, "0.1")]
        [InlineData(1500, "1.5")]
        [InlineData(10000, "10.0")]
        [InlineData(60000, "60.0")]
        [InlineData(50, "0.1")]
        [InlineData(250, "0.3")]
        [InlineData(-20, "0.0")]
        public void MillisecondsDisplayAsTenthsOfASecond(int milliseconds, string seconds)
        {
            Assert.Equal(decimal.Parse(seconds, System.Globalization.CultureInfo.InvariantCulture), DelayUnits.MillisecondsToSeconds(milliseconds));
        }

        [Theory]
        [InlineData("0.0", 0)]
        [InlineData("0.1", 100)]
        [InlineData("1.5", 1500)]
        [InlineData("10", 10000)]
        [InlineData("60", 60000)]
        [InlineData("0.3", 300)]
        public void SecondsStoreAsWholeMilliseconds(string seconds, int milliseconds)
        {
            Assert.Equal(milliseconds, DelayUnits.SecondsToMilliseconds(decimal.Parse(seconds, System.Globalization.CultureInfo.InvariantCulture)));
        }

        [Fact]
        public void SavedDefaultsRoundTripThroughSeconds()
        {
            Assert.Equal(0m, DelayUnits.SecondsToMilliseconds(DelayUnits.MillisecondsToSeconds(0)));
            Assert.Equal(1500m, DelayUnits.SecondsToMilliseconds(DelayUnits.MillisecondsToSeconds(LightTiming.DefaultOffDelayMilliseconds)));
            Assert.Equal(1.5m, DelayUnits.MillisecondsToSeconds(LightTiming.DefaultOffDelayMilliseconds));
        }

        [Fact]
        public void DisplayStepAndLimitsMatchTheOldMillisecondBoxes()
        {
            Assert.Equal(0.1m, DelayUnits.StepSeconds);
            Assert.Equal(10m, DelayUnits.MaxOnSeconds);
            Assert.Equal(60m, DelayUnits.MaxOffSeconds);
            Assert.Equal(10000m, DelayUnits.ClampMilliseconds(10001m, DelayUnits.MaxOnMilliseconds));
            Assert.Equal(0m, DelayUnits.ClampMilliseconds(-1m, DelayUnits.MaxOffMilliseconds));
            Assert.Equal(60000m, DelayUnits.ClampMilliseconds(60000m, DelayUnits.MaxOffMilliseconds));
        }

        [Fact]
        public void ARoundedDisplayIsNotTheStoredValueUntilTheUserEditsIt()
        {
            Assert.Equal(0.3m, DelayUnits.MillisecondsToSeconds(250));
            Assert.Equal(300m, DelayUnits.SecondsToMilliseconds(0.3m));
            Assert.NotEqual(250m, DelayUnits.SecondsToMilliseconds(DelayUnits.MillisecondsToSeconds(250)));
        }
    }
}
