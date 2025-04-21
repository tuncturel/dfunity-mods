using UnityEngine;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Utility;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    public class TakeAllInventory: DaggerfallInventoryWindow
    {
        public TakeAllInventory(IUserInterfaceManager uiManager, DaggerfallBaseWindow previous = null)
            : base(uiManager, previous)
        {
        }

        // squeeze a custom button to the inventory
        protected override void Setup()
        {
            base.Setup();

            Vector2 buttonPos = new Vector2(226, 94);
            Vector2 buttonSize = new Vector2(31, 9);

            Button takeAllbtn = DaggerfallUI.AddButton(buttonPos, buttonSize, NativePanel);
            takeAllbtn.Label.Text = TakeAll.TakeAllModManager.s_Mod.Localize("takeAllbtnLabel");
            takeAllbtn.OnMouseClick += (sender, args) =>
            {
                TakeAll.TakeAllModManager.Instance.TakeEverything();
            };
        }
    }
}
