using VoidSniffer.Services;
using Xunit;

namespace VoidSniffer.Tests
{
    public class AppSettingsTests
    {
        [Fact]
        public void Defaults_AreSensible()
        {
            var s = new AppSettings();
            Assert.Equal(200000, s.MaxResults);
            Assert.Equal(4, s.RecursionDepth);
            Assert.False(s.ShowFreeSpace);
            Assert.True(s.AutoExpand);
            Assert.False(s.Sdk3);
        }

        [Fact]
        public void Parse_ReadsValues()
        {
            const string ini =
                "; settings\n" +
                "language=en\n" +
                "sdk3=1\n" +
                "max_results=5000\n" +
                "recursion_depth=6\n" +
                "show_free_space=1\n" +
                "auto_expand=0\n" +
                "auto_expand_budget=42\n" +
                "auto_expand_seconds=3\n" +
                "window_width=1280\n" +
                "window_height=800\n" +
                "window_maximized=1\n" +
                "last_path=D:\\Data\n";

            AppSettings s = AppSettings.Parse(ini);

            Assert.Equal("en", s.Language);
            Assert.True(s.Sdk3);
            Assert.Equal(5000, s.MaxResults);
            Assert.Equal(6, s.RecursionDepth);
            Assert.True(s.ShowFreeSpace);
            Assert.False(s.AutoExpand);
            Assert.Equal(42, s.AutoExpandBudget);
            Assert.Equal(3, s.AutoExpandSeconds);
            Assert.Equal(1280, s.WindowWidth);
            Assert.Equal(800, s.WindowHeight);
            Assert.True(s.WindowMaximized);
            Assert.Equal("D:\\Data", s.LastPath);
        }

        [Fact]
        public void Parse_ClampsOutOfRangeValues()
        {
            AppSettings s = AppSettings.Parse(
                "recursion_depth=99\nmax_results=1\nauto_expand_budget=0\nauto_expand_seconds=0\nwindow_width=10\n");

            Assert.Equal(8, s.RecursionDepth);
            Assert.Equal(1000, s.MaxResults);
            Assert.Equal(10, s.AutoExpandBudget);
            Assert.Equal(1, s.AutoExpandSeconds);
            Assert.Equal(400, s.WindowWidth);
        }

        [Fact]
        public void Parse_AcceptsVariousTrueTokens()
        {
            Assert.True(AppSettings.Parse("auto_expand=1").AutoExpand);
            Assert.True(AppSettings.Parse("auto_expand=true").AutoExpand);
            Assert.True(AppSettings.Parse("auto_expand=yes").AutoExpand);
            Assert.True(AppSettings.Parse("auto_expand=on").AutoExpand);
            Assert.False(AppSettings.Parse("auto_expand=false").AutoExpand);
        }

        [Fact]
        public void ToIni_RoundTrips()
        {
            var original = new AppSettings
            {
                Language = "en",
                Sdk3 = true,
                MaxResults = 12345,
                RecursionDepth = 5,
                ShowFreeSpace = true,
                AutoExpand = false,
                AutoExpandBudget = 77,
                AutoExpandSeconds = 9,
                WindowWidth = 1111,
                WindowHeight = 777,
                WindowMaximized = true,
                LastPath = "E:\\Temp"
            };

            AppSettings parsed = AppSettings.Parse(original.ToIni());

            Assert.Equal(original.Language, parsed.Language);
            Assert.Equal(original.Sdk3, parsed.Sdk3);
            Assert.Equal(original.MaxResults, parsed.MaxResults);
            Assert.Equal(original.RecursionDepth, parsed.RecursionDepth);
            Assert.Equal(original.ShowFreeSpace, parsed.ShowFreeSpace);
            Assert.Equal(original.AutoExpand, parsed.AutoExpand);
            Assert.Equal(original.AutoExpandBudget, parsed.AutoExpandBudget);
            Assert.Equal(original.AutoExpandSeconds, parsed.AutoExpandSeconds);
            Assert.Equal(original.WindowWidth, parsed.WindowWidth);
            Assert.Equal(original.WindowHeight, parsed.WindowHeight);
            Assert.Equal(original.WindowMaximized, parsed.WindowMaximized);
            Assert.Equal(original.LastPath, parsed.LastPath);
        }
    }
}
