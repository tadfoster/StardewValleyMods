using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
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
        public static IGenericModConfigMenuApi? _gmcmConfigMenu;

        private int DayCount;
        private bool DayLimitReached;
        private bool BreakModeActive;
        private int BreakModeMinuteCount;
        private int _breakModeMinuteCount;
        private bool PreventChanging;

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            initMod();

            _helper = helper;

            _helper.Events.GameLoop.SaveLoaded += SaveLoadedEvent;

            _helper.Events.GameLoop.DayStarted += DayStartedEvent;

            _helper.Events.GameLoop.DayEnding += DayEndingEvent;

            _helper.Events.GameLoop.ReturnedToTitle += ReturnedToTitleEvent;

            _helper.Events.Display.MenuChanged += MenuChangedEvent;

            _helper.Events.GameLoop.GameLaunched += GameLaunchedEvent;
        }

        private void SaveLoadedEvent(object? sender, SaveLoadedEventArgs e)
        {
            if (DateTime.UtcNow < _config.TakeBreakUntilTime)
            {
                BreakModeActive = true;
            }
            else
            {
                if (_config.TakeBreakUntilTime.HasValue)
                {
                    _config.TakeBreakUntilTime = null;

                    writeConfigFileGMCM();
                }
                addBreakModeOptionToConfigMenu();
            }
        }

        private void DayStartedEvent(object? sender, DayStartedEventArgs e)
        {
            if (BreakModeMinuteCount > 0)
            {
                _config.TakeBreakUntilTime = DateTime.UtcNow.AddMinutes(BreakModeMinuteCount);

                writeConfigFileGMCM();
            }

            if (BreakModeActive)
            {
                string onBreakMessage = formatBreakRemainingText(_helper.Translation.Get("Message_OnBreak"));

                Game1.drawObjectDialogue(onBreakMessage);
            }
            else if (_config.ModEnabled) 
            {
                if (DayCount >= _config.DayLimitCount)
                {
                    DayLimitReached = true;

                    Game1.drawObjectDialogue(_helper.Translation.Get("Message_ShutDown"));
                }
                else if (DayCount == (_config.DayLimitCount - 1))
                {
                    Game1.drawObjectDialogue(_helper.Translation.Get("Message_FinalDay"));
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
            initMod();

            _gmcmConfigMenu?.Unregister(ModManifest);

            registerGMCMConfigMenu();
        }

        private void MenuChangedEvent(object? sender, MenuChangedEventArgs e)
        {
            if ((DayLimitReached || BreakModeActive) && Game1.hasLoadedGame && Game1.activeClickableMenu == null)
            {
                if (BreakModeActive || _config.ExitToTitle)
                {
                    Game1.exitToTitle = true;
                }
                else
                {
                    Game1.quit = true;
                }
            }
        }

        private void GameLaunchedEvent(object? sender, GameLaunchedEventArgs e)
        {
            registerGMCMConfigMenu();
        }

        private void registerGMCMConfigMenu()
        {
            _gmcmConfigMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

            if (_gmcmConfigMenu == null) return;

            _gmcmConfigMenu.Register(
                mod: ModManifest,
                reset: () => {
                    _config = new ModConfig();
                },
                save: () => {
                    BreakModeMinuteCount = _breakModeMinuteCount;
                    writeConfigFileGMCM();

                    if (PreventChanging)
                    {
                        _gmcmConfigMenu.Unregister( mod: ModManifest );

                        registerGMCMConfigMenu();

                        addBreakModeOptionToConfigMenu();
                    }
                }
            );
            _gmcmConfigMenu.AddSectionTitle(
                mod: ModManifest,
                text: () => _helper.Translation.Get("GMCM_Option_DayLimit_SectionTitle"),
                tooltip: () => _helper.Translation.Get("GMCM_Option_DayLimit_Description")
            );

            _gmcmConfigMenu.AddParagraph(
                mod: ModManifest,
                text: () => Game1.hasLoadedGame && _config.ModEnabled ? $"{_helper.Translation.Get("GMCM_Option_DaysPlayed")}: {DayCount}    {_helper.Translation.Get("GMCM_Option_DaysRemaining")}: {Math.Max(_config.DayLimitCount - DayCount, 0)}" : ""
            );

            if (PreventChanging)
            {
                _gmcmConfigMenu.AddParagraph(
                    mod: ModManifest,
                    text: () => _helper.Translation.Get("GMCM_Option_PreventChanging_Warning")
                );
            }
            else
            {
                _gmcmConfigMenu.AddBoolOption(
                    mod: ModManifest,
                    name: () => _helper.Translation.Get("GMCM_Option_ModEnabled_Name"),
                    getValue: () => _config.ModEnabled,
                    setValue: value => setModEnabledConfig(value)
                );

                _gmcmConfigMenu.AddNumberOption(
                    mod: ModManifest,
                    name: () => _helper.Translation.Get("GMCM_Option_DayLimitCount_Name"),
                    getValue: () => _config.DayLimitCount,
                    setValue: value => _config.DayLimitCount = value,
                    min: 1,
                    max: 50
                );

                _gmcmConfigMenu.AddBoolOption(
                    mod: ModManifest,
                    name: () => _helper.Translation.Get("GMCM_Option_ExitToTitle_Name"),
                    getValue: () => _config.ExitToTitle,
                    setValue: value => _config.ExitToTitle = value
                );

                _gmcmConfigMenu.AddBoolOption(
                    mod: ModManifest,
                    name: () => _helper.Translation.Get("GMCM_Option_PreventChanging_Name"),
                    getValue: () => PreventChanging,
                    setValue: value => PreventChanging = value && _config.ModEnabled,
                    tooltip: () => _helper.Translation.Get("GMCM_Option_PreventChanging_Description")
                );
            }
            _gmcmConfigMenu.AddSectionTitle(
                mod: ModManifest,
                text: () => _helper.Translation.Get("GMCM_Option_BreakTime_SectionTitle"),
                tooltip: () => _helper.Translation.Get("GMCM_Option_BreakTime_Description")
            );

            _gmcmConfigMenu.AddParagraph(
                mod: ModManifest,
                text: () => (!Game1.hasLoadedGame ? formatBreakRemainingText(_helper.Translation.Get("GMCM_Option_BreakTime_Remaining")) : _helper.Translation.Get("GMCM_Option_BreakTime_Warning"))
            );
        }

        private void addBreakModeOptionToConfigMenu()
        {
            if (_gmcmConfigMenu != null && Game1.hasLoadedGame)
            {
                _gmcmConfigMenu.AddNumberOption(
                    mod: ModManifest,
                    name: () => _helper.Translation.Get("GMCM_Option_BreakTime_Name"),
                    getValue: () => {
                        _breakModeMinuteCount = BreakModeMinuteCount;
                        return _breakModeMinuteCount;
                    },
                    setValue: value => _breakModeMinuteCount = value,
                    min: 0,
                    max: 480
                );
            }
        }

        private void writeConfigFileGMCM()
        {
            ModConfig savedConfig = Helper.ReadConfig<ModConfig>();

            Helper.WriteConfig(new ModConfig() { ModEnabled = savedConfig.ModEnabled, DayLimitCount = _config.DayLimitCount, ExitToTitle = _config.ExitToTitle, TakeBreakUntilTime = _config.TakeBreakUntilTime });
        }

        private void initMod()
        {
            DayCount = 0;
            DayLimitReached = false;
            BreakModeActive = false;
            PreventChanging = false;
            BreakModeMinuteCount = 0;
            _breakModeMinuteCount = 0;

            _config = Helper.ReadConfig<ModConfig>();
        }

        private void setModEnabledConfig(bool value)
        {
            DayCount = _config.ModEnabled != value ? 0 : DayCount;

            _config.ModEnabled = value;
        }

        private string formatBreakRemainingText(string text)
        {
            if (_config.TakeBreakUntilTime.HasValue)
            {
                int minsRemaining = Convert.ToInt32(Math.Ceiling(_config.TakeBreakUntilTime.Value.Subtract(DateTime.UtcNow).TotalMinutes));

                if (minsRemaining > 0)
                {
                    return string.Format(text, minsRemaining);
                }
            }
            return _helper.Translation.Get("GMCM_Option_BreakTime_RemainingNone");
        }
    }
}