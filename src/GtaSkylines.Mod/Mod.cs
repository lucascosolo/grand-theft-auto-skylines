using System;
using GtaSkylines.Mod.Lifecycle;
using GtaSkylines.Mod.Options;
using GtaSkylines.Mod.Persistence;
using ICities;

namespace GtaSkylines.Mod
{
    /// <summary>Entry point CS1 discovers in the mod folder.</summary>
    public sealed class Mod : IUserMod
    {
        /// <inheritdoc />
        public string Name { get { return "GTA: Skylines"; } }

        /// <inheritdoc />
        public string Description
        {
            get { return "Step into your city as a GTA-style character. Drag the walk-in figure onto the map, or press Ctrl+Shift+G over a spot."; }
        }

        /// <summary>Called by CS1 (reflection) when the mod is enabled.</summary>
        public void OnEnabled()
        {
            ModRuntime.Guard("OnEnabled", () => ModRuntime.Log.Info("enabled, settings in " + ModRuntime.SettingsPath));
            ModRuntime.Guard("OnEnabled self-test", SelfTest.SelfTestHost.Start);
        }

        /// <summary>Called by CS1 (reflection) when the mod is disabled: leaves play mode and removes everything.</summary>
        public void OnDisabled()
        {
            ModRuntime.Guard("OnDisabled self-test", SelfTest.SelfTestHost.Stop);
            ModRuntime.Guard("OnDisabled", PlayController.DestroyCurrent);
            ModRuntime.Guard("OnDisabled settings", () => ModRuntime.FlushSettings(true));
        }

        /// <summary>Builds the options page (CS1 calls it by reflection from OptionsMainPanel).</summary>
        public void OnSettingsUI(UIHelperBase helper)
        {
            ModRuntime.Guard("OnSettingsUI", () => OptionsPage.Build(helper));
        }
    }

    /// <summary>Creates the controller when a city is loaded and removes it on unload.</summary>
    public sealed class Loading : LoadingExtensionBase
    {
        /// <inheritdoc />
        public override void OnLevelLoaded(LoadMode mode)
        {
            ModRuntime.Guard("OnLevelLoaded", () =>
            {
                if (mode != LoadMode.NewGame && mode != LoadMode.LoadGame && mode != LoadMode.NewGameFromScenario) return;
                PlayController.Create();
            });
        }

        /// <inheritdoc />
        public override void OnLevelUnloading()
        {
            ModRuntime.Guard("OnLevelUnloading settings", () => ModRuntime.FlushSettings(true));
            ModRuntime.Guard("OnLevelUnloading", () =>
            {
                PlayController.DestroyCurrent();
                ModRuntime.Profiles.Reset();
            });
        }
    }

    /// <summary>The profile record in the city save (<see cref="ProfileStore"/>).</summary>
    public sealed class SaveData : SerializableDataExtensionBase
    {
        /// <inheritdoc />
        public override void OnLoadData()
        {
            ModRuntime.Guard("OnLoadData", () =>
            {
                ISerializableData d = serializableDataManager;
                ProfileStore store = ModRuntime.Profiles;
                store.Load(d.LoadData(ProfileStore.Key), d.LoadData(ProfileStore.UnreadableKey));
                ModRuntime.Log.Info("save data: " + (store.Record != null ? "profile " + store.Record.Length + " bytes" : "no profile")
                    + (store.Unreadable != null ? ", unreadable record kept" : ""));
            });
        }

        /// <inheritdoc />
        public override void OnSaveData()
        {
            ModRuntime.Guard("OnSaveData", () =>
            {
                ISerializableData d = serializableDataManager;
                ProfileStore store = ModRuntime.Profiles;
                if (!store.MayWrite)
                {
                    ModRuntime.Log.Info("save data: not written (play mode never entered in this city)");
                    return;
                }
                store.Save((key, bytes) => d.SaveData(key, bytes));
                ModRuntime.Log.Info("save data: profile written");
            });
        }
    }
}
