using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using System;
using System.Reflection;
using UnityEngine;

namespace TakeAll
{
    //Shout out to Shapur1234's DFU-LootMenu repo which I learned a lot from
    //Some methods are taken from there and modified to fit the needs of the class below
    //Their repo is here: https://github.com/Shapur1234/DFU-LootMenu/blob/main/Scripts/LootMenu.cs
    public class TakeAllModManager : MonoBehaviour
    {
        internal static TakeAllModManager s_Instance { get; private set; }
        internal static Mod s_Mod;
        DaggerfallUI _DaggerfallUI;
        UserInterfaceManager _UIManager;
        DaggerfallInventoryWindow _DaggerfallInventoryWindow;
        KeyCode _TakeAllKeyCode = KeyCode.None;
        float _AvailableCarryWeightOnPlayer;
        bool _ShowTakeAllConfirmationPopupForWagon = true;
        bool _ShowPopupWhenTakeAllFailedToTakeEverything = true;

        [Invoke(StateManager.StateTypes.Start, 0)]
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
            s_Instance = this;
            s_Mod = ModManager.Instance.GetMod("TakeAll");
            ModSettings modSettings = s_Mod.GetSettings();
            string takeAllKeyBind = s_Mod.GetSettings().GetValue<string>("MainSettings", "TakeAllKeyBind");
            bool showTakeAllInventoryButton = s_Mod.GetSettings().GetValue<bool>("MainSettings", "ShowTakeAllInventoryButton");
            _ShowTakeAllConfirmationPopupForWagon = s_Mod.GetSettings().GetValue<bool>("MainSettings", "ShowTakeAllConfirmationPopupForWagon");
            _ShowPopupWhenTakeAllFailedToTakeEverything = s_Mod.GetSettings().GetValue<bool>("MainSettings", "ShowPopupWhenTakeAllFailedToTakeEverything");
            _TakeAllKeyCode = GetKeyCodeFromSettings(takeAllKeyBind);
            if (showTakeAllInventoryButton)
                UIWindowFactory.RegisterCustomUIWindow(UIWindowType.Inventory,typeof(TakeAllInventoryWindow));
            s_Mod.IsReady = true;
        }

        KeyCode GetKeyCodeFromSettings(string keyCodeString)
        {
            KeyCode keyCode = KeyCode.None;
            if (Enum.TryParse(keyCodeString, out keyCode))
                return keyCode;
            DaggerfallUI.MessageBox(s_Mod.Localize("setKeyForTakeAllKeyFail"));
            return KeyCode.Q;
        }

        void Update()
        {
            ListenToKeyboardInput();
        }

        void ListenToKeyboardInput()
        {
            if (Input.GetKeyDown(_TakeAllKeyCode) && InventoryWindowIsOpen())
                TakeAll();
        }

        internal void TakeAll()
        {
            if (ReestablishReferences())
            {
                if (InventoryRightColumnIsALootPileOrCorpse())
                {
                    ItemCollection lootPileOrCorpseItems = _DaggerfallInventoryWindow.LootTarget.Items;
                    TransferAsManyItemsAsYouCanToPlayer(lootPileOrCorpseItems);
                }
                else if (InventoryRightColumnIsDropStage())
                {
                    ItemCollection dropStageItems = GetRemoteItems();
                    TransferAsManyItemsAsYouCanToPlayer(dropStageItems);
                }
                else if (InventoryRightColumnIsWagon())
                {
                    ItemCollection wagonItems = GameManager.Instance.PlayerEntity.WagonItems;
                    if( _ShowTakeAllConfirmationPopupForWagon)
                        AskToTransferAllOfTheWagonItemsToPlayer(wagonItems);
                    else
                        TransferAsManyItemsAsYouCanToPlayer(wagonItems);
                }
                _DaggerfallInventoryWindow.Refresh();
            }
        }

        bool ReestablishReferences()
        {
            _DaggerfallUI = null;
            _DaggerfallUI = DaggerfallUI.Instance;
            _UIManager = null;
            _UIManager = DaggerfallUI.Instance.UserInterfaceManager;
            _DaggerfallInventoryWindow = null;
            _DaggerfallInventoryWindow = _DaggerfallUI.InventoryWindow;
            return (_DaggerfallUI != null && _UIManager != null && _DaggerfallInventoryWindow != null);
        }

        bool InventoryRightColumnIsALootPileOrCorpse()
        {
            DaggerfallLoot daggerfallLootTarget = _DaggerfallInventoryWindow.LootTarget;
            return (daggerfallLootTarget != null && daggerfallLootTarget.Items.Count > 0);
        }

