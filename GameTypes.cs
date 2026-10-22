// RWC Formations: the game's type names, per mod loader (0.7.0).
// BepInEx's generated code keeps the game's namespaces (GameRiver,
// GameRiver.Client; camera classes in the global namespace). MelonLoader 0.6+
// prefixes them with "Il2Cpp" (Il2CppGameRiver, and Il2Cpp for the global
// namespace). EXPECTED, check on the first MelonLoader build: if the names differ,
// only this file changes.
#if MELON
global using GR = Il2CppGameRiver;
global using GRC = Il2CppGameRiver.Client;
global using OrbitCam = Il2Cpp.OrbitCameraController;
global using ZoomCam = Il2Cpp.ZoomCameraController;
#else
global using GR = GameRiver;
global using GRC = GameRiver.Client;
global using OrbitCam = global::OrbitCameraController;
global using ZoomCam = global::ZoomCameraController;
#endif
