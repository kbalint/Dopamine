﻿using Dopamine.Common.Presentation.Interfaces;
using Dopamine.Common.Presentation.Utils;
using Dopamine.Common.Presentation.ViewModels;
using Dopamine.Common.Services.Metadata;
using Dopamine.Common.Services.Playback;
using Dopamine.Core.Base;
using Dopamine.Core.Database;
using Dopamine.Core.Database.Entities;
using Dopamine.Core.Database.Repositories.Interfaces;
using Dopamine.Core.Helpers;
using Dopamine.Core.Logging;
using Dopamine.Core.Prism;
using Dopamine.Core.Settings;
using Dopamine.Core.Utils;
using Prism.Commands;
using Prism.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Dopamine.CollectionModule.ViewModels
{
    public class CollectionArtistsViewModel : CommonAlbumsViewModel, ISemanticZoomViewModel
    {
        #region Variables
        // Repositories
        private IArtistRepository artistRepository;

        // Lists
        private ObservableCollection<ISemanticZoomable> artists;
        private CollectionViewSource artistsCvs;
        private IList<Artist> selectedArtists;
        private ObservableCollection<ISemanticZoomSelector> artistsZoomSelectors;

        // Flags
        private bool isArtistsZoomVisible;

        // Other
        private long artistsCount;
        private SubscriptionToken shellMouseUpToken;
        private ArtistType artistType;
        private string artistTypeText;
        private double leftPaneWidthPercent;
        private double rightPaneWidthPercent;
        #endregion

        #region Commands
        public DelegateCommand<string> AddArtistsToPlaylistCommand { get; set; }
        public DelegateCommand<object> SelectedArtistsCommand { get; set; }
        public DelegateCommand ShowArtistsZoomCommand { get; set; }
        public DelegateCommand SemanticJumpCommand { get; set; }
        public DelegateCommand ToggleArtistTypeCommand { get; set; }
        public DelegateCommand AddArtistsToNowPlayingCommand { get; set; }
        #endregion

        #region Properties
        public double LeftPaneWidthPercent
        {
            get { return this.leftPaneWidthPercent; }
            set
            {
                SetProperty<double>(ref this.leftPaneWidthPercent, value);
                XmlSettingsClient.Instance.Set<int>("ColumnWidths", "ArtistsLeftPaneWidthPercent", Convert.ToInt32(value));
            }
        }

        public double RightPaneWidthPercent
        {
            get { return this.rightPaneWidthPercent; }
            set
            {
                SetProperty<double>(ref this.rightPaneWidthPercent, value);
                XmlSettingsClient.Instance.Set<int>("ColumnWidths", "ArtistsRightPaneWidthPercent", Convert.ToInt32(value));
            }
        }

        public ObservableCollection<ISemanticZoomable> Artists
        {
            get { return this.artists; }
            set { SetProperty<ObservableCollection<ISemanticZoomable>>(ref this.artists, value); }
        }
        ObservableCollection<ISemanticZoomable> ISemanticZoomViewModel.SemanticZoomables
        {
            get { return Artists; }
            set { Artists = value; }
        }

        public CollectionViewSource ArtistsCvs
        {
            get { return this.artistsCvs; }
            set { SetProperty<CollectionViewSource>(ref this.artistsCvs, value); }
        }

        public IList<Artist> SelectedArtists
        {
            get { return this.selectedArtists; }
            set { SetProperty<IList<Artist>>(ref this.selectedArtists, value); }
        }

        public long ArtistsCount
        {
            get { return this.artistsCount; }
            set { SetProperty<long>(ref this.artistsCount, value); }
        }

        public bool IsArtistsZoomVisible
        {
            get { return this.isArtistsZoomVisible; }
            set { SetProperty<bool>(ref this.isArtistsZoomVisible, value); }
        }

        public ObservableCollection<ISemanticZoomSelector> ArtistsZoomSelectors
        {
            get { return this.artistsZoomSelectors; }
            set { SetProperty<ObservableCollection<ISemanticZoomSelector>>(ref this.artistsZoomSelectors, value); }
        }
        ObservableCollection<ISemanticZoomSelector> ISemanticZoomViewModel.SemanticZoomSelectors
        {
            get { return ArtistsZoomSelectors; }
            set { ArtistsZoomSelectors = value; }
        }

        public ArtistType ArtistType
        {
            get { return this.artistType; }
            set
            {
                SetProperty<ArtistType>(ref this.artistType, value);
                this.UpdateArtistType(value);
            }
        }

        public string ArtistTypeText
        {
            get { return this.artistTypeText; }
        }

        public override bool CanOrderByAlbum
        {
            get
            {
                return (this.SelectedArtists != null && this.SelectedArtists.Count > 0) |
                     (this.SelectedAlbums != null && this.SelectedAlbums.Count > 0);
            }
        }
        #endregion

        #region Construction
        public CollectionArtistsViewModel(IArtistRepository artistRepository) : base()
        {
            // Repositories
            this.artistRepository = artistRepository;

            // Commands
            this.ToggleTrackOrderCommand = new DelegateCommand(async () => await this.ToggleTrackOrderAsync());
            this.ToggleAlbumOrderCommand = new DelegateCommand(async () => await this.ToggleAlbumOrderAsync());
            this.RemoveSelectedTracksCommand = new DelegateCommand(async () => await this.RemoveTracksFromCollectionAsync(this.SelectedTracks), () => !this.IsIndexing);
            this.AddArtistsToPlaylistCommand = new DelegateCommand<string>(async (iPlaylistName) => await this.AddArtistsToPlaylistAsync(this.SelectedArtists, iPlaylistName));
            this.SelectedArtistsCommand = new DelegateCommand<object>(async (iParameter) => await this.SelectedArtistsHandlerAsync(iParameter));
            this.ShowArtistsZoomCommand = new DelegateCommand(async () => await this.ShowSemanticZoomAsync());
            this.SemanticJumpCommand = new DelegateCommand(() => this.HideSemanticZoom());
            this.ToggleArtistTypeCommand = new DelegateCommand(async () => await this.ToggleArtistTypeAsync());
            this.AddArtistsToNowPlayingCommand = new DelegateCommand(async () => await this.AddArtistsToNowPlayingAsync(this.SelectedArtists));

            // Events
            this.eventAggregator.GetEvent<SettingEnableRatingChanged>().Subscribe(async (enableRating) =>
            {
                this.EnableRating = enableRating;
                this.SetTrackOrder("ArtistsTrackOrder");
                await this.GetTracksAsync(this.SelectedArtists, null, this.SelectedAlbums, this.TrackOrder);
            });

            this.eventAggregator.GetEvent<SettingUseStarRatingChanged>().Subscribe((useStarRating) => this.UseStarRating = useStarRating);

            // MetadataService
            this.metadataService.MetadataChanged += MetadataChangedHandlerAsync;

            // IndexingService
            this.indexingService.RefreshArtwork += async (_, __) => await this.collectionService.RefreshArtworkAsync(this.Albums, this.Tracks);

            // Set the initial ArtistType
            this.ArtistType = (ArtistType)XmlSettingsClient.Instance.Get<int>("Ordering", "ArtistType");

            // Set the initial AlbumOrder
            this.AlbumOrder = (AlbumOrder)XmlSettingsClient.Instance.Get<int>("Ordering", "ArtistsAlbumOrder");

            // Set the initial TrackOrder
            this.SetTrackOrder("ArtistsTrackOrder");

            // Subscribe to Events and Commands on creation
            this.Subscribe();

            // Set width of the panels
            this.LeftPaneWidthPercent = XmlSettingsClient.Instance.Get<int>("ColumnWidths", "ArtistsLeftPaneWidthPercent");
            this.RightPaneWidthPercent = XmlSettingsClient.Instance.Get<int>("ColumnWidths", "ArtistsRightPaneWidthPercent");

            // Cover size
            this.SetCoversizeAsync((CoverSizeType)XmlSettingsClient.Instance.Get<int>("CoverSizes", "ArtistsCoverSize"));
        }
        #endregion

        #region ISemanticZoomViewModel
        public async Task ShowSemanticZoomAsync()
        {
            this.ArtistsZoomSelectors = await SemanticZoomUtils.UpdateSemanticZoomSelectors(this.ArtistsCvs.View);

            this.IsArtistsZoomVisible = true;
        }

        public void HideSemanticZoom()
        {
            this.IsArtistsZoomVisible = false;
        }

        public void UpdateSemanticZoomHeaders()
        {
            string previousHeader = string.Empty;

            foreach (ArtistViewModel avm in this.ArtistsCvs.View)
            {
                if (string.IsNullOrEmpty(previousHeader) || !avm.Header.Equals(previousHeader))
                {
                    previousHeader = avm.Header;
                    avm.IsHeader = true;
                }else
                {
                    avm.IsHeader = false;
                }
            }
        }
        #endregion

        #region Private
        private async void MetadataChangedHandlerAsync(MetadataChangedEventArgs e)
        {
            if (e.IsAlbumArtworkMetadataChanged)
            {
                await this.collectionService.RefreshArtworkAsync(this.Albums, this.Tracks);
            }

            if (e.IsArtistMetadataChanged | (e.IsAlbumArtistMetadataChanged & (this.ArtistType == ArtistType.Album | this.ArtistType == ArtistType.All)))
            {
                await this.GetArtistsAsync(this.ArtistType);
            }

            if (e.IsArtistMetadataChanged | e.IsAlbumTitleMetadataChanged | e.IsAlbumArtistMetadataChanged | e.IsAlbumYearMetadataChanged)
            {
                await this.GetAlbumsAsync(this.SelectedArtists, null, this.AlbumOrder);
            }

            if (e.IsArtistMetadataChanged | e.IsAlbumTitleMetadataChanged | e.IsAlbumArtistMetadataChanged | e.IsTrackMetadataChanged)
            {
                await this.GetTracksAsync(this.SelectedArtists, null, this.SelectedAlbums, this.TrackOrder);
            }
        }

        private async Task GetArtistsAsync(ArtistType artistType)
        {
            try
            {
                // Get Artists from database
                List<Artist> artists = await this.artistRepository.GetArtistsAsync(artistType);

                // Create new ObservableCollection
                ObservableCollection<ArtistViewModel> artistViewModels = new ObservableCollection<ArtistViewModel>();

                await Task.Run(() =>
                {
                    List<ArtistViewModel> tempArtistViewModels = new List<ArtistViewModel>();

                    // Workaround to make sure the "#" GroupHeader is shown at the top of the list
                    tempArtistViewModels.AddRange(artists.Select(art => new ArtistViewModel { Artist = art, IsHeader = false }).Where(avm => avm.Header.Equals("#")));
                    tempArtistViewModels.AddRange(artists.Select(art => new ArtistViewModel { Artist = art, IsHeader = false }).Where(avm => !avm.Header.Equals("#")));

                    foreach (ArtistViewModel avm in tempArtistViewModels)
                    {
                        artistViewModels.Add(avm);
                    }
                });

                // Unbind to improve UI performance
                if (this.ArtistsCvs != null) this.ArtistsCvs.Filter -= new FilterEventHandler(ArtistsCvs_Filter);
                this.Artists = null;
                this.ArtistsCvs = null;

                // Populate ObservableCollection
                this.Artists = new ObservableCollection<ISemanticZoomable>(artistViewModels);
            }
            catch (Exception ex)
            {
                LogClient.Instance.Logger.Error("An error occured while getting Artists. Exception: {0}", ex.Message);

                // Failed getting Artists. Create empty ObservableCollection.
                this.Artists = new ObservableCollection<ISemanticZoomable>();
            }

            // Populate CollectionViewSource
            this.ArtistsCvs = new CollectionViewSource { Source = this.Artists };
            this.ArtistsCvs.Filter += new FilterEventHandler(ArtistsCvs_Filter);

            // Update count
            this.ArtistsCount = this.ArtistsCvs.View.Cast<ISemanticZoomable>().Count();

            // Update Semantic Zoom Headers
            this.UpdateSemanticZoomHeaders();
        }

        private async Task SelectedArtistsHandlerAsync(object parameter)
        {
            if (parameter != null)
            {
                this.SelectedArtists = new List<Artist>();

                foreach (ArtistViewModel item in (IList)parameter)
                {
                    this.SelectedArtists.Add(item.Artist);
                }
            }

            // Don't reload the lists when updating Metadata. MetadataChangedHandlerAsync handles that.
            if (this.metadataService.IsUpdatingDatabaseMetadata) return;

            await this.GetAlbumsAsync(this.SelectedArtists, null, this.AlbumOrder);
            this.SetTrackOrder("ArtistsTrackOrder");
            await this.GetTracksAsync(this.SelectedArtists, null, this.SelectedAlbums, this.TrackOrder);
        }

        private async Task AddArtistsToPlaylistAsync(IList<Artist> artists, string playlistName)
        {

            AddToPlaylistResult result = await this.collectionService.AddArtistsToPlaylistAsync(artists, playlistName);

            if (!result.IsSuccess)
            {
                this.dialogService.ShowNotification(
                    0xe711,
                    16,
                    ResourceUtils.GetStringResource("Language_Error"),
                    ResourceUtils.GetStringResource("Language_Error_Adding_Artists_To_Playlist").Replace("%playlistname%", "\"" + playlistName + "\""),
                    ResourceUtils.GetStringResource("Language_Ok"),
                    true,
                    ResourceUtils.GetStringResource("Language_Log_File"));
            }
        }

        protected async Task AddArtistsToNowPlayingAsync(IList<Artist> artists)
        {
            AddToQueueResult result = await this.playbackService.AddToQueue(artists);

            if (!result.IsSuccess)
            {
                this.dialogService.ShowNotification(0xe711, 16, ResourceUtils.GetStringResource("Language_Error"), ResourceUtils.GetStringResource("Language_Error_Adding_Artists_To_Now_Playing"), ResourceUtils.GetStringResource("Language_Ok"), true, ResourceUtils.GetStringResource("Language_Log_File"));
            }
        }

        private async Task ToggleArtistTypeAsync()
        {
            this.HideSemanticZoom();

            switch (this.ArtistType)
            {
                case ArtistType.All:
                    this.ArtistType = ArtistType.Track;
                    break;
                case ArtistType.Track:
                    this.ArtistType = ArtistType.Album;
                    break;
                case ArtistType.Album:
                    this.ArtistType = ArtistType.All;
                    break;
                default:
                    // Cannot happen, but just in case.
                    this.ArtistType = ArtistType.All;
                    break;
            }

            XmlSettingsClient.Instance.Set<int>("Ordering", "ArtistType", (int)this.ArtistType);
            await this.GetArtistsAsync(this.ArtistType);
        }

        private void ArtistsCvs_Filter(object sender, FilterEventArgs e)
        {
            ArtistViewModel avm = e.Item as ArtistViewModel;

            e.Accepted = Dopamine.Core.Database.Utils.FilterArtists(avm.Artist, this.searchService.SearchText);
        }
        #endregion

        #region Protected
        protected void UpdateArtistType(ArtistType artistType)
        {
            switch (artistType)
            {
                case ArtistType.All:
                    this.artistTypeText = ResourceUtils.GetStringResource("Language_All");
                    break;
                case ArtistType.Track:
                    this.artistTypeText = ResourceUtils.GetStringResource("Language_Song");
                    break;
                case ArtistType.Album:
                    this.artistTypeText = ResourceUtils.GetStringResource("Language_Album");
                    break;
                default:
                    // Cannot happen, but just in case.
                    this.artistTypeText = ResourceUtils.GetStringResource("Language_All");
                    break;
            }

            OnPropertyChanged(() => this.ArtistTypeText);
        }

        protected async Task ToggleTrackOrderAsync()
        {
            base.ToggleTrackOrder();

            XmlSettingsClient.Instance.Set<int>("Ordering", "ArtistsTrackOrder", (int)this.TrackOrder);
            await this.GetTracksCommonAsync(this.Tracks.Select((t) => t.TrackInfo).ToList(), this.TrackOrder);
        }

        protected async Task ToggleAlbumOrderAsync()
        {

            base.ToggleAlbumOrder();

            XmlSettingsClient.Instance.Set<int>("Ordering", "ArtistsAlbumOrder", (int)this.AlbumOrder);
            await this.GetAlbumsCommonAsync(this.Albums.Select((a) => a.Album).ToList(), this.AlbumOrder);
        }
        #endregion

        #region Overrides
        protected async override Task SetCoversizeAsync(CoverSizeType coverSize)
        {
            await base.SetCoversizeAsync(coverSize);
            XmlSettingsClient.Instance.Set<int>("CoverSizes", "ArtistsCoverSize", (int)coverSize);
        }

        protected async override Task FillListsAsync()
        {
            await this.GetArtistsAsync(this.ArtistType);
            await this.GetAlbumsAsync(null, null, this.AlbumOrder);
            await this.GetTracksAsync(null, null, null, this.TrackOrder);
        }

        protected override void FilterLists()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Artists
                if (this.ArtistsCvs != null)
                {
                    this.ArtistsCvs.View.Refresh();
                    this.ArtistsCount = this.ArtistsCvs.View.Cast<ISemanticZoomable>().Count();
                    this.UpdateSemanticZoomHeaders();
                }
            });

            base.FilterLists();
        }

        protected async override Task SelectedAlbumsHandlerAsync(object parameter)
        {
            await base.SelectedAlbumsHandlerAsync(parameter);

            this.SetTrackOrder("ArtistsTrackOrder");
            await this.GetTracksAsync(this.SelectedArtists, null, this.SelectedAlbums, this.TrackOrder);
        }

        protected override void Unsubscribe()
        {
            // Commands
            ApplicationCommands.RemoveSelectedTracksCommand.UnregisterCommand(this.RemoveSelectedTracksCommand);
            ApplicationCommands.SemanticJumpCommand.UnregisterCommand(this.SemanticJumpCommand);

            // Events
            this.eventAggregator.GetEvent<ShellMouseUp>().Unsubscribe(this.shellMouseUpToken);

            // Other
            this.IsArtistsZoomVisible = false;
        }

        protected override void Subscribe()
        {
            // Prevents subscribing twice
            this.Unsubscribe();

            // Commands
            ApplicationCommands.RemoveSelectedTracksCommand.RegisterCommand(this.RemoveSelectedTracksCommand);
            ApplicationCommands.SemanticJumpCommand.RegisterCommand(this.SemanticJumpCommand);

            // Events
            this.shellMouseUpToken = this.eventAggregator.GetEvent<ShellMouseUp>().Subscribe((_) => this.IsArtistsZoomVisible = false);
        }

        protected override void RefreshLanguage()
        {
            this.UpdateArtistType(this.ArtistType);
            this.UpdateAlbumOrderText(this.AlbumOrder);
            this.UpdateTrackOrderText(this.TrackOrder);
        }
        #endregion
    }
}
