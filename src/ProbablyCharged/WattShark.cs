using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProbablyCharged
{
    public class WattShark
    {
        private const int STORAGE_WIDTH = 12;
        private const int STORAGE_HEIGHT = 12;
        public static GameItem SharkDrop(string modId)
        {
            return InitBasics(modId);
        }

        private static GameItem InitBasics(string modId)
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            item.name = ModHelper.GetLocalized(modId, "watt_shark_name");
            item.shortDescription = ModHelper.GetLocalized(modId, "watt_shark_short_description");
            item.longDescription = ModHelper.GetLocalized(modId, "watt_shark_description");
            item.SetContentWindow(CreateContainerWindow(item.name, STORAGE_WIDTH, STORAGE_HEIGHT));
            item.SetSpriteAndShapeFromMod(modId, "watt_shark");
            item.SetShape(new GridShapeBuilder(5, 5).SetDataFill(1).Build());
            return item;
        }
        private static PixelWindow CreateContainerWindow(string title, int width, int height)
        {
            GameGridInventory storage = new GameGridInventory(width, height);
            storage.mayInventoryAddItemFunc = (item, inventory) =>
            {
                return item.IsGameItemType("MACHINE") || item.IsGameItemType("ENERGY_CELL");
            };

            PixelWindow window = MachineHelper.CreateMachineryWindow(
                true,
                MachineHelper.Panel(title, storage, "storage"));

            window.SetTitle(title);

            return window;
        }
    }
}
