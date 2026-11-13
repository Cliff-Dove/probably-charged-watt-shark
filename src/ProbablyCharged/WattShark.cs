using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ProbablyCharged
{
    public static class WattShark
    {
        // 物品 ID 与精灵键是不同契约，不从 manifest.ID 或显示名称推导。
        internal const string ItemId = "probably_charged_watt_shark";

        private const string ItemSpriteName = "watt_shark";
        private const string StoragePanelRole = "storage";
        private const string PowerPanelRole = "power";
        private const int StorageWidth = 15;
        private const int StorageHeight = 15;
        private const int ItemWidth = 4;
        private const int ItemHeight = 4;
        private const int CabinetValue = 1000;

        // 这些索引对应现有存档结构，面板及内部存储节点不得随意重排。
        private const int StorageInventoryIndex = 0;
        private const int PowerAreaInventoryIndex = 1;

        public static GameItem Create(string modId)
        {
            GameItem cabinet = ItemDirectory.CreateEmptyItem();
            cabinet.name = ModHelper.GetLocalized(modId, LocalizationKeys.WattSharkName);
            cabinet.SetContentWindow(CreateContainerWindow(modId, StorageWidth, StorageHeight));
            cabinet.SetSpriteAndShapeFromMod(modId, ItemSpriteName);
            cabinet.SetShape(new GridShapeBuilder(ItemWidth, ItemHeight).SetDataFill(1).Build());
            cabinet.SetValue(CabinetValue);
            return cabinet;
        }

        private static PixelWindow CreateContainerWindow(
            string modId, int storageWidth, int storageHeight)
        {
            GameGridInventory storageInventory =
                new GameGridInventory(storageWidth, storageHeight);
            storageInventory.mayInventoryAddItemFunc = (item, inventory) =>
                item.identifier != GameContent.PowerBlocItemId &&
                (item.IsGameItemType(GameContent.MachineItemType) ||
                 item.IsGameItemType(GameContent.EnergyCellItemType));

            string storageTitle = ModHelper.GetLocalized(
                modId, LocalizationKeys.WattSharkStorageTitle);

            GridShape powerBlocShape = GameItem.BuildShapeFromSprite(
                GameContent.PowerBlocSpriteAtlas, GameContent.PowerBlocSpriteName);
            int gridSize = UISettings.current.gridSize;
            int powerBlocWidth = powerBlocShape.globalWidth;
            int powerBlocHeight = powerBlocShape.globalHeight;
            int powerAreaWidthPixels = Mathf.Max(
                powerBlocWidth * gridSize,
                Mathf.CeilToInt(storageInventory.widthPixels / 2f));
            int powerAreaHeightPixels = Mathf.Max(
                powerBlocHeight * gridSize,
                Mathf.CeilToInt(storageInventory.heightPixels / 2f));

            // 过滤和堆叠规则由专用槽维护，外层布局只负责预留显示空间。
            GameSlotInventory powerSlot = new PowerBlocSlot(powerBlocWidth, powerBlocHeight);
            GridPixelElement powerSlotArea = new GridPixelElement(1, 1, isCentered: true);
            powerSlotArea.Attach(
                powerSlot,
                x: 0,
                y: 0,
                minimumWidth: powerAreaWidthPixels,
                minimumHeight: powerAreaHeightPixels);

            string powerTitle = ModHelper.GetLocalized(
                modId, LocalizationKeys.WattSharkPowerBlocTitle);

            PixelWindow window = MachineHelper.CreateMachineryWindow(
                true,
                MachineHelper.Panel(storageTitle, storageInventory, StoragePanelRole),
                MachineHelper.Panel(powerTitle, powerSlotArea, PowerPanelRole));
            window.SetTitle(ModHelper.GetLocalized(modId, LocalizationKeys.WattSharkName));
            return window;
        }

        private static GameSlotInventory GetPowerSlot(GameItem cabinet)
        {
            GridPixelElement powerSlotArea =
                (GridPixelElement)MachineHelper.GetBodyStorage(cabinet, PowerAreaInventoryIndex);
            return (GameSlotInventory)powerSlotArea.GetElement(0, 0);
        }

        public static void ChargeAtNight(GameItem cabinet)
        {
            GameGridInventory storageInventory =
                (GameGridInventory)MachineHelper.GetBodyStorage(cabinet, StorageInventoryIndex);
            GameSlotInventory powerSlot = GetPowerSlot(cabinet);
            GameItem? powerBloc = powerSlot.childItem;
            int cabinetHash = RuntimeHelpers.GetHashCode(cabinet);
            if (powerBloc == null)
            {
                ModEntry.Logger.Log(
                    $"[WattShark] ChargeSkipped: cabinetHash={cabinetHash:X8}, reason=EmptyPowerSlot");
                return;
            }

            int powerBlocCount = powerBloc.unitCount;
            // 原版充电量目前仅存在于回调局部变量中，游戏更新后需复核此规则。
            int chargePerPowerBloc = NetworkUpgrade.IsCommercialPowerUnlocked() ? 4 : 2;
            int chargePerBattery = powerBlocCount * chargePerPowerBloc;
            List<GameItem> batteries = GraphUtils.FindAllChildrenType<GameItem>(
                storageInventory,
                item => item.IsTag(PowerHelper.POWER_SOURCE_ITEM_TAG));
            int chargedBatteryCount = 0;
            long totalAppliedCharge = 0;

            ModEntry.Logger.Log(
                $"[WattShark] ChargeStarted: cabinetHash={cabinetHash:X8}, " +
                $"powerBlocCount={powerBlocCount}, chargePerPowerBloc={chargePerPowerBloc}, " +
                $"chargePerBattery={chargePerBattery}, batteryCount={batteries.Count}");

            // 递归结果已包含机器内电池，每个电池直接充一次，避免重复计算。
            foreach (GameItem battery in batteries)
            {
                GameItem? owner = battery.parentInventory.GetParentItem();
                string ownerInfo = owner == null
                    ? "<none>"
                    : $"{owner.name}#{RuntimeHelpers.GetHashCode(owner):X8}";
                int energyBefore = battery.GetTagReadonly(
                    PowerHelper.POWER_SOURCE_ITEM_ENERGY).GetInt();

                PowerHelper.ChargeBattery(battery, chargePerBattery);

                int energyAfter = battery.GetTagReadonly(
                    PowerHelper.POWER_SOURCE_ITEM_ENERGY).GetInt();
                int appliedCharge = energyAfter - energyBefore;
                totalAppliedCharge += appliedCharge;
                if (appliedCharge > 0)
                {
                    chargedBatteryCount++;
                }

                // 原版 API 可能因满电或充电限制不改变电量，记录实测差值而非声称成功。
                ModEntry.Logger.Log(
                    $"[WattShark] BatteryChargeChecked: " +
                    $"battery={battery.name}#{RuntimeHelpers.GetHashCode(battery):X8}, " +
                    $"owner={ownerInfo}, requested={chargePerBattery}, " +
                    $"applied={appliedCharge}, energyBefore={energyBefore}, energyAfter={energyAfter}");
            }

            ModEntry.Logger.Log(
                $"[WattShark] ChargeCompleted: cabinetHash={cabinetHash:X8}, " +
                $"batteryCount={batteries.Count}, chargedBatteryCount={chargedBatteryCount}, " +
                $"totalAppliedCharge={totalAppliedCharge}");
        }
    }
}
