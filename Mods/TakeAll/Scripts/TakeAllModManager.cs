using UnityEngine;
using System;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop;
using System.Reflection;

namespace TakeAll
{
    //Shout out to Shapur1234's DFU-LootMenu repo which I learned a lot from
    //Some methods are taken from there and modified to fit the needs of the class below
    //Their repo is here: https://github.com/Shapur1234/DFU-LootMenu/blob/main/Scripts/LootMenu.cs
    public class TakeAllModManager : MonoBehaviour
    {
        static Mod s_Mod;
        DaggerfallUI m_DaggerfallUI;
        UserInterfaceManager m_UIManager;
        DaggerfallInventoryWindow m_DaggerfallInventoryWindow;
        KeyCode m_TakeAllKeyCode = KeyCode.None;
        float m_AvailableCarryWeightOnPlayer;

        [Invoke(StateManager.StateTypes.Game, 0)]
        public static void Init(InitParams initParams)
        {
            s_Mod = initParams.Mod;
            GameObject modGameObject = new GameObject(s_Mod.Title);
            modGameObject.AddComponent<TakeAllModManager>();
        }

        void Awake()
        {
            PrepareMod();
        }

        void PrepareMod()
        {
            s_Mod = ModManager.Instance.GetMod("TakeAll");
            ModSettings modSettings = s_Mod.GetSettings();
            string takeAllKeyBind = s_Mod.GetSettings().GetValue<string>("MainSettings", "TakeAllKeyBind");
            m_TakeAllKeyCode = GetKeyCodeFromSettings(takeAllKeyBind);
            s_Mod.IsReady = true;
        }

        void Update()
        {
            ListenToKeyboardInput();
        }

        KeyCode GetKeyCodeFromSettings(string keyCodeString)
        {
            KeyCode keyCode = KeyCode.None;
            if (Enum.TryParse(keyCodeString, out keyCode))
            {
                return keyCode;
            }
            else
            {
                DaggerfallUI.MessageBox(s_Mod.Localize("setKeyForTakeAllKeyFail"));
                return KeyCode.Q;
            }
        }

        void ListenToKeyboardInput()
        {
            if (Input.GetKeyDown(m_TakeAllKeyCode) && ReestablishReferences())
            {
                DaggerfallLoot daggerfallLootTarget = m_DaggerfallInventoryWindow.LootTarget;
                if (daggerfallLootTarget != null && daggerfallLootTarget.Items.Count > 0)
                {
                    if (!CanPlayerCarryAllOfTheItems(daggerfallLootTarget.Items))
                    {
                        Debug.Log("Player cannot carry all of the items!");
                        DisplayTakeAllFailedToTakeEverythingWindow();
                    }
                    else
                        Debug.Log("Player CAN carry all of the items!");
                    TransferAsManyItemsAsYouCanToPlayer(m_DaggerfallInventoryWindow.LootTarget.Items);
                }
                else if (IsWagonTabActive())
                    AskToTransferAllOfTheWagonItemsToPlayer(GameManager.Instance.PlayerEntity.WagonItems);
                m_DaggerfallInventoryWindow.Refresh();
            }
        }

