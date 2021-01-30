using System.Linq;

using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;

using CustomFloorPlugin.Configuration;

using HMUI;

using Zenject;


namespace CustomFloorPlugin.UI {


    /// <summary>
    /// A <see cref="BSMLAutomaticViewController"/> generated by Zenject and maintained by BSML at runtime.<br/>
    /// BSML uses the <see cref="ViewDefinitionAttribute"/> to determine the Layout of the GameObjects and their Components<br/>
    /// Tagged functions and variables from this class may be used/called by BSML if the .bsml file mentions them.<br/>
    /// </summary>
    [ViewDefinition("CustomFloorPlugin.Views.PlatformLists.bsml")]
    internal class PlatformListsView : BSMLAutomaticViewController {

        [Inject]
        private readonly PluginConfig _config;

        [Inject]
        private readonly PlatformSpawnerMenu _platformSpawner;

        [Inject]
        private readonly PlatformManager _platformManager;


        /// <summary>
        /// The table of currently loaded Platforms, for singleplayer only because BSML can't use the same list for different tabs
        /// </summary>
        [UIComponent("singleplayerPlatformList")]
        public CustomListTableData singleplayerPlatformListTable = null;


        /// <summary>
        /// The table of currently loaded Platforms, for multiplayer only because BSML can't use the same list for different tabs
        /// </summary>
        [UIComponent("multiplayerPlatformList")]
        public CustomListTableData multiplayerPlatformListTable = null;


        [UIAction("select-cell")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by BSML")]
        private void TabSelect(SegmentedControl segmentedControl, int idx) {
            if (segmentedControl.selectedCellNumber == 0) {
                singleplayerPlatformListTable.tableView.ScrollToCellWithIdx(idx, TableViewScroller.ScrollPositionType.Beginning, false);
                _platformSpawner.ChangeToPlatform(PlatformType.Singleplayer);
                _platformManager.currentPlatformType = PlatformType.Singleplayer;
            }
            else {
                multiplayerPlatformListTable.tableView.ScrollToCellWithIdx(idx, TableViewScroller.ScrollPositionType.Beginning, false);
                _platformSpawner.ChangeToPlatform(PlatformType.Multiplayer);
                _platformManager.currentPlatformType = PlatformType.Multiplayer;
            }
        }


        /// <summary>
        /// Called when a <see cref="CustomPlatform"/> is selected by the user<br/>
        /// Passes the choice to the <see cref="PlatformManager"/>
        /// </summary>
        /// <param name="_1">I love how optimised BSML is</param>
        /// <param name="idx">Cell index of the users selection</param>
        [UIAction("SingleplayerSelect")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by BSML")]
        private void SingleplayerSelect(TableView _1, int idx) {
            _platformSpawner.SetPlatformAndShow(idx, PlatformType.Singleplayer);
        }


        /// <summary>
        /// Called when a <see cref="CustomPlatform"/> is selected by the user<br/>
        /// Passes the choice to the <see cref="PlatformManager"/>
        /// </summary>
        /// <param name="_1">I love how optimised BSML is</param>
        /// <param name="idx">Cell index of the users selection</param>
        [UIAction("MultiplayerSelect")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by BSML")]
        private void MultiplayerSelect(TableView _1, int idx) {
            _platformSpawner.SetPlatformAndShow(idx, PlatformType.Multiplayer);
        }


        /// <summary>
        /// Changing to the current platform when the menu is shown<br/>
        /// [Called by Beat Saber]
        /// </summary>
        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling) {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            _platformSpawner.ChangeToPlatform(_platformManager.currentPlatformType);
        }


        /// <summary>
        /// Swapping back to the standard menu environment when the menu is closed<br/>
        /// [Called by Beat Saber]
        /// </summary>
        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling) {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
            if (_config.ShowInMenu) {
                _platformSpawner.ChangeToPlatform(PlatformType.Singleplayer);
            }
            else {
                _platformSpawner.ChangeToPlatform(0);
            }
        }


        /// <summary>
        /// (Re-)Loading the table for the ListView of available platforms and environments to override.<br/>
        /// [Called by BSML]
        /// </summary>
        [UIAction("#post-parse")]
        internal void SetupPlatformLists() {
            SetupList(singleplayerPlatformListTable, PlatformType.Singleplayer);
            SetupList(multiplayerPlatformListTable, PlatformType.Multiplayer);
        }


        private void SetupList(CustomListTableData listTable, PlatformType platformType) {
            listTable.data.Clear();
            foreach (CustomPlatform platform in _platformManager.AllPlatforms) {
                listTable.data.Add(new CustomListTableData.CustomCellInfo(platform.platName, platform.platAuthor, platform.icon));
            }
            listTable.tableView.ReloadData();
            int selectedPlatform = platformType == PlatformType.Singleplayer ? _platformManager.CurrentSingleplayerPlatformIndex : _platformManager.CurrentMultiplayerPlatformIndex;
            if (!listTable.tableView.visibleCells.Any(x => x.selected)) {
                listTable.tableView.ScrollToCellWithIdx(selectedPlatform, TableViewScroller.ScrollPositionType.Beginning, false);
            }
            listTable.tableView.SelectCellWithIdx(selectedPlatform);
        }
    }
}