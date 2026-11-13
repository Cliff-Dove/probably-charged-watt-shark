using System.Collections;
using System.Collections.Generic;
using System.Linq;
using QFSW.QC;
using TMPro;
using UnityEngine;

namespace ProbablyCharged
{
    public sealed class ModEntry : IMod
    {
        // Init 先于启用和游戏钩子执行，其他模组类共享此日志实例。
        internal static ModLog Logger { get; private set; } = null!;

        private string _modId = null!;
        private Coroutine? _consoleFontRoutine;
        private QuantumConsole? _console;
        private TMP_FontAsset? _originalConsoleFont;

        public void Init(ModManifest manifest)
        {
            _modId = manifest.ID;
            Logger = new ModLog(manifest);
            Logger.Log(
                $"[ModEntry] Initialized: modId={_modId}, gameVersion={Application.version}");
        }

        public void OnEnable()
        {
            _consoleFontRoutine = ModHelper.StartCoroutine(_modId, ConfigureConsoleFont());
            ModHook.OnModItemDirectoryInit += RegisterItems;
            ModHook.OnPlaceInventorInventoryItemEarly += OfferWattSharkIfMissing;

            // 先让原版阵列和机器结算，再根据实际剩余电量执行柜内充电。
            ModHook.OnGoingSleepLate += ChargeWattSharksAtNight;
            Logger.Log("[ModEntry] Enabled");
        }

        public void OnDisable()
        {
            ModHook.OnGoingSleepLate -= ChargeWattSharksAtNight;
            ModHook.OnPlaceInventorInventoryItemEarly -= OfferWattSharkIfMissing;
            ModHook.OnModItemDirectoryInit -= RegisterItems;
            RestoreConsoleFont();
            Logger.Log("[ModEntry] Disabled");
        }

        private void RegisterItems(ModItemDirectory directory)
        {
            bool registered = directory.Add(
                WattShark.ItemId, () => WattShark.Create(_modId));

            if (registered)
            {
                Logger.Log($"[ModEntry] ItemRegistered: itemId={WattShark.ItemId}");
            }
            else
            {
                Logger.Error($"[ModEntry] ItemRegistrationFailed: itemId={WattShark.ItemId}");
            }
        }

        private void OfferWattSharkIfMissing(List<GameItem> playerItems)
        {
            if (ExistsInStoreOrSoldInventory(WattShark.ItemId))
            {
                Logger.Log(
                    $"[ModEntry] OfferSkipped: itemId={WattShark.ItemId}, reason=AlreadyPresent");
                return;
            }

            // 此接口与原上柜方法使用相同流程，并返回真实的放置结果。
            bool placed = PlayerStore.Instance.TryAddDirectSellingItemToTable(WattShark.ItemId);
            if (placed)
            {
                Logger.Log($"[ModEntry] ItemOffered: itemId={WattShark.ItemId}");
            }
            else
            {
                Logger.Warning($"[ModEntry] ItemOfferFailed: itemId={WattShark.ItemId}");
            }
        }

        private void ChargeWattSharksAtNight()
        {
            if (!PlayerStore.Instance.isPowerOn)
            {
                Logger.Log("[ModEntry] NightChargeSkipped: reason=PowerOff");
                return;
            }

            int cabinetCount = 0;
            foreach (GameItem item in PlayerStore.Instance.FindAllItem())
            {
                if (item.identifier == WattShark.ItemId)
                {
                    cabinetCount++;
                    WattShark.ChargeAtNight(item);
                }
            }

            Logger.Log($"[ModEntry] NightChargeProcessed: cabinetCount={cabinetCount}");
        }

        private static bool ExistsInStoreOrSoldInventory(string itemId)
        {
            // 保留现有检查范围；这并不是覆盖所有存档位置的全局查询。
            bool existsInStore = PlayerStore.Instance.FindAllItem(false)
                .Any(item => item.identifier == itemId);
            bool existsInSold = EmporiumEntry.Instance.soldElement.childItems
                .Any(item => item.identifier == itemId);

            Logger.Log(
                $"[ModEntry] InventoryChecked: itemId={itemId}, " +
                $"inStore={existsInStore}, inSold={existsInSold}");
            return existsInStore || existsInSold;
        }

        private IEnumerator ConfigureConsoleFont()
        {
            while (QuantumConsole.Instance == null ||
                FontLoader.Instance == null ||
                FontLoader.Instance.simplifiedChineseFont == null)
            {
                yield return null;
            }

            _console = QuantumConsole.Instance;
            _originalConsoleFont = _console.Theme.Font;
            _console.Theme.Font = FontLoader.Instance.simplifiedChineseFont;
            _console.ApplyTheme(_console.Theme, true);

            Logger.Log($"[ModEntry] ConsoleFontApplied: font={_console.Theme.Font.name}");
        }

        private void RestoreConsoleFont()
        {
            if (_consoleFontRoutine != null)
            {
                ModHelper.StopCoroutine(_modId, _consoleFontRoutine);
            }

            if (_console != null && _originalConsoleFont != null)
            {
                _console.Theme.Font = _originalConsoleFont;
                _console.ApplyTheme(_console.Theme, true);
                Logger.Log($"[ModEntry] ConsoleFontRestored: font={_console.Theme.Font.name}");
            }
        }
    }
}
