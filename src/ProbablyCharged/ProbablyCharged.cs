using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using QFSW.QC;

namespace ProbablyCharged
{
    public sealed class ModEntry : IMod
    {
        internal static ModLog logger { get; private set; } = null!;
        public string modId = null!;
        private Coroutine? consoleRoutine;

        private QuantumConsole? console;

        private TMP_FontAsset? originalFont;

        private const string PREFIX = "probably_charged";

        private const string WATT_SHARK_ID = $"{PREFIX}_watt_shark";

        public void Init(ModManifest manifest)
        {
            modId = manifest.ID;
            logger = new ModLog(manifest);
            logger.Log($"Application version: {UnityEngine.Application.version}");
        }

        public void OnEnable()
        {
            logger?.Log($"Mod Enabled");
            consoleRoutine = ModHelper.StartCoroutine(modId, ConfigureConsole());
            ModHook.OnModItemDirectoryInit += RegisterItems;
            //瓦鲨获取渠道
            ModHook.OnPlaceInventorInventoryItemEarly += OfferWattShark;
            //夜间充电
            ModHook.OnGoingSleepLate += ChargeWattSharks;
        }

        public void OnDisable()
        {
            ModHook.OnGoingSleepLate -= ChargeWattSharks;
            ModHook.OnPlaceInventorInventoryItemEarly -= OfferWattShark;
            ModHook.OnModItemDirectoryInit -= RegisterItems;
            ConsoleRestore();
            logger?.Log($"Mod exiting");
        }

        private void RegisterItems(ModItemDirectory dir)
        {
            dir.Add(WATT_SHARK_ID, () => WattShark.SharkDrop(modId));
        }

        private void OfferWattShark(List<GameItem> playerItems)
        {
            logger?.Log($"Checking if `WattShark` should be offered to Inventor store.");
            // 检查是否应该提供瓦鲨
            if (!IsGlobalExist(WATT_SHARK_ID))
            {
                logger?.Log($"Trying to offer `WattShark` to Inventor store.");
                PlayerStore.Instance.AddDirectSellingItemToTable(WATT_SHARK_ID);
                logger?.Log("Offer `WattShark` to Inventor store.");
            }
        }

        private void ChargeWattSharks()
        {
            if (!PlayerStore.Instance.isPowerOn)
            {
                return;
            }

            foreach (GameItem item in PlayerStore.Instance.FindAllItem())
            {
                if (item.identifier == WATT_SHARK_ID)
                {
                    WattShark.ChargeAtNight(item);
                }
            }
        }
        private bool IsGlobalExist(string itemId)
        {
            //检查用户商店
            bool existsInStore = PlayerStore.Instance.FindAllItem(false).Any(item => item.identifier == itemId);
            bool existsInSold = EmporiumEntry.Instance.soldElement.childItems.Any(item => item.identifier == itemId);
            logger?.Log($"Checking global existence: `{itemId}` ———— {existsInStore || existsInSold}, exists in store: {existsInStore}, exists in sold: {existsInSold}");
            return existsInStore || existsInSold;
        }
        private IEnumerator ConfigureConsole()
        {
            while (QuantumConsole.Instance == null || FontLoader.Instance == null
            || FontLoader.Instance.simplifiedChineseFont == null)
            {
                yield return null;
            }
            console = QuantumConsole.Instance;
            originalFont = console.Theme.Font;
            console.Theme.Font = FontLoader.Instance.simplifiedChineseFont;
            console.ApplyTheme(console.Theme, true);

            logger?.Log($"控制台中文字体已应用:{console.Theme.Font.name}");
        }

        private void ConsoleRestore()
        {
            if (consoleRoutine != null)
            {
                ModHelper.StopCoroutine(modId, consoleRoutine);
            }

            if (console != null && originalFont != null)
            {
                console.Theme.Font = originalFont;
                console.ApplyTheme(console.Theme, true);
                logger?.Log($"console font restored:{console.Theme.Font.name}");
            }
        }
    }
}
