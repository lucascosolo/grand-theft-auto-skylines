# CS1 API notes

One row per CS1 or Unity member the mod uses. "Read at" is the decompile file and line read before use
(`~/.cache/minecraft-skylines/decompile/`, Assembly-CSharp / ColossalManaged / ICities, one `.cs` per type; local only).
"Compiles" = the mod builds against the owner's assemblies (`~/.cache/gta-skylines/refs/cs1/Managed/`). "Seen in game" is
only ever set from an owner run. Rows marked *ref* were verified by the reference project (`minecraft-skylines/docs/CS1-API-NOTES.md`)
and are used here through `Skylines.Host`; the reference recorded none of them as seen in game.

## GtaSkylines.Mod (M1)

| Area | Member | Read at | Compiles | Seen in game |
|---|---|---|---|---|
| Mod entry | `ICities.IUserMod { string Name; string Description; }` | ICities/IUserMod.cs:3-7 | yes | no |
| Mod entry | public `OnEnabled()` / `OnDisabled()` found by reflection | *ref* (PluginManager) | yes | no |
| Options page | public `OnSettingsUI(UIHelperBase)` found by reflection; called with `new UIHelper(<OptionsScrollPanelTemplate>.Find("ScrollContent"))` | Assembly-CSharp/OptionsMainPanel.cs:163-169 | yes | no |
| Options page | `UIHelperBase.AddGroup(string)`, `AddButton(string, OnButtonClicked)`, `AddCheckbox(string, bool, OnCheckChanged)`, `AddSlider(string, float, float, float, float, OnValueChanged)`, `AddDropdown(string, string[], int, OnDropdownSelectionChanged)` | ICities/UIHelperBase.cs:5-15; impl Assembly-CSharp/UIHelper.cs:28-150 (returns `UICheckBox`, `UISlider`, `UIDropDown`, `UIButton`; null callback or empty text returns null) | yes | no |
| Options page | `UISlider` lives in a template panel with a child `UILabel` "Label" (`UIHelper.cs:49-51`); `UIComponent.parent` (UIComponent.cs:870), `UIComponent.Find<T>(string)` (:1268), `UITextComponent.text` (UITextComponent.cs:108; `UIButton : UIInteractiveComponent : UITextComponent`, UIButton.cs:7, UIInteractiveComponent.cs:6) | as listed | yes | no |
| Options page | Unity `MonoBehaviour` added to the first key button's `gameObject` (`Component.gameObject.AddComponent<T>()`), `Time.frameCount`, `Input.GetKeyDown(KeyCode)`, `Input.mouseScrollDelta`, `Enum.Parse(typeof(KeyCode), name)` | UnityEngine 5.6 (compiled) | yes | no |
| Settings file | `ColossalFramework.IO.DataLocation.localApplicationData` (static string) → `<that>/GtaSkylines/settings.cfg` | ColossalManaged/ColossalFramework.IO/DataLocation.cs:236 | yes | no |
| Log | `Skylines.Host.HostLog("GtaSkylines")` → `<localApplicationData>/ModLogs/GtaSkylines.log` | *ref* | yes | no |
| City lifecycle | `LoadingExtensionBase.OnLevelLoaded(LoadMode)`, `OnLevelUnloading()`; `LoadMode.NewGame/LoadGame/NewGameFromScenario` | ICities/LoadingExtensionBase.cs:16-22, LoadMode.cs:3-19 | yes | no |
| Save data | `SerializableDataExtensionBase.OnLoadData/OnSaveData`, `serializableDataManager`; `ISerializableData.LoadData(string)` / `SaveData(string, byte[])` | ICities/SerializableDataExtensionBase.cs:3-25, ISerializableData.cs:3-17 | yes | no |
| Save data | `LoadData` returns null for a missing key; `OnSaveData` runs from `SimulationManager.Data.Serialize` (not the main thread), hence `ProfileStore` is locked and fed from the main thread | Assembly-CSharp/SerializableDataWrapper.cs:96-129, :168-180; SimulationManager.cs:59-63 | yes | no |
| Enter key | `UIView.HasModalInput()`, `UIView.HasInputFocus()` (static) | ColossalManaged/ColossalFramework.UI/UIView.cs:801, :1957 | yes | no |
| Terrain pick | `TerrainManager.RayCast(Segment3 ray, out Vector3 hit)` (instance; clips to the 17280 m map box, 16 m raw cells) via `Singleton<TerrainManager>.instance`; `ColossalFramework.Math.Segment3(Vector3, Vector3)`; Unity `Camera.main`, `ScreenPointToRay`, `farClipPlane`, `Input.mousePosition`, `transform.eulerAngles` | Assembly-CSharp/TerrainManager.cs:1065; ColossalManaged/ColossalFramework.Math/Segment3.cs:5; ColossalManaged/ColossalFramework/Singleton.cs:9, :28 | yes | no |
| Unpause gate | `SimulationManager.SimulationPaused` (get/set) | Assembly-CSharp/SimulationManager.cs:412 | yes | no |
| Pause menu | `SimulationManager.ForcedSimulationPaused` (get/set), as CS1's own pause menu uses it | SimulationManager.cs:435; PauseMenu.cs:261, :276 | yes | no |
| Sim rate | `Time.fixedDeltaTime`; `SimulationManager.m_referenceFrameIndex` (uint), `m_dayTimeOffsetFrames` (uint), `m_timeOffsetTicks` (long), `m_timePerFrame` (TimeSpan) | SimulationManager.cs:316, :352, :349, :346 (reference: :664-674 derive time from them) | yes | no |
| Render | `RenderManager.CameraHeight` (get/set), `QualitySettings.shadowDistance` | Assembly-CSharp/RenderManager.cs:299; *ref* | yes | no |
| Camera | `CameraTakeover` (CameraController fields, `CinematicCameraController`, TiltShift/DepthOfField, `Camera.rect`) | *ref* | yes | no |
| Shortcuts and UI hiding | `ShortcutBlocker` (`UIView.PushModal/PopModal/SetFocus`, `UIView.uiCamera.enabled` off while playing); Esc/P never reach `GameKeyShortcuts` while the modal has focus, so the pause key goes to our menu | *ref* (UI hiding without UIView.Show; Shortcut blocking rows) | yes | no |
| Cursor and mouse | `InputCapture` (`Cursor.lockState/visible`, `Input.GetAxisRaw("Mouse X"/"Mouse Y")`, `Application.isFocused`); `Input.GetKey(KeyCode)`, `Screen.height` | *ref* (Raw input row) | yes | no |
| Walk-in button | `MapDropButton` / `MapDropTool` (custom `ToolBase`, `ToolBase.RayCast`, `OverlayEffect.DrawCircle`, `UIView.Find("BulldozerButton")`, sprite `InfoIconPopulation`) | *ref* (Walk-in drop button section) | yes | no |
| Backup | `CityBackup` (`SavePanel.SaveGame(string)`, `SavePanel.isSaving`, `continueSaveDataLocation`), `GameDialogs.Confirm` (`ConfirmPanel.ShowModal`, result 1 = Yes), `GameDialogs.Error` (`ExceptionPanel`) | *ref* (City backup and pairing section) | yes | no |
| City name | `SimulationManager.m_metaData.m_CityName` via `CityState.Capture()` | SimulationManager.cs:295; *ref* | yes | no |
| Pause box | Unity IMGUI `GUI.Box`, `GUI.Button(Rect, string)`, `Screen.width/height` from `MonoBehaviour.OnGUI` | UnityEngine 5.6 (compiled) | yes | no |
| Free camera | `Quaternion.Euler`, `Mathf.Repeat/Clamp`, `CameraTakeover.Drive` | UnityEngine 5.6 (compiled) | yes | no |

