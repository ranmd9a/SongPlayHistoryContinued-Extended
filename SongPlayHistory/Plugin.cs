﻿using System;
using System.Collections.Generic;
using System.Reflection;
using BeatSaberMarkupLanguage.Settings;
using BS_Utils.Gameplay;
using HarmonyLib;
using IPA;
using IPA.Config.Stores;
using IPA.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
using BS_Utils.Utilities;
using Config = IPA.Config.Config;

namespace SongPlayHistoryContinued
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        public const string HarmonyId = "com.github.swift-kim.SongPlayHistory";

        public static Plugin Instance { get; private set; }
        public static Logger Log { get; internal set; }

        private readonly Harmony _harmony;
        private bool _isPractice;

        [Init]
        public Plugin(Logger logger, Config config)
        {
            Instance = this;
            Log = logger;
            _harmony = new Harmony(HarmonyId);

            PluginConfig.Instance = config.Generated<PluginConfig>();
            BSMLSettings.instance.AddSettingsMenu("Song Play History", $"SongPlayHistoryContinued.Settings.bsml", SettingsController.instance);

            SPHModel.InitializeRecords();
        }

        [OnStart]
        public void OnStart()
        {
            BSEvents.gameSceneLoaded += OnGameSceneLoaded;
            BSEvents.LevelFinished += OnLevelFinished;

            // Init after the menu scene is loaded.
            BSEvents.lateMenuSceneLoadedFresh += (o) =>
            {
                Log.Info("The menu scene was loaded.");
                _ = new UnityEngine.GameObject(nameof(SPHController)).AddComponent<SPHController>();
            };

            ApplyHarmonyPatches(PluginConfig.Instance.ShowVotes);
        }

        [OnExit]
        public void OnExit()
        {
            BSEvents.gameSceneLoaded -= OnGameSceneLoaded;
            BSEvents.LevelFinished -= OnLevelFinished;

            SPHModel.BackupRecords();
        }

        private void OnGameSceneLoaded()
        {
            var practiceSettings = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData?.practiceSettings;
            _isPractice = practiceSettings != null;
        }

        private void OnLevelFinished(object scene, LevelFinishedEventArgs eventArgs)
        {
            if (eventArgs.LevelType != LevelType.Multiplayer && eventArgs.LevelType != LevelType.SoloParty)
            {
                return;
            }

            var result = ((LevelFinishedWithResultsEventArgs)eventArgs).CompletionResults;
            
            if (eventArgs.LevelType == LevelType.Multiplayer)
            {
                var beatmap = ((MultiplayerLevelScenesTransitionSetupDataSO)scene)?.difficultyBeatmap;
                SaveRecord(beatmap, result, true);
            }
            else
            {
                // solo
                if (_isPractice || Gamemode.IsPartyActive)
                {
                    return;
                }
                var beatmap = ((StandardLevelScenesTransitionSetupDataSO)scene)?.difficultyBeatmap;
                SaveRecord(beatmap, result, false);
            }
            
        }

        private void SaveRecord(IDifficultyBeatmap beatmap, LevelCompletionResults result, bool isMultiplayer)
        {
            if (result?.multipliedScore > 0)
            {
                // Actually there's no way to know if any custom modifier was applied if the user failed a map.
                var submissionDisabled = ScoreSubmission.WasDisabled || ScoreSubmission.Disabled || ScoreSubmission.ProlongedDisabled;
                SPHModel.SaveRecord(beatmap, result, submissionDisabled, isMultiplayer);
            }
        }

        public void ApplyHarmonyPatches(bool enabled)
        {
            try
            {
                if (enabled && !Harmony.HasAnyPatches(HarmonyId))
                {
                    Log.Info("Applying Harmony patches...");
                    _harmony.PatchAll(Assembly.GetExecutingAssembly());
                }
                else if (!enabled && Harmony.HasAnyPatches(HarmonyId))
                {
                    Log.Info("Removing Harmony patches...");
                    _harmony.UnpatchSelf();

                    SetDataFromLevelAsync.OnUnpatch();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error while applying Harmony patches.\n" + ex.ToString());
            }
        }
    }
}
