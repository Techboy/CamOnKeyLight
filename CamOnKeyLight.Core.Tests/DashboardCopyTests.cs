using CamOnKeyLight.Core;

namespace CamOnKeyLight.Core.Tests
{
    public class DashboardCopyTests
    {
        [Fact]
        public void OffCameraUsesTheStatusHeadline()
        {
            Assert.Equal("Lights are off", DashboardCopy.Headline(LightSummaryKind.AllOff, "Lights are OFF"));
            Assert.Equal("because your camera is off", DashboardCopy.Reason(LightMode.Auto, cameraOn: false, quiet: false, anyLights: true));
            Assert.Equal("Auto: lights follow the camera", DashboardCopy.ModePill(LightMode.Auto));
        }

        [Fact]
        public void ManualModesExplainWhoIsHoldingTheLights()
        {
            Assert.Equal("Lights are on", DashboardCopy.Headline(LightSummaryKind.AllOn, "Lights are ON via relay"));
            Assert.Equal("because you asked the lights to stay on", DashboardCopy.Reason(LightMode.On, cameraOn: false, quiet: true, anyLights: true));
            Assert.Equal("On: lights stay on", DashboardCopy.ModePill(LightMode.On));
            Assert.Equal("because you asked the lights to stay off", DashboardCopy.Reason(LightMode.Off, cameraOn: true, quiet: false, anyLights: true));
            Assert.Equal("Off: lights stay off", DashboardCopy.ModePill(LightMode.Off));
        }

        [Fact]
        public void QuietHoursAndAnEmptyListHaveTheirOwnReasons()
        {
            Assert.Equal("because quiet hours are on", DashboardCopy.Reason(LightMode.Auto, cameraOn: true, quiet: true, anyLights: true));
            Assert.Equal("because your camera is on", DashboardCopy.Reason(LightMode.Auto, cameraOn: true, quiet: false, anyLights: true));
            Assert.Equal("Add a light and it will follow your camera.", DashboardCopy.Reason(LightMode.Auto, cameraOn: true, quiet: false, anyLights: false));
        }

        [Fact]
        public void MixedAndRelaySummariesStaySpecific()
        {
            Assert.Equal("1 on, 1 off", DashboardCopy.Headline(LightSummaryKind.Mixed, "1 on, 1 off via relay"));
            Assert.Equal("Lights are unreachable", DashboardCopy.Headline(LightSummaryKind.Problems, "Lights are unreachable"));
            Assert.Equal("No lights added", DashboardCopy.Headline(LightSummaryKind.Empty, "No lights added"));
        }

        [Fact]
        public void NewInstallDefaultsToDarkAndKeepsASavedSystemChoice()
        {
            Assert.Equal("Dark", AppUserSettings.CreateDefault().Theme);
            Assert.Equal("Dark", SettingsJson.Deserialize("{}")!.Theme);
            Assert.Equal(ThemePreference.Dark, ThemePreferenceParser.Parse("Dark"));
            Assert.Equal(ThemePreference.FollowWindows, ThemePreferenceParser.Parse("FollowWindows"));
            Assert.Equal(ThemePreference.FollowWindows, ThemePreferenceParser.Parse("nope"));
        }
    }
}
