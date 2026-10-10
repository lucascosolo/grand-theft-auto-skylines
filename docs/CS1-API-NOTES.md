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
| Pause menu | `SimulationManager.ForcedSimulationPaused` (get/set; plain field write, :435-445), written on the simulation thread through `AddAction` exactly as CS1's own pause menu does (PauseMenu.cs:257-262, :272-277; also ChirpOptionsPanel.cs:82-85, BailoutPanel.cs:39-41, UnlockingPanel.cs:1478-1480); `PlayController` queues two cached delegates, and the previous value is read in the queued action | SimulationManager.cs:435, :725-745 | yes | no |
| Sim rate | `Time.fixedDeltaTime`; `SimulationManager.m_referenceFrameIndex` (uint, read on the main thread where `Update` advances it, :662-663), `m_dayTimeOffsetFrames` (uint), `m_timeOffsetTicks` (long, not atomic on Mono), `m_timePerFrame` (TimeSpan); the offsets are written only on the simulation thread through `AddAction(Action)` (queue drained at the start of every `SimulationStep`, :869-889; CS1 writes them there too, :923-928), batched by `SimClockDebt` (8 frames of 65536 per day) with one outstanding cached delegate | SimulationManager.cs:316, :352, :349, :346, :725-745 (reference: :664-674 derive time from them) | yes | no |
| Render | `RenderManager.CameraHeight` (get/set), `QualitySettings.shadowDistance` | Assembly-CSharp/RenderManager.cs:299; *ref* | yes | no |
| Camera | `CameraTakeover` (CameraController fields, `CinematicCameraController`, TiltShift/DepthOfField, `Camera.rect`) | *ref* | yes | no |
| Shortcuts and UI hiding | `ShortcutBlocker` (`UIView.PushModal/PopModal/SetFocus`, `UIView.uiCamera.enabled` off while playing); Esc/P never reach `GameKeyShortcuts` while the modal has focus, so the pause key goes to our menu | *ref* (UI hiding without UIView.Show; Shortcut blocking rows) | yes | no |
| Cursor and mouse | `InputCapture` (`Cursor.lockState/visible`, `Input.GetAxisRaw("Mouse X"/"Mouse Y")`, `Application.isFocused`); `Input.GetKey(KeyCode)`, `Screen.height` | *ref* (Raw input row) | yes | no |
| Walk-in button | `MapDropButton` / `MapDropTool` (custom `ToolBase`, `ToolBase.RayCast`, `OverlayEffect.DrawCircle`, `UIView.Find("BulldozerButton")`, sprite `InfoIconPopulation`) | *ref* (Walk-in drop button section) | yes | no |
| Backup | `CityBackup` (`SavePanel.SaveGame(string)`, `SavePanel.isSaving`, `continueSaveDataLocation`), `GameDialogs.Confirm` (`ConfirmPanel.ShowModal`, result 1 = Yes), `GameDialogs.Error` (`ExceptionPanel`) | *ref* (City backup and pairing section) | yes | no |
| City name | `SimulationManager.m_metaData.m_CityName` via `CityState.Capture()` | SimulationManager.cs:295; *ref* | yes | no |
| Pause box | Unity IMGUI `GUI.Box`, `GUI.Button(Rect, string)`, `Screen.width/height` from `MonoBehaviour.OnGUI` | UnityEngine 5.6 (compiled) | yes | no |
| Free camera | `Quaternion.Euler`, `Mathf.Repeat/Clamp`, `CameraTakeover.Drive` | UnityEngine 5.6 (compiled) | yes | no |

## GtaSkylines.Mod (M2, city adapters)

Decompile: Assembly-CSharp unless noted. All rows compile against the owner's assemblies; none is seen in game yet.

### Research answers (a)-(f)

- **(a) Releasing a walking citizen's instance.** `CitizenManager.ReleaseCitizenInstance(ushort)` (CitizenManager.cs:1408-1435)
  calls the AI's `ReleaseInstance`, unspawns, releases the path, clears the citizen's `m_instance` and vehicle. A citizen
  left `Moving` with neither instance nor vehicle is put home by `ResidentAI.SimulationStep` (ResidentAI.cs:2003-2011,
  visit place cleared) and at its hotel or out of the city by `TouristAI` (TouristAI.cs:720-736); a dead one without
  a vehicle is released (ResidentAI.cs:1983-1988). `CityEffectApplier.TakeCitizen` releases the instance and, for a
  resident whose instance target (`CitizenInstance.m_targetBuilding`, CitizenInstance.cs:84, not `Flags.TargetIsNode`
  :39) is its work or visit building, sets `Citizen.CurrentLocation` (Citizen.cs:466) there; else home. No Harmony needed.