## GtaSkylines.Mod (M5 audio)

Unity rows cite `ilspycmd -t <type>` of `~/.cache/gta-skylines/refs/cs1/Managed/UnityEngine.dll` (output in `~/.cache/scratch/unity56/`).

| Area | Member | Read at | Compiles | Seen in game |
|---|---|---|---|---|
| Clips | `AudioClip.Create(string name, int lengthSamples, int channels, int frequency, bool stream)` (static) | UnityEngine.AudioClip.decompiled.cs:115 | yes | no |
| Clips | `AudioClip.SetData(float[] data, int offsetSamples)` returns bool | UnityEngine.AudioClip.decompiled.cs:95 | yes | no |
| Sources | `AudioSource.volume` :12, `pitch` :22, `clip` :54, `isPlaying` :74, `loop` :88, `playOnAwake` :108, `spatialBlend` :148, `dopplerLevel` :218, `minDistance` :258, `maxDistance` :268, `rolloffMode` :278, `Play()` :326, `Stop()` :350, `Pause()` :352, `UnPause()` :361 | UnityEngine.AudioSource.decompiled.cs (lines as listed) | yes | no |
| Sources | `AudioRolloffMode.Linear` | ilspycmd -t UnityEngine.AudioRolloffMode, line 6 | yes | no |
| Sources | `UnityEngine.Random.Range(float, float)`, `Mathf.Clamp/Clamp01`, `GameObject(string)`, `AddComponent<T>()`, `Transform.parent/position` | UnityEngine.Random :115; rest UnityEngine 5.6 (compiled) | yes | no |
| Listener | CS1 owns the only `AudioListener` ("Audio Listener", DontDestroyOnLoad), moved to the camera controller plus `m_listenerOffset` every `LateUpdate`; our sources need no listener of their own | Assembly-CSharp/AudioManager.cs:651-654, :870, :884-893 | n/a | no |
| Ducking | `AudioManager.MuteAll` (bool get/set) silences ambient (:995), effect group (:955), radio/broadcast fade (:1089, :1168), and vehicle/citizen/building groups (VehicleManager.cs:1581, CitizenManager.cs:1140, BuildingManager.cs:4777); CS1's pause menu sets/clears it (PauseMenu.cs:266, :281), as does ChirpOptionsPanel.cs:89/:104. Used by `AmbienceDucker`, previous value restored | AudioManager.cs:593-603 | yes | no |
| Ducking (rejected) | Ambient `AudioGroup` volume: `m_cachedVolume` is public but overwritten from the persisted `SavedFloat` every frame (AudioGroup.cs:450); writing the `SavedFloat` (AudioManager.cs:630) would change the player's saved game settings | AudioGroup.cs:37-56, :450 | n/a | no |
| Pause | `SimulationManager.SimulationPaused` :412, `ForcedSimulationPaused` :435 (read only); `Singleton<T>.exists` | SimulationManager.cs; ColossalManaged/ColossalFramework/Singleton.cs:28 | yes | no |
| CS1 engine | `VehicleInfo.m_effects` (`Effect[]`) :305, `VehicleInfo.Effect { m_effect, m_vehicleFlagsRequired, ... }` :239; `EffectInfo.PlayEffect(InstanceID, SpawnArea, Vector3 velocity, float acceleration, float magnitude, AudioManager.ListenerInfo, AudioGroup)` virtual :112; `EffectInfo.SpawnArea(Vector3 position, Vector3 direction, float radius)` :62; `EngineSoundEffect.PlayEffect` → `SoundEffect.PlaySound` → `AudioGroup.AddPlayer` | VehicleInfo.cs; EffectInfo.cs; EngineSoundEffect.cs:17-25; SoundEffect.cs:18-24; AudioGroup.cs:82 | yes | no |
| CS1 engine | `VehicleManager.m_audioGroup` (public) is updated in `PlayAudioImpl` from `AudioManager.LateUpdate`, so players added from our `Update` are played the same frame; pattern copied from `Vehicle.PlayAudio` | VehicleManager.cs:822, :1520-1582; Vehicle.cs:837-852; AudioManager.cs:870, :941 | yes | no |
| CS1 engine | `AudioManager.CurrentListenerInfo`; `InstanceID.Vehicle` setter (`0x2000000 | id`; id 0 is never a real vehicle) | AudioManager.cs:617; InstanceID.cs:109-120 | yes | no |
