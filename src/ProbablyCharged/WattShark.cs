using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;

namespace ProbablyCharged
{
    public class WattShark
    {
        private const int STORAGE_WIDTH = 15;
        private const int STORAGE_HEIGHT = 15;

        private const int CABINET_VALUE = 1000;
        public static GameItem SharkDrop(string modId)
        {
            return InitBasics(modId);
        }

        private static GameItem InitBasics(string modId)
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            item.name = ModHelper.GetLocalized(modId, "watt_shark_name");
            item.SetContentWindow(CreateContainerWindow(modId, STORAGE_WIDTH, STORAGE_HEIGHT));
            item.SetSpriteAndShapeFromMod(modId, "watt_shark");
            item.SetShape(new GridShapeBuilder(4, 4).SetDataFill(1).Build());
            item.SetValue(CABINET_VALUE);
            return item;
        }
        private static PixelWindow CreateContainerWindow(string modId, int width, int height)
        {
            //机器存储区
            GameGridInventory storage = new GameGridInventory(width, height);
            storage.mayInventoryAddItemFunc = (item, inventory) =>
            {
                return item.identifier != "powerblock" && (item.IsGameItemType("MACHINE") || item.IsGameItemType("ENERGY_CELL"));
            };
            string storageTitle = ModHelper.GetLocalized(modId, "machine_slot_title");

            // 能量阵列槽位
            GridShape blockShape = GameItem.BuildShapeFromSprite("Items/items_tool", "powerblock");
            int gridSize = UISettings.current.gridSize;
            int blockWidth = blockShape.globalWidth;
            int blockHeight = blockShape.globalHeight;
            int areaWidthPixels = Mathf.Max(
                blockWidth * gridSize,
                Mathf.CeilToInt(storage.widthPixels / 2f)
            );
            int areaHeightPixels = Mathf.Max(
                blockHeight * gridSize,
                Mathf.CeilToInt(storage.heightPixels / 2f)
            );
            GameSlotInventory powerBlocks = new PowerBlocSlot(blockWidth, blockHeight);
            powerBlocks.mayInventoryAddItemFunc = (item, inventory) =>
            {
                return item.identifier == "powerblock";
            };
            // 一个布局单元，把真实槽位放在预留区域中央。
            GridPixelElement powerArea = new GridPixelElement(1, 1, isCentered: true);
            powerArea.Attach(
                powerBlocks,
                x: 0,
                y: 0,
                minimumWidth: areaWidthPixels,
                minimumHeight: areaHeightPixels
            );
            string powerAreaTitle = ModHelper.GetLocalized(modId, "power_slot_title");

            PixelWindow window = MachineHelper.CreateMachineryWindow(
                true,
                MachineHelper.Panel(storageTitle, storage, "storage"),
                MachineHelper.Panel(powerAreaTitle, powerArea, "power"));

            window.SetTitle(ModHelper.GetLocalized(modId, "watt_shark_name"));

            return window;
        }


        public static void ChargeAtNight(GameItem cabinet)
        {
            GameGridInventory storage = (GameGridInventory)MachineHelper.GetBodyStorage(cabinet, 0);
            GridPixelElement powerArea = (GridPixelElement)MachineHelper.GetBodyStorage(cabinet, 1);
            GameSlotInventory powerSlot = (GameSlotInventory)powerArea.GetElement(0, 0);
            GameItem? powerBlock = powerSlot.childItem;
            if (powerBlock == null)
            {
                return;
            }
            int n = powerBlock.unitCount;
            int p = NetworkUpgrade.IsCommercialPowerUnlocked() ? 4 : 2;
            int amount = n * p;
            ModEntry.logger.Log($"`WattShark` biting at night: {n} power blocks, {p} power per block, total charge amount: {amount}");
            List<GameItem> batteries = GraphUtils.FindAllChildrenType<GameItem>(storage, item => item.IsTag(PowerHelper.POWER_SOURCE_ITEM_TAG));
            foreach (GameItem battery in batteries)
            {
                GameItem? owner = battery.parentInventory.GetParentItem();

                string ownerInfo = owner == null
                    ? "<has no owner>"
                    : $"{owner.name}#{RuntimeHelpers.GetHashCode(owner):X8}";
                ModEntry.logger.Log($"Charging battery in {ownerInfo}: {battery.name}#{RuntimeHelpers.GetHashCode(battery):X8}, amount: {amount}");
                PowerHelper.ChargeBattery(battery, amount);
            }
        }
    }
}