- **(b) Releasing vehicles.** `VehicleManager.ReleaseVehicle(ushort)` (VehicleManager.cs:1647-1743): AI `ReleaseVehicle`
  (`PassengerCarAI` :179-183 removes its target; `CarAI` :27-37), unlinks leading/trailing and cargo, releases the
  path and `ReleaseUnits(m_citizenUnits)`, whose `ReleaseUnitCitizen` clears each passenger's `m_vehicle`
  (CitizenManager.cs:1220-1271), after which the passengers fall under (a). CS1 itself despawns congested cars this
  way (PassengerCarAI.cs:196-199, CargoTruckAI.cs:156). Trailers are released with the first vehicle
  (`Vehicle.GetFirstVehicle`, Vehicle.cs:1117; `m_trailingVehicle` :171). Parked: `VehicleManager.ReleaseParkedVehicle(ushort)`
  (VehicleManager.cs:1887-1910) removes it from the grid and clears the owner's `Citizen.m_parkedVehicle`.
- **(c) Sick so an ambulance comes.** `Citizen.Sick` (Citizen.cs:281) with the citizen at a building and no vehicle:
  `ResidentAI.SimulationStep` Home (ResidentAI.cs:1684-1686), Work (:1766-1772) and Visit (:1869-1897, not in a
  hospital) branches call `FindHospital(.., TransferReason.Sick)` (:2272), which posts the offer an ambulance answers.
  Health is set to 10 (`Citizen.m_health`, :271) because `FindHospital` sends children/seniors with health >= 40 to
  child/eldercare instead (:2298). Placement: `Citizen.SetVisitplace(uint, ushort, uint)` (Citizen.cs:770) for a building
  that is neither home nor work, `Citizen.GetBuildingByLocation()` (:616). Same placement for deaths: Dead branches
  call `FindHospital(.., Dead)` (ResidentAI.cs:1669, :1755, :1851), a hearse; `ResidentAI.Die` (private, :712) by reflection
  as the reference's `CitizenFate.Kill`.
- **(d) Dousing a fire.** `Building.m_fireIntensity` (byte, Building.cs:225) is lowered as `FireTruckAI.ExtinguishFire`
  does (FireTruckAI.cs:415-475); at 0 it clears the instance group (`InstanceManager.SetGroup(InstanceID, null)`,
  InstanceManager.cs:388), re-activates a producing building (`m_productionRate` :221, `Flags.Active`/`Evacuating`),
  `BuildingManager.UpdateBuildingRenderer(ushort, bool)` (:5803), `UpdateBuildingColors(ushort)` (:7819),
  `UpdateFlags(ushort, Building.Flags)` (:6492), `m_buildingOnFire.Deactivate(ushort, bool)` (:2926) when
  `GuideManager.m_properties` is set, and caps sub-buildings (`m_subBuilding` :173, `m_parentBuilding` :175).
- **(e) Chirper as a citizen.** `MessageManager.QueueMessage(MessageBase)` (MessageManager.cs:535-542, enqueues only
  when the manager has properties) with the game's own `CustomCitizenMessage(uint senderID, string message, string tag)`
  (CustomCitizenMessage.cs:3-16): with a null tag `GetText` returns the text verbatim; the sender name is
  `CitizenManager.GetCitizenName` or `GetDefaultCitizenName` (CitizenMessage.cs:29-38). A game type, so saved chirps
  load without the mod (a mod subclass would not).
- **(f) Doors and front.** `BuildingInfo.m_enterDoors` (`Prop[]`, BuildingInfo.cs:324, filled at :1212-1264), each
  `Prop.m_position` (:103) in building space with +z = front. CS1 places a door at
  `Building.CalculatePosition(new Vector3(p.x, p.y, CalculateLocalMeshOffset(info, Length) - p.z))`
  (BuildingAI.cs:1735, :1786-1787; also :1528-1531), where `CalculatePosition` (Building.cs:676-683) maps local x to
  (cos a, 0, sin a) and local z to (sin a, 0, -cos a), `a = Building.m_angle` (:169, radians), and
  `CalculateLocalMeshOffset` (:596-604) is `(Length - m_cellLength) * 4`, negated unless `m_expandFrontYard` (BuildingInfo.cs:249).
  The front (sidewalk) is `m_position + (-sin a, 0, cos a) * Length * 4` (`CalculateSidewalkPosition`, Building.cs:657-661),
  so the Game's yaw is `-a` in degrees (`BuildingFrame`, tested). No door: that front centre.

### Members used

