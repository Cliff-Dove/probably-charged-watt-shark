using System.Collections;
using UnityEngine;
using TMPro;
using QFSW.QC;

namespace ProbablyCharged
{
    public sealed class ModEntry : IMod
    {
        public ModLog? logger;
        public string modId = null!;
        private Coroutine? consoleRoutine;

        private QuantumConsole? console;

        private TMP_FontAsset? originalFont;

        private const string PREFIX = "probably_charged";

        public void Init(ModManifest manifest)
        {
            modId = manifest.ID;
            logger = new ModLog(manifest);
            logger.Log($"Application version: {UnityEngine.Application.version}");
        }

        public void OnEnable()
        {
            logger?.Log($"[{modId}] Enabled");
            consoleRoutine = ModHelper.StartCoroutine(modId, ConfigureConsole());
            ModHook.OnModItemDirectoryInit += RegisterItems;
        }

        public void OnDisable()
        {
            ModHook.OnModItemDirectoryInit -= RegisterItems;
            ConsoleRestore();
            logger?.Log($"[{modId}] exiting");
        }

        private void RegisterItems(ModItemDirectory dir)
        {
            dir.Add($"{PREFIX}_watt_shark", () => WattShark.SharkDrop(modId));
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
