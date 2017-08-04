using Shadowsocks.Controller;
using Xunit;

namespace test
{
    public class VersionCompareTest
    {
        [Fact]
        public void TestCompareVersion()
        {
            Assert.True(UpdateChecker.Asset.CompareVersion("2.3.1.0", "2.3.1") == 0);
            Assert.True(UpdateChecker.Asset.CompareVersion("1.2", "1.3") < 0);
            Assert.True(UpdateChecker.Asset.CompareVersion("1.3", "1.2") > 0);
            Assert.True(UpdateChecker.Asset.CompareVersion("1.3", "1.3") == 0);
            Assert.True(UpdateChecker.Asset.CompareVersion("1.2.1", "1.2") > 0);
            Assert.True(UpdateChecker.Asset.CompareVersion("2.3.1", "2.4") < 0);
            Assert.True(UpdateChecker.Asset.CompareVersion("1.3.2", "1.3.1") > 0);
        }
    }
}