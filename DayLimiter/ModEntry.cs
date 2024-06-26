﻿using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using GMCMOptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace DayLimiter
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        public static ModConfig _config;
        public static IModHelper _helper;
        public static int DayCount;

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            DayCount = 0;

            _helper = helper;

            _config = Helper.ReadConfig<ModConfig>();

            _helper.Events.GameLoop.DayStarted += DayStartedEvent;

            _helper.Events.GameLoop.DayEnding += DayEndingEvent;

            _helper.Events.GameLoop.ReturnedToTitle += ReturnedToTitleEvent;

            _helper.Events.GameLoop.GameLaunched += GameLaunchedEvent;
        }

        private void DayStartedEvent(object? sender, DayStartedEventArgs e)
        {
            if (_config.ModEnabled && DayCount >= _config.DayLimitCount) 
            {
                if (DayCount == _config.DayLimitCount)
                {
                    Game1.drawObjectDialogue(_helper.Translation.Get("Message_FinalDay"));
                }
                else if (_config.ExitToTitle)
                {
                    Game1.exitToTitle = true;
                }
                else
                {
                    Game1.quit = true;
                }
            }
        }

        private void DayEndingEvent(object? sender, DayEndingEventArgs e)
        {
            if (_config.ModEnabled)
            {
                DayCount++;
            }
        }

        private void ReturnedToTitleEvent(object? sender, ReturnedToTitleEventArgs e)
        {
            DayCount = 0;

            _config = Helper.ReadConfig<ModConfig>();
        }

        private void GameLaunchedEvent(object? sender, GameLaunchedEventArgs e)
        {
            IGenericModConfigMenuApi? configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

            if (configMenu != null)
            {
                configMenu.Register(
                    mod: ModManifest,
                    reset: () => {
                        _config = new ModConfig();
                    },
                    save: () => {
                        ModConfig savedConfig = Helper.ReadConfig<ModConfig>();

                        Helper.WriteConfig(new ModConfig() { ModEnabled = savedConfig.ModEnabled, DayLimitCount = _config.DayLimitCount, ExitToTitle = _config.ExitToTitle });
                    }
                );

                IGMCMOptionsAPI? configMenuExt = Helper.ModRegistry.GetApi<IGMCMOptionsAPI>("jltaylor-us.GMCMOptions");

                if (configMenuExt != null)
                {
                    string daysPlayedStr = _helper.Translation.Get("GMCM_Option_DaysPlayed");
                    string daysRemainingStr = _helper.Translation.Get("GMCM_Option_DaysRemaining");

                    configMenuExt.AddDynamicParagraph(
                        mod: ModManifest,
                        logName: "dayCounter",
                        text: () => Game1.hasLoadedGame && _config.ModEnabled ? $"{daysPlayedStr}: {DayCount}    {daysRemainingStr}: {Math.Max(_config.DayLimitCount - DayCount, 0)}" : "",
                        isStyledText: true
                    );
                }

                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: () => _helper.Translation.Get("GMCM_Option_ModEnabled_Name"),
                    getValue: () => _config.ModEnabled,
                    setValue: value => setModEnabledConfig(value),
                    fieldId: "DayLimitEnabled"
                );

                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: () => _helper.Translation.Get("GMCM_Option_DayLimitCount_Name"),
                    getValue: () => _config.DayLimitCount,
                    setValue: value => _config.DayLimitCount = value,
                    min: 1,
                    max: 100,
                    fieldId: "DayLimitCount"
                );

                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: () => _helper.Translation.Get("GMCM_Option_ExitToTitle_Name"),
                    getValue: () => _config.ExitToTitle,
                    setValue: value => _config.ExitToTitle = value
                );
            }
        }

        private void setModEnabledConfig(bool value)
        {
            DayCount = _config.ModEnabled != value ? 0 : DayCount;

            _config.ModEnabled = value;
        }
    }
}