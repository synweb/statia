using System;
using Xunit;

namespace Statia.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void DummyTest()
        {
            Assert.Equal(1,1);
        }

        private const string CONTENT_DIR = "/var/www/html";
        
        [Theory]
        [InlineData("/var/www/html/index.html", (string)null, "/")]
        [InlineData("/var/www/html/index.ru.html", "ru", "/")]
        [InlineData("/var/www/html/index.en.html", "en", "/")]
        [InlineData("/var/www/html/index.english.html", (string) null, "/index.english")]
        [InlineData("/var/www/html/style.css", (string) null, "/style.css")]
        [InlineData("/var/www/html/css/style.css", (string) null, "/css/style.css")]
        [InlineData("/var/www/html/folder/index.html", (string) null, "/folder")]
        [InlineData("/var/www/html/folder.html", (string) null, "/folder")]
        public void GetLocaleAndRelativeUrlTest(string filePath, string expectedLocale, string expectedRelativeUrl)
        {
            var loader = new Loader(CONTENT_DIR);
            var localeUrlPair = loader.GetLocaleAndRelativeUrl(filePath);
            Assert.Equal(expectedLocale, localeUrlPair.Item1);
            Assert.Equal(expectedRelativeUrl, localeUrlPair.Item2);
        }
    }
}