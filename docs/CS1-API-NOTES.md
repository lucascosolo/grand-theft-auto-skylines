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