        void AskToTransferAllOfTheWagonItemsToPlayer(ItemCollection itemCollection)
        {
            DaggerfallMessageBox daggerfallMessageBox1 = new DaggerfallMessageBox(m_UIManager);
            daggerfallMessageBox1.SetText(s_Mod.Localize("takeAllFromWagonConfirm"));
            daggerfallMessageBox1.ParentPanel.SetFocus();
            Button yesButton = daggerfallMessageBox1.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
            Button noButton = daggerfallMessageBox1.AddButton(DaggerfallMessageBox.MessageBoxButtons.No);
            noButton.OnMouseClick += (sender, e) => { daggerfallMessageBox1.CloseWindow(); };

            DaggerfallMessageBox daggerfallMessageBox2 = new DaggerfallMessageBox(m_UIManager);
            daggerfallMessageBox2.SetText(s_Mod.Localize("takeAllFailedToTakeEverything"));
            Button okButton = daggerfallMessageBox2.AddButton(DaggerfallMessageBox.MessageBoxButtons.OK);
            okButton.OnMouseClick += (sender, e) => { daggerfallMessageBox2.CloseWindow(); };

            yesButton.OnMouseClick += (sender, e) =>
            {
                TransferAsManyItemsAsYouCanToPlayer(itemCollection);
                m_DaggerfallInventoryWindow.Refresh();
                daggerfallMessageBox1.CloseWindow();
                if (!CanPlayerCarryAllOfTheItems(itemCollection))
                {
                    daggerfallMessageBox2.Show();
                    daggerfallMessageBox2.ParentPanel.SetFocus();
                }
            };

            daggerfallMessageBox1.Show();
        }

        void DisplayTakeAllFailedToTakeEverythingWindow()
        {
            DaggerfallMessageBox daggerfallMessageBox = new DaggerfallMessageBox(m_UIManager);
            daggerfallMessageBox.SetText(s_Mod.Localize("takeAllFailedToTakeEverything"));
            Button okButton = daggerfallMessageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.OK);
            okButton.OnMouseClick += (sender, e) => { daggerfallMessageBox.CloseWindow(); };
            daggerfallMessageBox.Show();
            daggerfallMessageBox.ParentPanel.SetFocus();
        }

        bool ReestablishReferences()
        {
            m_DaggerfallUI = null;
            m_DaggerfallUI = DaggerfallUI.Instance;
            m_UIManager = null;
            m_UIManager = DaggerfallUI.Instance.UserInterfaceManager;
            m_DaggerfallInventoryWindow = null;
            m_DaggerfallInventoryWindow = m_DaggerfallUI.InventoryWindow;
            return (m_DaggerfallUI != null && m_UIManager != null && m_DaggerfallInventoryWindow != null);
        }

        void TransferAsManyItemsAsYouCanToPlayer(ItemCollection itemCollection)
        {
            if (itemCollection != null && itemCollection.Count != 0)
            {
                for (int i = itemCollection.Count - 1; i >= 0; i--)
                {
                    DaggerfallUnityItem item = itemCollection.GetItem(i);
                    if (item != null)
                        TransferItemToPlayer(item, itemCollection);
                }
            }
        }

        void TransferItemToPlayer(DaggerfallUnityItem item, ItemCollection from)
        {
            ItemCollection.AddPosition itemPosition;

            if (item.ItemGroup == ItemGroups.Transportation || item.IsSummoned)
                return;

            bool playerCanCarryItem = CanPlayerCarryItem(item);

            if (playerCanCarryItem)
            {
                if (item.IsOfTemplate(ItemGroups.Currency, (int)Currency.Gold_pieces))
                {
                    GameManager.Instance.PlayerEntity.GoldPieces += item.stackCount;
                    DaggerfallUI.Instance.PlayOneShot(SoundClips.GoldPieces);
                    from.RemoveItem(item);
                }
                else if (item.IsOfTemplate(ItemGroups.MiscItems, (int)MiscItems.Map))
                {
                    RecordLocationFromMap(item);
                    from.RemoveItem(item);
                }
                else
                {
                    if (item.IsQuestItem)
                        itemPosition = ItemCollection.AddPosition.Front;
                    else
                        itemPosition = ItemCollection.AddPosition.DontCare;

                    GameManager.Instance.PlayerEntity.Items.Transfer(item, from, itemPosition);
                    DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
                }
            }
            else
            {
                if (item.IsAStack() && m_AvailableCarryWeightOnPlayer > 0)
                {
                    DaggerfallUnityItem splitItem = new DaggerfallUnityItem();
                    int canCarryAmount = (int)(m_AvailableCarryWeightOnPlayer / item.weightInKg);

                    if (item.IsOfTemplate(ItemGroups.Currency, (int)Currency.Gold_pieces))
                    {
                        splitItem = from.SplitStack(item, canCarryAmount);
                        GameManager.Instance.PlayerEntity.GoldPieces += splitItem.stackCount;
                        from.RemoveItem(splitItem);

                        DaggerfallUI.Instance.PlayOneShot(SoundClips.GoldPieces);
                    }
                    else
                    {
                        splitItem = from.SplitStack(item, canCarryAmount);

                        if (splitItem != null && splitItem.IsQuestItem)
                            itemPosition = ItemCollection.AddPosition.Front;
                        else
                            itemPosition = ItemCollection.AddPosition.DontCare;

                        GameManager.Instance.PlayerEntity.Items.Transfer(splitItem, from, itemPosition);
                        from.RemoveItem(splitItem);

                        DaggerfallUI.Instance.PlayOneShot(SoundClips.GoldPieces);
                    }
                }
            }
        }