| Area | Member | Read at | Compiles | Seen in game |
|---|---|---|---|---|
| Ground / water | `TerrainManager.SampleDetailHeightSmooth(Vector3)`, `SampleRawHeightSmoothWithWater(Vector3, bool, float)`, `SampleRawHeightSmooth(Vector3)` | TerrainManager.cs:1449, :1413, :1399 | yes | no (*ref* rows too) |
| Time | `SimulationManager.m_currentDayTimeHour`, `m_enableDayNight`, `m_dayTimeOffsetFrames`, `DAYTIME_FRAMES` (65536) | SimulationManager.cs:361, :364, :352, :275 | yes | no |
| Sim thread | `SimulationManager.AddAction(Action)` | SimulationManager.cs:743 | yes | no |
| Districts | `DistrictManager.GetDistrict(Vector3)`, `GetDistrictName(int)`; city name `SimulationMetaData.m_CityName` | DistrictManager.cs:4572, :6459; SimulationMetaData.cs:47 | yes | no |
| Traffic side | `SimulationMetaData.m_invertTraffic` (`MetaBool.True` = left-hand); already applied in `NetInfo.Lane.m_finalDirection` | SimulationMetaData.cs:29; NetInfo.cs:1100-1107 | yes | no |
| Roads | `NetManager.m_segments`, `m_nodes`, `m_segmentCount`, `m_nodeCount`, `m_segmentGrid` (270 x 270, 64 m), `GetSegmentName(ushort)` (allocates; cached) | NetManager.cs:1669, :1666, :1659, :1657, :1690/:2018, :5740 | yes | no |
| Roads | `NetSegment.m_startNode/m_endNode/m_startDirection/m_endDirection/m_flags/m_nextGridSegment/Info`, `Flags.Created/Deleted/Invert`, static `CalculateMiddlePoints(Vector3 x4, bool, bool, out Vector3, out Vector3)`; `NetNode.m_position`, `Flags.Middle` | NetSegment.cs:103, :105, :85, :87, :89, :131, :169, :14-18, :2416; NetNode.cs:121 | yes | no |
| Roads | `NetInfo.m_lanes`, `m_netAI`; `Lane.m_laneType`, `m_vehicleType`, `m_finalDirection`, `m_width`, `m_speedLimit`; `RoadBaseAI.m_highwayRules`; `RoadBridgeAI`, `RoadTunnelAI`; vehicle speed = 8 x lane limit | NetInfo.cs:800, :830, :75-144; RoadBaseAI.cs:24; VehicleAI.cs:913-918 | yes | no |
| Change detection | `SimulationManager.m_currentBuildIndex` (moved by the player's tools, e.g. NetTool.cs:1298) | SimulationManager.cs:313 | yes | no |
| Buildings | `BuildingManager.m_buildings`, `m_buildingGrid` (270 x 270, 64 m), `GetBuildingName(ushort, InstanceID)`, `EventBuildingCreated/Released` (sim thread) | BuildingManager.cs:2836, :2845/:3285, :7915, :3258-3262 | yes | no |
| Buildings | `Building.m_flags` (`Created`, `Deleted`, `Completed` 0x10000, `Active`, `Abandoned`, `Collapsed`, `Evacuating`), `m_position`, `m_angle`, `Width`, `Length`, `Info`, `m_citizenUnits`, `m_nextGridBuilding`, `GetLastFrameData().m_fireDamage` | Building.cs:159, :12-58, :167, :169, :369, :381, :345, :165, :183, :525, :142 | yes | no |
| Buildings | `BuildingInfo.m_class` (`ItemClass.m_service/m_subService/m_level`), `m_buildingAI`, `m_cellLength`, `m_size`, `BuildingInfoBase.m_mesh` | BuildingInfo.cs:214, :282, :235, :345; ItemClass.cs:120-194, :309-317, :417-421; BuildingInfoBase.cs:21 | yes | no |
| Citizens | `CitizenManager.m_citizens`, `m_units`, `m_instances`, `m_citizenGrid`, `GetCitizenName(uint)`, `GetDefaultCitizenName(uint)`, `ReleaseCitizen(uint)` | CitizenManager.cs:794, :797, :800, :803, :1860, :1855, :1326 | yes | no |
| Citizens | `Citizen.m_flags` (`Tourist`, `Student`, `Created`), `m_homeBuilding`, `m_workBuilding`, `m_visitBuilding`, `m_instance`, `m_vehicle`, `m_health`, `m_wellbeing`, `Age`, `Dead`, `Sick`, `WealthLevel`, `EducationLevel`, `GetGender(uint)` (id & 1), `GetHappiness(int, int)`, `GetCitizenInfo(uint)` | Citizen.cs:253-273, :281, :300, :319, :445, :487, :574, :579, :1080 | yes | no |
| Citizens | Age has no years formula in CS1 (UI shows only `GetAgeGroup`, bounds 15/45/90/180, Citizen.cs:1106-1125); `CityMath.AgeYears` maps those bounds to 13/20/30/65 years | Citizen.cs:241-249; CitizenWorldInfoPanel.cs:34 | yes | no |
| Units | `CitizenUnit.m_flags` (`Home` 0x10, `Work`, `Visit`, `Student`), `m_nextUnit`, `GetCitizen(int)`, `m_citizen0` | CitizenUnit.cs:7-18, :30, :57, :20 | yes | no |
| Models | `PrefabCollection<T>.LoadedCount()`, `GetLoaded(uint)`; `VehicleInfo.m_vehicleType`, `m_isLargeVehicle`, `m_maxSpeed`, `m_acceleration`, `m_braking`, `m_vehicleAI`, `m_generatedInfo`, `m_mesh` bounds; `CitizenInfo.m_gender`, `m_agePhase`, `m_citizenAI` (`ResidentAI`/`TouristAI`/`ServicePersonAI`) | PrefabCollection.cs:38, :43; VehicleInfo.cs:268-323; VehicleInfoBase.cs:13; CitizenInfo.cs:30, :34, :77 | yes | no |
| Models | Acceleration/braking in m/s² assume frame quantities per 1/3.75 s (`Vehicle.GetSmoothVelocity` x 3.75, Vehicle.cs:896-904): **hypothesis**, check in game | Vehicle.cs:903 | yes | no |
| Snapshot | Grids, frames and placement as the reference's `MovingObjects` (*ref* "Moving obstacles"); `VehicleAI.GetColor(ushort, ref Vehicle, InfoMode, SubInfoMode)` and the parked overload; velocity = lerped `Frame.m_velocity` x 3.75 | VehicleAI.cs:23, :52; Vehicle.cs:896-904 | yes | no |
| Effects | (a)-(f) above; `CitizenFate.Panic/Kill` | *ref* "Citizens as villagers" | yes | no |
| Collision | `Skylines.Host.Geometry` exporters as the reference streamed them | *ref* (Collision rows) | yes | no |
| Debug view | Unity `Camera.onPostRender`, `GL.LINES`, `Shader.Find("Hidden/Internal-Colored")`, `Input.GetKeyDown(KeyCode.F9)` | UnityEngine 5.6 (compiled; *ref* CollisionViewer) | yes | no |
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

## M3 rendering

### Findings

- **Render hook.** `RenderManager.LateUpdate` (RenderManager.cs:572-687) clears `m_lightSystem.m_lightBuffer`, sets
  `PrefabPool.m_canCreateInstances = 1` (:576), calls `UpdateCameraInfo` (camera bounds for culling), then every registered
  `IRenderableManager.BeginRendering(cameraInfo)`, renders the groups, every `EndRendering(cameraInfo)`, and finally
  `m_lightSystem.EndRendering`. Managers register through `SimulationManager.RegisterManager` → static
  `RenderManager.RegisterRenderableManager` (SimulationManager.cs:574-576; RenderManager.cs:475), a static list with no
  unregister. `RenderHook` registers once per process and forwards `EndRendering` to the active `Renderer`, so
  `Vehicle.RenderInstance` (its effects use `cameraInfo`) and `LightSystem.DrawLight` land in this frame's buffers.
- **Prefab instances.** `PrefabInfo.ObtainPrefabInstance<T>(InstanceID, maxCount)` (PrefabInfo.cs:204) → `PrefabPool`
  (PrefabPool.cs): a `Dictionary<InstanceID, PrefabInfo>` per prefab; a new instance is created at most once per frame
  across all prefabs (`m_canCreateInstances`) and that call returns null (:57-70). An instance is released when its own
  `PrefabInfo.LateUpdate` finds `m_instanceNeeded` unset since the previous LateUpdate (PrefabInfo.cs:190-200), so
  instances must be obtained every frame and are released by not obtaining them. CS1 keys citizens by
  `InstanceID.CitizenInstance` (type 9, CitizenInstance.cs:178-180). Ours use the raw type byte 0xF0 (InstanceType.cs
  goes to 23; `InstanceID.Type`/`RawData`, InstanceID.cs:61-84) and index slot + 1 (`IsEmpty` is index 0, :59), so they
  never collide; typed getters such as `Vehicle` return 0 for a foreign type (:109-114).
- **Citizen pose.** `CitizenInfo.SetRenderParameters(pos, rot, velocity, colour, AnimationState, underground, forceState)`
  (CitizenInfo.cs:341): states Default 0, Panic 1, Sitting 2 (:10-27); thresholds MIN_SPEED_WALK 0.1, MIN_SPEED_SPRINT 3.2
  (:148-150); animator speed = magnitude (walk) or magnitude / 3.2 (sprint), times `m_simulationTimeSpeed` (:450-461) —
  **animations freeze while the simulation is paused**. `forceState` replays the state clip only when the state changes.
  Colour: `m_color0` (:59).
- **Vehicles.** Static `Vehicle.RenderInstance` (Vehicle.cs:363) indexes `m_vehicles.m_buffer[id.Vehicle]` (:380) and
  passes it to `RenderExtraStuff`/`SetMeshParameters`; we pass `InstanceID.Empty` (slot 0). Body matrix:
  `VehicleAI.CalculateBodyMatrix` (VehicleAI.cs:72-79) = TRS(position, rotation · Euler(sway.z rad, 0, −sway.x rad)), +sway.y.
  Light state as CarAI fills it (CarAI.cs:587-590): x headlights 5, y tail 0.5 / brake 5, z/w indicators. Tyres: x steer
  angle, y travelled distance (Vehicle.cs:338-339). Siren: `Flags.Emergency2` 0x80 (Vehicle.cs:18; PoliceCarAI.cs:194).
  Sway/track from `VehicleInfoGen.m_wheelGauge`, `m_wheelBase` (VehicleInfoGen.cs:13-15).
- **Lighting.** `DayNightProperties.Update → Refresh` (DayNightProperties.cs:839-871) rewrites sun rotation (:742),
  sun intensity/colour/enabled, moon and trilight ambient (`UpdateLighting`, :914) every frame; `sunLightSource`,
  `moonLightSource` (:596-618); fog is the camera image effects `FogEffect` and `DayNightFogEffect`. `IndoorLighting`
  re-applies its override every LateUpdate while inside and restores the recorded values once.

### Members used

| Area | Member | Read at | Compiles | Seen in game |
|---|---|---|---|---|
| Hook | `IRenderableManager` (all members), `RenderManager.RegisterRenderableManager`, `RenderManager.CameraInfo.m_camera`, `DrawCallData` | IRenderableManager.cs:1-28; RenderManager.cs:475, :61; DrawCallData.cs:1 | yes | no |
| Peds | `PrefabInfo.ObtainPrefabInstance<CitizenInfo>(InstanceID, int)`, `InstanceID.RawData`, `CitizenInfo.SetRenderParameters(..., bool forceState)`, `CitizenInfo.AnimationState`, `m_color0` | PrefabInfo.cs:204; InstanceID.cs:73; CitizenInfo.cs:341, :10, :59 | yes | no |
| Cars | static `Vehicle.RenderInstance(CameraInfo, VehicleInfo, Vector3, Quaternion, Vector3, Vector4, Vector4, Vector3, float, Color, Flags, Flags2, int, InstanceID, bool, bool)`, `Vehicle.Flags.Created/Spawned/Emergency2`, `VehicleInfoGen.m_wheelGauge/m_wheelBase`, `VehicleInfo.m_mesh` bounds | Vehicle.cs:363, :11-18; VehicleInfoGen.cs:13-15 | yes | no |
| Lights | `RenderManager.lightSystem`, `LightSystem.DrawLight(LightType, Vector3, Vector3, Vector3, Color, float, float, float, float, bool)` | RenderManager.cs:285; LightSystem.cs:223 | yes | no |
| Lighting | `DayNightProperties.instance`, `sunLightSource`, `moonLightSource`; `FogEffect`, `DayNightFogEffect` (Behaviour.enabled) | DayNightProperties.cs:382, :596, :608; FogEffect.cs:6; DayNightFogEffect.cs:5 | yes | no |
| Materials | `Shader.Find("Custom/Props/Prop/Default")` with `_MainTex`, `_Color`, `_XYSMap`, `_ACIMap`; `Hidden/Internal-Colored` with `_Color`, `_SrcBlend`, `_DstBlend`, `_Cull`, `_ZWrite`; props layer `LayerMask.NameToLayer("Props")` | *ref* BlockRenderer, TunnelInteriorRenderer | yes | no |
| Unity | `Graphics.DrawMesh(Mesh, Matrix4x4, Material, int, Camera, int, MaterialPropertyBlock, ShadowCastingMode, bool)`, `Texture2D.SetPixels32(Color32[])`, `Apply(true, true)` (interior textures; replaced `LoadRawTextureData`, see the 2026-10-10 section) | UnityEngine 5.6 (compiled) | yes | no |
| Camera | `Skylines.Host.Camera.CameraTakeover.Drive` | src/Skylines.Host/Camera/CameraTakeover.cs | yes | no |

## GtaSkylines.Mod HUD and menus (M4)

Unity 5.6 members were read in an ilspycmd decompile of `refs/cs1/Managed/UnityEngine.dll` kept locally at
`~/.cache/scratch/unity56/<Type>.cs` (local only, line numbers from that decompile).

| Area | Member | Read at | Compiles | Seen in game |
|---|---|---|---|---|
| Font | `UIView.GetAView()` (static), `UIView.defaultFont` (UIFont), `UIFont.baseFont` (virtual `UnityEngine.Font`; `UIDynamicFont` overrides it with its dynamic `m_BaseFont`) | ColossalManaged/ColossalFramework.UI/UIView.cs:2046, :554; UIFont.cs:11; UIDynamicFont.cs:747, :862 | yes | no |
| Font | `Font.GetOSInstalledFontNames()`, `Font.CreateDynamicFontFromOSFont(string, int)`, `Resources.GetBuiltinResource<Font>("Arial.ttf")`, `Object.name` | Font.cs:107, :117; Resources.cs:82 | yes | no |
| IMGUI | `GUI.Label(Rect, GUIContent, GUIStyle)`, `GUI.DrawTexture(Rect, Texture)`, `GUI.DrawTextureWithTexCoords(Rect, Texture, Rect)`, `GUI.color`, `GUI.matrix`, `GUIUtility.RotateAroundPivot(float, Vector2)` | GUI.cs:373, :379, :472, :224, :181; GUIUtility.cs:238 | yes | no |
| IMGUI | `new GUIStyle()`, `font`, `fontSize`, `fontStyle`, `alignment`, `wordWrap`, `richText`, `clipping`, `normal.textColor`, `CalcSize(GUIContent)`; `new GUIContent()`, `.text` | GUIStyle.cs:439, :264, :409, :419, :313, :323, :429, :333, :59, :597; GUIStyleState.cs:33; GUIContent.cs:27 | yes | no |
| IMGUI | `Event.current`, `type`, `keyCode`, `button`, `mousePosition`, `delta`, `Use()` | Event.cs:172, :226, :296, :236, :18, :31, :705 | yes | no |
| Textures | `new Texture2D(int, int, TextureFormat, bool)`, `LoadRawTextureData(byte[])`, `Apply(bool, bool)`, `Texture2D.whiteTexture`, `Texture.wrapMode`, `filterMode`, `width` | Texture2D.cs:54, :195, :255, :35; Texture.cs:87, :67, :31 | yes | no |
| Minimap pass | `new RenderTexture(int, int, int, RenderTextureFormat)`, `Create()`, `Release()`, `RenderTexture.active`; `GL.PushMatrix/PopMatrix`, `GL.LoadPixelMatrix(float, float, float, float)`, `GL.Clear(bool, bool, Color)`, `GL.Begin(GL.QUADS)`, `GL.Color`, `GL.TexCoord2`, `GL.Vertex3`, `GL.End`; `Material.SetPass(int)`, `mainTexture`, `SetInt` | RenderTexture.cs:223, :336, :345; GL.cs:187, :134, :138, :156, :212, :123, :14, :76, :96, :65, :130; Material.cs:349, :33, :403 | yes | no |
| Shaders | `Shader.Find("UI/Default UI Shader")` (CS1's own UI shader, textured + vertex colour) for the map quad; `Hidden/Internal-Colored` (*ref*) for GPS, search zone and the weapon wheel | ColossalManaged/ColossalFramework.UI/UIView.cs:932, UITextureSprite.cs:141; Shader.cs:77 | yes | no |
| Input | `Input.GetKey(KeyCode)`, `Input.GetKeyDown`, `Input.mouseScrollDelta`, `Time.frameCount`, `Time.realtimeSinceStartup` | as M1 rows; GUIStyle.cs:530 uses realtimeSinceStartup | yes | no |
| City map | `ICity.TryWaterSurface`, `BuildingsNear`, `TryGetBuilding`; `RoadGraph.TryGetSegment` probed over ids 0..65535 (segment id = CS1 segment index, `RoadGraphBuilder.cs:124`) | src/GtaSkylines.Game/City/ICity.cs:17, :35, :32 | yes | no |

## M7 wiring (play session)

| What | API | Read at | Built | In game |
|---|---|---|---|---|
| Camera before culling | `RenderManager.LateUpdate` calls `UpdateCameraInfo` (culling bounds from the camera) before `BeginRendering`; Unity runs every `Update` before any `LateUpdate`, so `GtaPlaySession` drives the camera from Update (and again in LateUpdate) with no script-order dependency | Assembly-CSharp/RenderManager.cs:572-590 | yes | no |
| Save from the pause menu | `SavePanel.SaveGame(string)` (returns false while loading or saving), `SavePanel.isSaving`, private static `m_LastSaveName` read by reflection (only a setter `lastLoadedName` is public) | Assembly-CSharp/SavePanel.cs:33, :77-85, :370-380 | yes | no |
| Save-time profile snapshot | `PlayController` snapshots on the frame `SavePanel.isSaving` rises; the save coroutine takes its screenshot first (`Snapshot` coroutine) before serialising, so `OnSaveData` sees the fresh record | Assembly-CSharp/SavePanel.cs:376-377 | yes | no |
| Chirp(0) sender | nearest `CitizenInstance` with `Flags.Created` and `m_citizen` ≠ 0 within 200 m of the player (`GetLastFramePosition`), on the simulation thread; `CustomCitizenMessage(uint, string, string)` | Assembly-CSharp/CitizenInstance.cs:12, :78, :441; CustomCitizenMessage.cs:3 | yes | no |

## Architecture review fixes (2026-10-10)

Unity 5.6 members were read in a local decompile of `~/.cache/gta-skylines/refs/cs1/Managed/UnityEngine.dll`
(ilspycmd, one `.cs` per type under `~/.cache/scratch/unity56/`).

| Area | Member | Read at | Compiles | Seen in game |
|---|---|---|---|---|
| Interior textures | `Texture2D.LoadRawTextureData(byte[])` is a native call (`LoadRawTextureData_ImplArray`, extern) whose mip handling cannot be read; replaced by `SetPixels32(Color32[])` (level 0) + `Apply(bool updateMipmaps, bool makeNoLongerReadable)`, which builds the whole chain on any version | UnityEngine/Texture2D.cs:153, :189-198, :255 | yes | no |
| Render fault notice | `ExceptionPanel.SetMessage(string title, string message, bool error)` via `UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel")` (`Skylines.Host.Saves.GameDialogs.Error`), shown by `PlayController` after a fault exit | Assembly-CSharp/ExceptionPanel.cs:6, :16 | yes | no |
| Minimap | `GL.Viewport(Rect)` (sub-viewport of the grow-only RenderTexture), `GUI.DrawTextureWithTexCoords(Rect, Texture, Rect)` | UnityEngine/GL.cs:161; GUI.cs:472 | yes | no |
| Audio teardown | `Object.Destroy(AudioClip)` for every bank clip (`ClipBank.Dispose`), after `AudioSource.Stop()` and `clip = null` | UnityEngine/AudioClip.cs (UnityEngine.Object) | yes | no |
| Lighting order | `DayNightProperties.Update()` → `Refresh()` (:839-871) rewrites sun, moon and ambient every frame; its `LateUpdate()` (:873-879) only draws the star mesh. No `DefaultExecutionOrder` in Assembly-CSharp. `IndoorLighting` therefore relies on Unity's phase order (every `Update` before every `LateUpdate`), not on script order between LateUpdates: its first `Apply` (PlayController.LateUpdate) captures this frame's DayNight values. Assumption recorded: no other script rewrites sun, ambient or the fog effects' `enabled` in LateUpdate before ours in the frame of the first `Apply`; if one does, `Restore` writes that script's values back (sun and ambient are overwritten by the next `Refresh` anyway; fog `enabled` is not) | DayNightProperties.cs:839, :844, :873 | yes | no |
| Snapshot | `Singleton<CitizenManager>.instance.m_instances` (`Array16<CitizenInstance>`), `m_citizenGrid` (`ushort[]`), `m_units` (`Array32<CitizenUnit>`) | CitizenManager.cs:800, :803, :797 | yes | no |
| Snapshot | `CitizenInstance.Flags` (`Created` 1, `Deleted` 2, `Character` 0x10, `HangAround` 0x40), `m_flags`, `m_citizen` (uint), `m_nextGridInstance` (ushort), `Info` (CitizenInfo) | CitizenInstance.cs:9-18, :74, :78, :86, :122 | yes | no |
| Snapshot | `CitizenInstance.GetFrameData(uint)` → `CitizenInstance.Frame { m_velocity, m_position, m_rotation, m_underground, m_insideBuilding, m_transition }` | CitizenInstance.cs:453; :47-59 | yes | no |
| Snapshot | `CitizenUnit.m_citizen0` (uint; the driver of a vehicle's first unit) | CitizenUnit.cs:20 | yes | no |
| Snapshot | `Singleton<VehicleManager>.instance.m_vehicles` (`Array16<Vehicle>`), `m_parkedVehicles` (`Array16<VehicleParked>`), `m_vehicleGrid`, `m_parkedGrid` (`ushort[]`) | VehicleManager.cs:741, :744, :753, :759 | yes | no |
| Snapshot | `Vehicle.Flags` (`Created` 1, `Deleted` 2, `Spawned` 4), `m_flags`, `m_citizenUnits` (uint), `m_nextGridVehicle`, `m_leadingVehicle` (ushort), `Info` (VehicleInfo) | Vehicle.cs:11-13, :145, :149, :157, :169, :219 | yes | no |
| Snapshot | `Vehicle.GetTargetFrame(VehicleInfo, ushort)` → uint, `Vehicle.GetFrameData(uint)` → `Vehicle.Frame { m_rotation, m_velocity, m_position, m_underground, m_transition, m_insideBuilding }` | Vehicle.cs:1038, :942; :89-115 | yes | no |
| Snapshot | `VehicleParked.Flags` (`Created` 1, `Deleted` 2), `m_flags` (ushort), `m_nextGridParked` (ushort), `m_position` (Vector3), `m_rotation` (Quaternion), `Info` | VehicleParked.cs:12-13, :28, :30, :22, :20, :34 | yes | no |
| Snapshot | `SimulationManager.m_referenceFrameIndex` (uint), `m_referenceTimer` (float) for the drawn frame pair and lerp | SimulationManager.cs:316, :334 | yes | no |
| Snapshot | `VehicleInfo.m_vehicleAI`; `VehicleAI.GetColor(ushort, ref Vehicle, InfoManager.InfoMode, InfoManager.SubInfoMode)` (virtual; parked overload :52) with `InfoMode.None`, `SubInfoMode.Default` (0) | VehicleInfo.cs:308; VehicleAI.cs:23; InfoManager.cs:11-13, :105-108 | yes | no |
| VehicleMath | No CS1 member is called; `Vehicle.RenderInstance` is cited in a comment only (the formulas it mirrors are in the M3 findings above) | — | yes | no |
## M6b self-test

| What | API | Read at | Built | In game |
|---|---|---|---|---|
| Second loading extension | `LoadingWrapper.GetImplementations` collects every `ILoadingExtension` in enabled mods (`PluginManager.GetImplementations<ILoadingExtension>`), so `SelfTestLoading` runs beside `Loading` | Assembly-CSharp/LoadingWrapper.cs:49-53 | yes | no |
| Road for the spawn | `NetManager.m_segments` (`Array16<NetSegment>`, `.m_buffer`), `NetSegment.Flags.Created`, `NetSegment.Info`, `NetSegment.m_middlePosition`, `NetInfo.m_netAI`, `RoadBaseAI` | Assembly-CSharp/NetManager.cs:1669, NetSegment.cs:14, :83, :169, NetInfo.cs:830, RoadBaseAI.cs:8 | yes | no |
| Unpause before entry | `SimulationManager.SimulationPaused` (get/set) | Assembly-CSharp/SimulationManager.cs:412 | yes | no |
| UI restored check | `UIView.GetAView()`, `UIView.uiCamera`, `UIView.HasModalInput()` | ColossalManaged/ColossalFramework.UI/UIView.cs:2046, :626, :801 | yes | no |
| Report header | `BuildConfig.applicationVersion` (static string), `SimulationManager.m_metaData.m_CityName` | Assembly-CSharp/BuildConfig.cs:52; SaveAutoloader (M1 row) | yes | no |
| Screenshots | Unity 5.6 `Application.CaptureScreenshot(string)`; written at the end of the frame (reference note, verified with ilspycmd on the refs UnityEngine.dll) | reference docs/CS1-API-NOTES.md:76 | yes | no |
| Autoload, quit, autosave | `SaveAutoloader` (reused from the reference: `LoadingManager.LoadLevel`, `QuitApplication`, `autoSaveTimer.Pause`) | src/Skylines.Host/SaveAutoloader.cs comments | yes | no |

## Police helicopter (F-HEAT-7)

| What | API | Read at | Built | In game |
|---|---|---|---|---|
| Draw the helicopter | Same static `Vehicle.RenderInstance` as the cars, with `Flags.Created | Spawned | Flying | Emergency2` and `InstanceID.Empty`; `Flying = 0x4000`, `Emergency2 = 0x80` | Vehicle.cs:363, :39, :18 | yes | no |
| Helicopter tilt | `HelicopterAI.CalculateBodyMatrix` builds `Quaternion.Euler(sway.z * 57.29578, 0, sway.x * -57.29578)` from the `swayPosition` argument (radians: x lean, z nod) and `CalculateTyreMatrix` is identity, so tilt is passed straight in `swayPosition` | HelicopterAI.cs:8-19; Vehicle.cs:385-386 | yes | no |
| Rotor spin | The only per-frame animation input the shader gets is `_TyrePosition` (`x` steer, `y` travel distance, Vehicle.cs:337-341, :411); we feed an accumulating value in `y`. The shader is not decompiled, so that it spins the rotor, and the rate, are unverified | Vehicle.cs:337-341, :411 | yes | no |
| Spotlight light | `LightSystem.DrawLight(LightType.Spot, ...)`: for `Spot`, `dir.w = -cos(spotAngle/2)`; `spotAngle` is the full cone angle in degrees | LightSystem.cs:223-246 | yes | no |

## Readiness review 1 fixes (debug overlay, unattended self-test, entry drop)

| What | API | Read at | Built | In game |
|---|---|---|---|---|
| Autosave hold | `Singleton<LoadingManager>.instance.autoSaveTimer` (`AutoSaveTimer`); `Pause()` increments and `UnPause()` decrements a counter (clamped at 0); `AutoSaveRoutine` waits the interval, then spins while the counter is above 0, then calls `SavePanel.AutoSave("AutoSave")` at once, so a release after the interval elapsed saves immediately; `Start()`/`Stop()` never reset the counter (it survives level loads); `Start()` runs right after `OnLevelLoaded`. CS1's own panels pair Pause/UnPause on the main thread. `AutosaveHold` pairs them; an unattended run never releases before the quit | Assembly-CSharp/LoadingManager.cs:223, :99-111, :36-97, :113-131, :1832-1834; SavePanel.cs:131, :141, :352-368 | yes | no |
| Unattended frame rate | `QualitySettings.vSyncCount`, `Application.targetFrameRate` read before and written back after the run (runtime only, settings file untouched) | UnityEngine 5.6 (compiled) | yes | no |
| Entry drop surface | `NetManager.RayCast(NetInfo, Segment3, float snapElevation, bool nameOnly, Service, Service, SubService, SubService, Layer, Layer, NetNode.Flags, NetSegment.Flags, out Vector3, out ushort node, out ushort segment)` with `null, -1000, false, None×4, Layer.Default×2, Flags.None×2` (the `RaycastInput` defaults, ToolBase.cs:92-96, :104, :114-116, as the walk-in drop uses them); `BuildingManager.RayCast(Segment3, Service, SubService, Layer, Building.Flags, out Vector3, out ushort)`; `TerrainManager.RayCast`; nearest along the ray with terrain 100 m farther, as `ToolBase.RayCast` does; `Segment3.a` | NetManager.cs:4136; BuildingManager.cs:6389; TerrainManager.cs:1065; ToolBase.cs:536-600; ColossalManaged/ColossalFramework.Math/Segment3.cs:7 | yes | no |
| Debug overlay | Unity `Camera.onPostRender`, `GL.Begin(GL.LINES)`/`Vertex3`/`Color`, `Material.SetInt("_ZTest")` (as `CollisionViewer`), `Camera.WorldToScreenPoint`, `GUI.Label`/`GUI.Box`, `GUIStyle.CalcHeight`; no CS1 member | UnityEngine 5.6 (compiled) | yes | no |
