using System;
using UnityEngine;
using HarmonyLib;

namespace ProbablyCharged
{
    public sealed class ModEntry : IMod
    {
        private ModLog? logger;

        public void Init(ModManifest manifest)
        {
            logger = new ModLog(manifest);
            logger.Log($"Application version: {UnityEngine.Application.version}");
        }

        public void OnEnable()
        {
            logger?.Log("PowerCabinet Enabled");
        }

        public void OnDisable()
        {
            logger?.Log("PowerCabinet exiting");
        }
    }
}
