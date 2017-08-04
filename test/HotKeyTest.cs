using GlobalHotKey;
using System.Windows.Input;
using Shadowsocks.Controller.Hotkeys;
using Xunit;

namespace test
{
    public class HotKeyTest
    {
        [Fact]
        public void TestHotKey2Str() {
            Assert.Equal( "Ctrl+A", HotKeys.HotKey2Str( Key.A, ModifierKeys.Control ) );
            Assert.Equal( "Ctrl+Alt+D2", HotKeys.HotKey2Str( Key.D2, (ModifierKeys.Alt | ModifierKeys.Control) ) );
            Assert.Equal("Ctrl+Alt+Shift+NumPad7", HotKeys.HotKey2Str(Key.NumPad7, (ModifierKeys.Alt|ModifierKeys.Control|ModifierKeys.Shift)));
            Assert.Equal( "Ctrl+Alt+Shift+F6", HotKeys.HotKey2Str( Key.F6, (ModifierKeys.Alt|ModifierKeys.Control|ModifierKeys.Shift)));
            Assert.NotEqual("Ctrl+Shift+Alt+F6", HotKeys.HotKey2Str(Key.F6, (ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift)));
        }

        [Fact]
        public void TestStr2HotKey()
        {
            Assert.True(HotKeys.Str2HotKey("Ctrl+A").Equals(new HotKey(Key.A, ModifierKeys.Control)));
            Assert.True(HotKeys.Str2HotKey("Ctrl+Alt+A").Equals(new HotKey(Key.A, (ModifierKeys.Control | ModifierKeys.Alt))));
            Assert.True(HotKeys.Str2HotKey("Ctrl+Shift+A").Equals(new HotKey(Key.A, (ModifierKeys.Control | ModifierKeys.Shift))));
            Assert.True(HotKeys.Str2HotKey("Ctrl+Alt+Shift+A").Equals(new HotKey(Key.A, (ModifierKeys.Control | ModifierKeys.Alt| ModifierKeys.Shift))));
            HotKey testKey0 = HotKeys.Str2HotKey("Ctrl+Alt+Shift+A");
            Assert.True(testKey0 != null && testKey0.Equals(new HotKey(Key.A, (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))));
            HotKey testKey1 = HotKeys.Str2HotKey("Ctrl+Alt+Shift+F2");
            Assert.True(testKey1 != null && testKey1.Equals(new HotKey(Key.F2, (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))));
            HotKey testKey2 = HotKeys.Str2HotKey("Ctrl+Shift+Alt+D7");
            Assert.True(testKey2 != null && testKey2.Equals(new HotKey(Key.D7, (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))));
            HotKey testKey3 = HotKeys.Str2HotKey("Ctrl+Shift+Alt+NumPad7");
            Assert.True(testKey3 != null && testKey3.Equals(new HotKey(Key.NumPad7, (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))));
        }
    }
}
