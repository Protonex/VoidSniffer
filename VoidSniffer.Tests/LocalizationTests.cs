using System.Collections.Generic;
using VoidSniffer;
using Xunit;

namespace VoidSniffer.Tests
{
    public class LocalizationTests
    {
        [Fact]
        public void Parse_ReadsKeysIgnoresCommentsAndUnescapesNewlines()
        {
            const string text =
                "; comment\n" +
                "# another\n" +
                "[section]\n" +
                "key.one=value\n" +
                "key.two=line1\\nline2\n" +
                "key.three=a=b\n";

            Dictionary<string, string> map = Localization.Parse(text);

            Assert.Equal("value", map["key.one"]);
            Assert.Equal("line1\nline2", map["key.two"]);
            Assert.Equal("a=b", map["key.three"]);
        }

        [Fact]
        public void T_UnknownKey_ReturnsKey()
        {
            Localization.Initialize("ru");
            Assert.Equal("no.such.key", Localization.T("no.such.key"));
        }

        [Fact]
        public void RussianPack_IsLoadedFromEmbeddedResource()
        {
            Localization.Initialize("ru");
            Assert.Equal("ru", Localization.Code);
            Assert.Equal("Сканировать", Localization.T("scan.text"));
            Assert.Equal("на диске", Localization.T("treemap.onDisk"));
        }

        [Fact]
        public void EnglishPack_IsLoadedAndUsesEnglishPlurals()
        {
            try
            {
                Localization.Initialize("en");
                Assert.Equal("en", Localization.Code);
                Assert.Equal("Scan", Localization.T("scan.text"));
                Assert.Equal("1 folder", Localization.Plural("count.folders", 1));
                Assert.Equal("2 folders", Localization.Plural("count.folders", 2));
            }
            finally
            {
                Localization.Initialize("ru"); // restore the default for other tests
            }
        }

        [Fact]
        public void RussianPlurals_UseOneFewMany()
        {
            Localization.Initialize("ru");
            Assert.Equal("1 файл", Localization.Plural("count.files", 1));
            Assert.Equal("2 файла", Localization.Plural("count.files", 2));
            Assert.Equal("5 файлов", Localization.Plural("count.files", 5));
            Assert.Equal("21 файл", Localization.Plural("count.files", 21));
        }

        [Fact]
        public void UnknownLanguage_FallsBackToDefault()
        {
            try
            {
                Localization.Initialize("zz");
                Assert.Equal("ru", Localization.Code);
            }
            finally
            {
                Localization.Initialize("ru");
            }
        }

        [Fact]
        public void AvailableLanguages_IncludesEmbeddedRuAndEn()
        {
            var codes = new List<string>();
            foreach (LanguageInfo info in Localization.AvailableLanguages()) codes.Add(info.Code);

            Assert.Contains("ru", codes);
            Assert.Contains("en", codes);
        }
    }
}
