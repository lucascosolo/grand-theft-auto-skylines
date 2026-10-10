using GtaSkylines.Game.Input;
using GtaSkylines.Mod.Play;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class MenuRouterTests
    {
        private static InputFrame Pressed(params GameButton[] buttons)
        {
            var f = new InputFrame();
            foreach (GameButton b in buttons) { f.Pressed |= InputFrame.Bit(b); f.Held |= InputFrame.Bit(b); }
            return f;
        }

        private static OpenMenus Clear { get { return new OpenMenus { PlayerAlive = true }; } }

        private static MenuAction Decide(OpenMenus o, params GameButton[] pressed)
        {
            return MenuRouter.Decide(Pressed(pressed), o);
        }

        [Fact]
        public void NothingPressedIsNone()
        {
            Assert.Equal(MenuAction.None, Decide(Clear));
        }

        [Fact]
        public void PhoneKeyOpensThePhoneFromNothing()
        {
            Assert.Equal(MenuAction.OpenPhone, Decide(Clear, GameButton.Phone));
        }

        [Fact]
        public void PhoneKeyNeverClosesThePhone_EvenWithMenuUpInTheSameFrame()
        {
            OpenMenus o = Clear; o.Phone = true;
            Assert.Equal(MenuAction.None, Decide(o, GameButton.Phone));
            Assert.Equal(MenuAction.None, Decide(o, GameButton.Phone, GameButton.MenuUp));
        }

        [Fact]
        public void PhoneKeyIsIgnoredWhileDead()
        {
            OpenMenus o = Clear; o.PlayerAlive = false;
            Assert.Equal(MenuAction.None, Decide(o, GameButton.Phone));
        }

        [Fact]
        public void PhoneKeyIsIgnoredInAShop()
        {
            OpenMenus o = Clear; o.Shop = true;
            Assert.Equal(MenuAction.None, Decide(o, GameButton.Phone));
        }

        [Fact]
        public void PhoneKeyIsIgnoredInTheInteractionMenu()
        {
            OpenMenus o = Clear; o.Interaction = true;
            Assert.Equal(MenuAction.None, Decide(o, GameButton.Phone));
        }

        [Fact]
        public void PhoneKeyIsIgnoredInPause()
        {
            OpenMenus o = Clear; o.Pause = true;
            Assert.Equal(MenuAction.None, Decide(o, GameButton.Phone));
        }

        [Fact]
        public void EscapeOpensPauseFromNothing()
        {
            Assert.Equal(MenuAction.OpenPause, Decide(Clear, GameButton.Pause));
        }

        [Fact]
        public void EscapeClosesPause()
        {
            OpenMenus o = Clear; o.Pause = true;
            Assert.Equal(MenuAction.ClosePause, Decide(o, GameButton.Pause));
        }

        [Fact]
        public void EscapeClosesThePhone()
        {
            OpenMenus o = Clear; o.Phone = true;
            Assert.Equal(MenuAction.ClosePhone, Decide(o, GameButton.Pause));
        }

        [Fact]
        public void EscapeInAShopOrTheInteractionMenuIsLeftToThatMenu()
        {
            OpenMenus shop = Clear; shop.Shop = true;
            OpenMenus inter = Clear; inter.Interaction = true;
            Assert.Equal(MenuAction.None, Decide(shop, GameButton.Pause));
            Assert.Equal(MenuAction.None, Decide(inter, GameButton.Pause));
        }

        [Fact]
        public void WhilePauseIsOpenOtherMenuKeysDoNothing()
        {
            OpenMenus o = Clear; o.Pause = true;
            Assert.Equal(MenuAction.None, Decide(o, GameButton.InteractionMenu));
            Assert.Equal(MenuAction.None, Decide(o, GameButton.BigMap));
        }

        [Fact]
        public void InteractionKeyToggles_ExceptInAShop()
        {
            Assert.Equal(MenuAction.ToggleInteraction, Decide(Clear, GameButton.InteractionMenu));
            OpenMenus open = Clear; open.Interaction = true;
            Assert.Equal(MenuAction.ToggleInteraction, Decide(open, GameButton.InteractionMenu));
            OpenMenus shop = Clear; shop.Shop = true;
            Assert.Equal(MenuAction.None, Decide(shop, GameButton.InteractionMenu));
        }

        [Fact]
        public void BigMapKeyToggles()
        {
            Assert.Equal(MenuAction.ToggleBigMap, Decide(Clear, GameButton.BigMap));
        }

        [Fact]
        public void RebindingSwallowsEverything()
        {
            OpenMenus o = Clear; o.Rebinding = true; o.Pause = true;
            Assert.Equal(MenuAction.None, Decide(o, GameButton.Pause));
            Assert.Equal(MenuAction.None, Decide(o, GameButton.Phone));
            Assert.Equal(MenuAction.None, Decide(o, GameButton.InteractionMenu));
            Assert.Equal(MenuAction.None, Decide(o, GameButton.BigMap));
        }
    }
}
