using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    public class TakeAllInventoryWindow : DaggerfallInventoryWindow
    {
        public TakeAllInventoryWindow(IUserInterfaceManager uiManager, DaggerfallBaseWindow previous = null) : base(uiManager, previous){}

        protected override void Setup()
        {
            base.Setup();
            Vector2 buttonPosition = new Vector2(226, 94);
            Vector2 buttonSize = new Vector2(31, 9);
            Button takeAllButton = DaggerfallUI.AddButton(buttonPosition, buttonSize, NativePanel);
            takeAllButton.Label.Text = TakeAll.TakeAllModManager.s_Mod.Localize("takeAllbuttonLabel");
            takeAllButton.OnMouseClick += (sender, args) => { TakeAll.TakeAllModManager.s_Instance.TakeAll(); };
        }
    }
}