        bool CanPlayerCarryItem(DaggerfallUnityItem item, int index = 0)
        {
            bool playerCanCarryItems = false;
            m_AvailableCarryWeightOnPlayer = GameManager.Instance.PlayerEntity.MaxEncumbrance - GameManager.Instance.PlayerEntity.CarriedWeight;

            if (item.IsAStack())
                playerCanCarryItems = ((item.weightInKg * item.stackCount) <= m_AvailableCarryWeightOnPlayer);
            else
                playerCanCarryItems = (item.weightInKg <= m_AvailableCarryWeightOnPlayer);
            return playerCanCarryItems;
        }

        bool CanPlayerCarryAllOfTheItems(ItemCollection itemCollection)
        {
            float availableCarryWeight = GameManager.Instance.PlayerEntity.MaxEncumbrance - GameManager.Instance.PlayerEntity.CarriedWeight;
            for (int i = 0; i < itemCollection.Count; i++)
            {
                DaggerfallUnityItem item = itemCollection.GetItem(i);
                if (item == null)
                    continue;
                float itemTotalWeight = item.IsAStack() ? item.weightInKg * item.stackCount : item.weightInKg;
                if (itemTotalWeight > availableCarryWeight)
                    return false;
                availableCarryWeight -= itemTotalWeight;
            }
            return true;
        }

        void RecordLocationFromMap(DaggerfallUnityItem item)
        {
            const int mapTextId = 499;
            PlayerGPS playerGPS = GameManager.Instance.PlayerGPS;

            try
            {
                DFLocation revealedLocation = playerGPS.DiscoverRandomLocation();

                if (string.IsNullOrEmpty(revealedLocation.Name))
                    throw new Exception();

                playerGPS.LocationRevealedByMapItem = revealedLocation.Name;
                GameManager.Instance.PlayerEntity.Notebook.AddNote(
                    TextManager.Instance.GetLocalizedText("readMap").Replace("%map", revealedLocation.Name));

                DaggerfallMessageBox mapText = new DaggerfallMessageBox(m_UIManager);
                mapText.SetTextTokens(DaggerfallUnity.Instance.TextProvider.GetRandomTokens(mapTextId));
                mapText.ParentPanel.BackgroundColor = Color.clear;
                mapText.ClickAnywhereToClose = true;
                mapText.Show();
            }
            catch (Exception)
            {
                DaggerfallUI.MessageBox(TextManager.Instance.GetLocalizedText("readMapFail"));
            }
        }

        bool IsWagonTabActive()
        {
            var currentWindow = m_UIManager.TopWindow;

            if (currentWindow is DaggerfallInventoryWindow inventoryWindow)
            {
                FieldInfo usingWagonField = typeof(DaggerfallInventoryWindow).GetField("usingWagon", BindingFlags.NonPublic | BindingFlags.Instance);
                if (usingWagonField != null)
                {
                    bool isUsingWagon = (bool)usingWagonField.GetValue(inventoryWindow);
                    return isUsingWagon;
                }
            }
            return false;
        }
    }
}