        bool InventoryRightColumnIsWagon()
        {
            var currentWindow = _UIManager.TopWindow;

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

        bool InventoryRightColumnIsDropStage()
        {
            if (_DaggerfallInventoryWindow == null)
                return false;

            bool isDroppedType = _DaggerfallInventoryWindow.LootTarget == null;

            var remoteTargetTypeField = typeof(DaggerfallInventoryWindow).GetField("remoteTargetType", BindingFlags.NonPublic | BindingFlags.Instance);
            if (remoteTargetTypeField != null)
            {
                object value = remoteTargetTypeField.GetValue(_DaggerfallInventoryWindow);
                if (value != null && value.ToString() == "Dropped" && isDroppedType)
                {
                    ItemCollection remoteItems = GetRemoteItems();
                    if (remoteItems != null && remoteItems.Count > 0)
                        return true;
                }
            }

            return false;
        }

        ItemCollection GetRemoteItems()
        {
            var field = typeof(DaggerfallInventoryWindow).GetField("remoteItems", BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(_DaggerfallInventoryWindow) as ItemCollection;
        }

        bool InventoryWindowIsOpen()
        {
            return DaggerfallUI.Instance.UserInterfaceManager.TopWindow is DaggerfallInventoryWindow;
        }

        void AskToTransferAllOfTheWagonItemsToPlayer(ItemCollection itemCollection)
        {
            DaggerfallMessageBox daggerfallMessageBox = new DaggerfallMessageBox(_UIManager);
            daggerfallMessageBox.SetText(s_Mod.Localize("takeAllFromWagonConfirm"));
            Button yesButton = daggerfallMessageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
            Button noButton = daggerfallMessageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.No);
            noButton.OnMouseClick += (sender, e) =>
            {
                daggerfallMessageBox.CloseWindow();
            };
            yesButton.OnMouseClick += (sender, e) =>
            {
                daggerfallMessageBox.CloseWindow();
                TransferAsManyItemsAsYouCanToPlayer(itemCollection);
            };
            daggerfallMessageBox.Show();
            daggerfallMessageBox.ParentPanel.SetFocus();
        }

        void TransferAsManyItemsAsYouCanToPlayer(ItemCollection itemCollection)
        {
            bool couldntTakeAllItems = false;
            if (itemCollection != null && itemCollection.Count != 0)
            {
                for (int i = itemCollection.Count - 1; i >= 0; i--)
                {
                    DaggerfallUnityItem item = itemCollection.GetItem(i);
                    if (item != null)
                    {
                        bool playerCanCarryItem = CanPlayerCarryItem(item);
                        bool playerCanCarryPartOfTheItemStackButNotAll = CanPlayerCarryPartOfAnItemStackButNotAll(item);
                        if (playerCanCarryItem || playerCanCarryPartOfTheItemStackButNotAll)
                            TransferItemToPlayer(item, itemCollection);
                        else
                            couldntTakeAllItems = true;
                    }
                }
            }
            if(couldntTakeAllItems && _ShowPopupWhenTakeAllFailedToTakeEverything)
                DisplayTakeAllFailedToTakeEverythingWindow();
            _DaggerfallInventoryWindow.Refresh();
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
            else if (item.IsAStack() && _AvailableCarryWeightOnPlayer > 0)
            {
                DaggerfallUnityItem splitItem = new DaggerfallUnityItem();
                int canCarryAmount = (int)(_AvailableCarryWeightOnPlayer / item.weightInKg);

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

        bool CanPlayerCarryItem(DaggerfallUnityItem item, int index = 0)
        {
            bool playerCanCarryItems = false;
            _AvailableCarryWeightOnPlayer = GameManager.Instance.PlayerEntity.MaxEncumbrance - GameManager.Instance.PlayerEntity.CarriedWeight;

            if (item.IsAStack())
                playerCanCarryItems = ((item.weightInKg * item.stackCount) <= _AvailableCarryWeightOnPlayer);
            else
                playerCanCarryItems = (item.weightInKg <= _AvailableCarryWeightOnPlayer);
            return playerCanCarryItems;
        }

        bool CanPlayerCarryPartOfAnItemStackButNotAll(DaggerfallUnityItem stackItem)
        {
            _AvailableCarryWeightOnPlayer = GameManager.Instance.PlayerEntity.MaxEncumbrance - GameManager.Instance.PlayerEntity.CarriedWeight;
            if (stackItem == null || !stackItem.IsAStack() || !Mathf.Approximately(_AvailableCarryWeightOnPlayer, 0f))
                return false;
            float stackItemTotalWeight = stackItem.weightInKg * stackItem.stackCount;
            return (stackItemTotalWeight > _AvailableCarryWeightOnPlayer);
        }

        void DisplayTakeAllFailedToTakeEverythingWindow()
        {
            DaggerfallMessageBox daggerfallMessageBox = new DaggerfallMessageBox(_UIManager);
            daggerfallMessageBox.SetText(s_Mod.Localize("takeAllFailedToTakeEverything"));
            Button okButton = daggerfallMessageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.OK);
            okButton.OnMouseClick += (sender, e) =>
            {
                daggerfallMessageBox.CloseWindow();
            };
            daggerfallMessageBox.Show();
            daggerfallMessageBox.ParentPanel.SetFocus();
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

                DaggerfallMessageBox mapText = new DaggerfallMessageBox(_UIManager);
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
    }
}