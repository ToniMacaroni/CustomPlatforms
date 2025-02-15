﻿using System;
using System.Threading;
using System.Threading.Tasks;

using CustomFloorPlugin.Configuration;
using CustomFloorPlugin.Helpers;
using CustomFloorPlugin.Interfaces;

using SiraUtil.Logging;

using Zenject;


namespace CustomFloorPlugin
{
    /// <summary>
    /// Handles platform spawning and despawning
    /// </summary>
    public sealed class PlatformSpawner : IInitializable, IDisposable
    {
        private readonly SiraLog _siraLog;
        private readonly PluginConfig _config;
        private readonly Random _random;
        private readonly AssetLoader _assetLoader;
        private readonly EnvironmentHider _environmentHider;
        private readonly PlatformManager _platformManager;
        private readonly GameScenesManager _gameScenesManager;
        private readonly LobbyGameStateModel _lobbyGameStateModel;

        private MultiplayerGameState _prevGameState;
        private CancellationTokenSource? _cancellationTokenSource;
        private DiContainer _container = null!;

        internal CustomPlatform RandomPlatform => _platformManager.AllPlatforms[_random.Next(0, _platformManager.AllPlatforms.Count)];

        public PlatformSpawner(SiraLog siraLog,
                               PluginConfig config,
                               Random random,
                               AssetLoader assetLoader,
                               EnvironmentHider environmentHider,
                               PlatformManager platformManager,
                               GameScenesManager gameScenesManager,
                               LobbyGameStateModel lobbyGameStateModel)
        {
            _siraLog = siraLog;
            _config = config;
            _random = random;
            _assetLoader = assetLoader;
            _environmentHider = environmentHider;
            _platformManager = platformManager;
            _gameScenesManager = gameScenesManager;
            _lobbyGameStateModel = lobbyGameStateModel;
        }

        public void Initialize()
        {
            _gameScenesManager.transitionDidStartEvent += OnTransitionDidStart;
            _gameScenesManager.transitionDidFinishEvent += OnTransitionDidFinish;
            _lobbyGameStateModel.gameStateDidChangeAlwaysSentEvent += OnMultiplayerGameStateModelDidChange;
        }

        public void Dispose()
        {
            _gameScenesManager.transitionDidStartEvent -= OnTransitionDidStart;
            _gameScenesManager.transitionDidFinishEvent -= OnTransitionDidFinish;
            _lobbyGameStateModel.gameStateDidChangeAlwaysSentEvent -= OnMultiplayerGameStateModelDidChange;
        }

        /// <summary>
        /// Clean up before switching scenes
        /// </summary>
        // ReSharper disable once AsyncVoidMethod
        private async void OnTransitionDidStart(float aheadTime)
        {
            if (_lobbyGameStateModel.gameState == MultiplayerGameState.Game) _lobbyGameStateModel.SetGameStateWithoutNotification(MultiplayerGameState.Lobby);
            await ChangeToPlatformAsync(_platformManager.DefaultPlatform);
        }

        /// <summary>
        /// Decide which platform to change to based on the type of the <see cref="ScenesTransitionSetupDataSO"/>
        /// </summary>
        // ReSharper disable once AsyncVoidMethod
        internal async void OnTransitionDidFinish(ScenesTransitionSetupDataSO? setupData, DiContainer container)
        {
            CustomPlatform platform;
            switch (setupData)
            {
                case null when _lobbyGameStateModel.gameState != MultiplayerGameState.Lobby:
                case MenuScenesTransitionSetupDataSO:
                    platform = _config.ShufflePlatforms ? RandomPlatform : _platformManager.MenuPlatform;
                    break;
                case StandardLevelScenesTransitionSetupDataSO when _platformManager.APIRequestedPlatform is not null:
                    platform = _platformManager.APIRequestedPlatform;
                    break;
                case StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupDataSO when standardLevelScenesTransitionSetupDataSO.difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.requires360Movement:
                    platform = _config.ShufflePlatforms ? RandomPlatform : _platformManager.A360Platform;
                    break;
                case StandardLevelScenesTransitionSetupDataSO:
                case MissionLevelScenesTransitionSetupDataSO:
                case TutorialScenesTransitionSetupDataSO:
                    platform = _config.ShufflePlatforms ? RandomPlatform : _platformManager.SingleplayerPlatform;
                    break;
                case MultiplayerLevelScenesTransitionSetupDataSO when container.HasBinding<MultiplayerLocalActivePlayerFacade>():
                    platform = _config.ShufflePlatforms ? RandomPlatform : _platformManager.MultiplayerPlatform;
                    break;
                case null:
                case BeatmapEditorScenesTransitionSetupDataSO:
                    platform = _platformManager.DefaultPlatform;
                    break;
                default:
                    return;
            }

            _container = container;
            _environmentHider.OnTransitionDidFinish(setupData, container);
            await ChangeToPlatformAsync(platform);
        }

