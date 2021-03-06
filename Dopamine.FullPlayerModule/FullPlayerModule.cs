﻿using Dopamine.ControlsModule.Views;
using Dopamine.Core.Prism;
using Dopamine.FullPlayerModule.ViewModels;
using Dopamine.FullPlayerModule.Views;
using Prism.Modularity;
using Prism.Regions;
using Microsoft.Practices.Unity;

namespace Dopamine.FullPlayerModule
{
    public class FullPlayerModule : IModule
    {
        #region Variables
        private readonly IRegionManager regionManager;
        private IUnityContainer container;
        #endregion

        #region Construction
        public FullPlayerModule(IRegionManager regionManager, IUnityContainer container)
        {
            this.regionManager = regionManager;
            this.container = container;
        }
        #endregion

        #region IModule
        public void Initialize()
        {
            this.container.RegisterType<object, FullPlayer>(typeof(FullPlayer).FullName);
            this.container.RegisterType<object, FullPlayerViewModel>(typeof(FullPlayerViewModel).FullName);
            this.container.RegisterType<object, MainMenu>(typeof(MainMenu).FullName);
            this.container.RegisterType<object, MainMenuViewModel>(typeof(MainMenuViewModel).FullName);
            this.container.RegisterType<object, Status>(typeof(Status).FullName);
            this.container.RegisterType<object, StatusViewModel>(typeof(StatusViewModel).FullName);
            this.container.RegisterType<object, MainScreen>(typeof(MainScreen).FullName);
            this.container.RegisterType<object, MainScreenViewModel>(typeof(MainScreenViewModel).FullName);
            this.container.RegisterType<object, NowPlayingScreen>(typeof(NowPlayingScreen).FullName);
            this.container.RegisterType<object, NowPlayingScreenViewModel>(typeof(NowPlayingScreenViewModel).FullName);
            this.container.RegisterType<object, NowPlayingScreenShowcase>(typeof(NowPlayingScreenShowcase).FullName);
            this.container.RegisterType<object, NowPlayingScreenShowcaseViewModel>(typeof(NowPlayingScreenShowcaseViewModel).FullName);
            this.container.RegisterType<object, NowPlayingScreenList>(typeof(NowPlayingScreenList).FullName);
            this.container.RegisterType<object, NowPlayingScreenListViewModel>(typeof(NowPlayingScreenListViewModel).FullName);

            this.regionManager.RegisterViewWithRegion(RegionNames.ScreenTypeRegion, typeof(Views.MainScreen));
            this.regionManager.RegisterViewWithRegion(RegionNames.StatusRegion, typeof(Views.Status));
            this.regionManager.RegisterViewWithRegion(RegionNames.MainMenuRegion, typeof(Views.MainMenu));
            this.regionManager.RegisterViewWithRegion(RegionNames.NowPlayingPlaybackControlsRegion, typeof(NowPlayingPlaybackControls));
            this.regionManager.RegisterViewWithRegion(RegionNames.NowPlayingSpectrumAnalyzerRegion, typeof(SpectrumAnalyzerControl));
            this.regionManager.RegisterViewWithRegion(RegionNames.FullPlayerSearchRegion, typeof(SearchControl));
        }
        #endregion
    }
}
