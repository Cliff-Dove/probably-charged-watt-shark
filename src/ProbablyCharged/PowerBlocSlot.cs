using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ProbablyCharged
{
    internal sealed class PowerBlocSlot : GameSlotInventory
    {
        internal const string ItemId = "powerblock";
        internal const int MaxCount = 10;

        public PowerBlocSlot(int width, int height) : base(width, height)
        {
            mayInventoryAddItemFunc =
                (item, inventory) => item.identifier == ItemId;

            maxSlotAmountItemFunc =
                (item, inventory, slot) => MaxCount;

            onSlotAddItemEndFunc += NormalizeChangedItem;
            onSlotChangeAmountItemFunc += NormalizeChangedItem;
        }

        public override SlotMarker? TryFindOneValidInventorySlot(GameItem item)
        {
            if (childItem == null || ReferenceEquals(childItem, item))
            {
                return base.TryFindOneValidInventorySlot(item);
            }

            SlotMarker slot = CreateMergeSlot(
                item,
                item.MaxNumRemove(),
                item.modifiedShape,
                item.modifiedState);

            return slot.IsValid() ? slot : null;
        }

        internal SlotMarker CreateMergeSlot(
            GameItem item,
            int requestedNum,
            GridShape shape,
            TagSystem state)
        {
            GameItem? target = childItem;

            SlotMarker slot = new SlotMarker
            {
                item = item,
                inventory = this,
                targetItem = target,
                itemGridShape = shape,
                index = 0
            };

            if (target == null ||
                ReferenceEquals(item, target) ||
                item.identifier != ItemId ||
                target.identifier != ItemId ||
                GeneralHelper.IsItemOwned(item) !=
                    GeneralHelper.IsItemOwned(target) ||
                !MayHaveValidInventorySlot(item))
            {
                return slot;
            }

            if (item.parentInventory != null && !item.MayRemove())
            {
                return slot;
            }

            if (!item.CheckIsStateValid(this, slot, shape, state) ||
                !CheckAcceptParentShapeStateModifier(
                    item, slot, shape, state))
            {
                return slot;
            }

            slot.numTransfer = Math.Max(
                0,
                Math.Min(
                    requestedNum,
                    Math.Min(item.MaxNumRemove(), target.MaxNumAdd())));

            slot.priority = Math.Max(
                slotPriorityFunc?.Invoke(item, this, slot) ?? 0,
                item.slotPriorityFunc?.Invoke(item, this, slot) ?? 0);

            return slot;
        }

        internal bool CanMerge(GameItem item)
        {
            return CreateMergeSlot(
                item,
                item.MaxNumRemove(),
                item.modifiedShape,
                item.modifiedState).IsValid();
        }

        private static void NormalizeChangedItem(
            GameItem item,
            GameInventory inventory,
            SlotMarker slot)
        {
            // 读档时恢复保存状态，不在恢复途中重新基础化。
            if (SaveManager.IsDecoding || item.unitCount <= 0)
            {
                return;
            }

            bool owned = GeneralHelper.IsItemOwned(item);

            // 直接调用当前目录工厂，取得基础模板。
            // 不通过 DirectoryMaster.Item 分配额外的游戏实例编号。
            GameItem basic =
                DirectoryMaster.GetFactory<GameItem>(ItemId)();

            try
            {
                // 当前原版 PowerBloc 没有基础 ItemFeature。
                while (item.itemFeatures.Count > 0)
                {
                    item.RemoveItemFeature(item.itemFeatures[0]);
                }

                item.state = basic.state.Clone();
                item.modifiedState = basic.modifiedState.Clone();
                item.itemTypes = new List<string>(basic.itemTypes);

                item.unitValue = basic.unitValue;
                item.unitBaseValue = basic.unitBaseValue;
                item.lateUnitValue = basic.lateUnitValue;
                item.backupUnitValue = basic.backupUnitValue;

                item.name = basic.name;
                item.shortDescription = basic.shortDescription;
                item.longDescription = basic.longDescription;
                item.flavorText = basic.flavorText;
                item.customText = basic.customText;
                item.useSpriteFromMod = basic.useSpriteFromMod;

                // 保留交易归属；不改变数量、实例 ID、父库存和回调。
                GeneralHelper.SetItemOwned(item, owned);

                item.SetShape(basic.shape);
                item.SetSprite(basic.spriteAtlasPath, basic.spritePath);
                item.ValidateShapeState();
                item.Validate();
            }
            finally
            {
                basic.Destroy();
            }
        }
    }

    [HarmonyPatch(typeof(GameItem), nameof(GameItem.MayStack))]
    internal static class PowerBlocMayStackPatch
    {
        private static void Postfix(
            GameItem __instance,
            GameItem otherItem,
            ref bool __result)
        {
            if (__instance.parentInventory is PowerBlocSlot slot &&
                __instance.identifier == PowerBlocSlot.ItemId &&
                otherItem != null &&
                otherItem.identifier == PowerBlocSlot.ItemId)
            {
                __result = slot.CanMerge(otherItem);
            }
        }
    }

    [HarmonyPatch(
        typeof(GameSlotInventory),
        nameof(GameSlotInventory.TryInventorySlot),
        new Type[]
        {
            typeof(GameItem),
            typeof(int),
            typeof(Vector2),
            typeof(GridShape),
            typeof(TagSystem)
        })]
    internal static class PowerBlocDropPatch
    {
        private static void Postfix(
            GameSlotInventory __instance,
            GameItem item,
            int requestedNum,
            Vector2 itemGraphicsCenterPosition,
            GridShape tempShape,
            TagSystem tempState,
            TreeNodeRender ___background,
            ref SlotMarker __result)
        {
            if (!(__instance is PowerBlocSlot slot) ||
                slot.childItem == null ||
                item == null ||
                ReferenceEquals(slot.childItem, item))
            {
                return;
            }

            // 保留原版对拖入位置的检查。
            if (float.IsFinite(itemGraphicsCenterPosition.x) &&
                !___background.rectTransform.rect.Contains(
                    (Vector2)slot.rectTransform.InverseTransformPoint(
                        itemGraphicsCenterPosition)))
            {
                return;
            }

            __result = slot.CreateMergeSlot(
                item,
                requestedNum,
                tempShape ?? item.modifiedShape,
                tempState ?? item.modifiedState);
        }
    }

    [HarmonyPatch(typeof(GameItem), nameof(GameItem.GetSplitItem))]
    internal static class PowerBlocSplitPatch
    {
        private static void Postfix(
            GameItem __instance,
            GameItem __result)
        {
            if (__instance.identifier == PowerBlocSlot.ItemId &&
                __result != null &&
                __result.identifier == PowerBlocSlot.ItemId)
            {
                GeneralHelper.SetItemOwned(
                    __result,
                    GeneralHelper.IsItemOwned(__instance));
            }
        }
    }
}