        /// <summary>
        /// Despawns the current platform when entering a lobby and changing back when leaving
        /// </summary>
        // ReSharper disable once AsyncVoidMethod
        private async void OnMultiplayerGameStateModelDidChange(MultiplayerGameState multiplayerGameState)
        {
            CustomPlatform platform;
            switch (multiplayerGameState)
            {
                case MultiplayerGameState.None:
                    platform = _config.ShufflePlatforms ? RandomPlatform : _platformManager.MenuPlatform;
                    break;
                case MultiplayerGameState.Lobby when _prevGameState != MultiplayerGameState.Game:
                    platform = _platformManager.DefaultPlatform;
                    break;
                default:
                    return;
            }

            _prevGameState = multiplayerGameState;
            await ChangeToPlatformAsync(platform);
        }

        /// <summary>
        /// Changes to a specific <see cref="CustomPlatform"/>
        /// </summary>
        /// <param name="platform">The <see cref="CustomPlatform"/> to change to</param>
        public async Task ChangeToPlatformAsync(CustomPlatform platform)
        {
            if (_platformManager.ActivePlatform == platform) return;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = _cancellationTokenSource.Token;

            DestroyCustomObjects();
            _platformManager.ActivePlatform.gameObject.SetActive(false);
            _platformManager.ActivePlatform = platform;

            if (platform.isDescriptor)
            {
                CustomPlatform? newPlatform = await _platformManager.CreatePlatformAsync(platform.fullPath);
                if (newPlatform is null)
                {
                    await ChangeToPlatformAsync(_platformManager.DefaultPlatform);
                    return;
                }

                _platformManager.AllPlatforms.Replace(platform, newPlatform);
                UnityEngine.Object.Destroy(platform.gameObject);
                if (cancellationToken.IsCancellationRequested) return;
                _platformManager.ActivePlatform = newPlatform;
            }

            _siraLog.Debug($"Switching to {_platformManager.ActivePlatform.name}");
            _environmentHider.HideObjectsForPlatform(_platformManager.ActivePlatform);
            _platformManager.ActivePlatform.gameObject.SetActive(true);
            SpawnCustomObjects();
        }

        /// <summary>
        /// Enables or spawns all registered custom objects, as required by the selected <see cref="CustomPlatform"/>
        /// </summary>
        private void SpawnCustomObjects()
        {
            if (_lobbyGameStateModel.gameState == MultiplayerGameState.Game) _assetLoader.MultiplayerLightEffects.PlatformEnabled(_container);
            foreach (INotifyPlatformEnabled notifyEnable in _platformManager.ActivePlatform.GetComponentsInChildren<INotifyPlatformEnabled>(true))
                notifyEnable?.PlatformEnabled(_container);
        }

        /// <summary>
        /// Disables all registered custom objects, as required by the selected <see cref="CustomPlatform"/>
        /// </summary>
        private void DestroyCustomObjects()
        {
            if (_lobbyGameStateModel.gameState != MultiplayerGameState.None) _assetLoader.MultiplayerLightEffects.PlatformDisabled();
            foreach (INotifyPlatformDisabled notifyDisable in _platformManager.ActivePlatform.GetComponentsInChildren<INotifyPlatformDisabled>(true))
                notifyDisable?.PlatformDisabled();
        }
    }
}