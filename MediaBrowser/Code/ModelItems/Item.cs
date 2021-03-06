﻿using System;
using System.Collections.Generic;
using Microsoft.MediaCenter.UI;
using System.Diagnostics;
using Microsoft.MediaCenter;
using MediaBrowser.Code.ModelItems;
using System.IO;
using MediaBrowser.Library.Playables;
using MediaBrowser.Library.Entities;
using MediaBrowser.Code;
using System.Threading;
using MediaBrowser.Library.Factories;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.Metadata;
using MediaBrowser.Library.RemoteControl;
using MediaBrowser.Library.Logging;
using System.Linq;


namespace MediaBrowser.Library
{

    public partial class Item : BaseModelItem
    {
        static Item blank;
        static Dictionary<Type, ItemType> itemTypeMap;
        static Item()
        {
            itemTypeMap = new Dictionary<Type, ItemType>();
            itemTypeMap[typeof(Episode)] = ItemType.Episode;
            itemTypeMap[typeof(Movie)] = ItemType.Movie;
            itemTypeMap[typeof(Series)] = ItemType.Series;
            itemTypeMap[typeof(Season)] = ItemType.Season;
            itemTypeMap[typeof(MediaBrowser.Library.Entities.Folder)] = ItemType.Folder;

            blank = new Item();
            BaseItem item = new BaseItem();
            item.Path = "";
            item.Name = "";
            blank.Assign(item);

        }

        object loadMetadatLock = new object();
        protected object watchLock = new object();

        PlayableItem playable;
        private PlaybackStatus playstate;
        protected BaseItem baseItem;

        protected int unwatchedCountCache = -1;


        #region Item Construction
        internal Item()
        {
        }

        internal virtual void Assign(BaseItem baseItem)
        {
            this.baseItem = baseItem;
            baseItem.MetadataChanged += new EventHandler<MetadataChangedEventArgs>(MetadataChanged);
        }

        #endregion

        public BaseItem BaseItem { get { return baseItem; } }

        public FolderModel PhysicalParent { get; internal set; }

        private FolderModel TopParent
        {
            get
            {
                if (PhysicalParent != null && !PhysicalParent.IsRoot)
                {
                    return PhysicalParent.TopParent;
                }
                else
                {
                    return this as FolderModel;
                }
            }
        }

        public Guid Id { get { return baseItem.Id; } }

        public virtual void NavigatingInto()
        {
        }

        public bool IsVideo
        {
            get
            {
                return (baseItem is Video);
            }
        }

        public bool IsNotVideo
        {
            get
            {
                bool isVideo = (baseItem is Video) || (baseItem is Movie);
                return (baseItem is Folder) ? !((baseItem as Folder).HasVideoChildren) : !isVideo;
            }
        }

        // having this in Item and not in Folder helps us avoid lots of messy mcml 
        public virtual bool ShowNewestItems
        {
            get
            {
                return false;
            }
        }

        public string Name
        {
            get
            {
                return BaseItem.Name;
            }
        }

        public string LongName
        {
            get
            {
                return BaseItem.LongName;
            }
        }

        public string Path
        {
            get
            {
                return baseItem.Path;
            }
        }

        public DateTime CreatedDate
        {
            get
            {
                return baseItem.DateCreated;
            }
        }

        public string CreatedDateString
        {
            get
            {
                return baseItem.DateCreated.ToShortDateString();
            }
        }


        public ItemType ItemType
        {
            get
            {
                ItemType type;
                if (!itemTypeMap.TryGetValue(baseItem.GetType(), out type))
                {
                    type = ItemType.None;
                }
                return type;
            }
        }

        public string ItemTypeString
        {
            get
            {
                string[] items = BaseItem.GetType().ToString().Split('.');
                return items[items.Length - 1];
            }
        }

        public string MediaTypeString
        {
            get {
                if (this.BaseItem.DisplayMediaType != null)
                {
                    return this.BaseItem.DisplayMediaType;
                }
                else
                {
                    string mediaType = "";
                    var video = baseItem as Video;
                    if (video != null)
                    {
                        mediaType = video.MediaType.ToString().ToLower();
                    }
                    return mediaType;
                }
            }
        }

        public bool IsRoot
        {
            get
            {
                return baseItem.Id == Application.CurrentInstance.RootFolder.Id;
            }
        }

        public bool IsRemoteContent
        {
            get { return baseItem.IsRemoteContent; }
        }

        public bool SelectAction()
        {
            if (this.BaseItem != null)
            {
                return BaseItem.SelectAction(this);
            }
            {
                Logger.ReportWarning("BaseItem null in request to navigate to " + this.Name);
                return false;
            }
        }

        #region Playback

        public bool PlayAction()
        {
            if (this.BaseItem != null)
            {
                return BaseItem.PlayAction(this);
            }
            else
            {
                Logger.ReportWarning("BaseItem null in request to play " + this.Name);
                return false;
            }
        }

        public bool SupportsMultiPlay
        {
            get
            {
                return baseItem is Folder;
            }
        }

        public bool ParentalAllowed { get { return Kernel.Instance.ParentalControls.Allowed(this); } }
        public string ParentalRating
        {
            get
            {
                return baseItem.ParentalRating;
            }
        }

        private void Play(bool resume, bool queue)
        {
            if (this.IsPlayable || this.IsFolder)
            {
                if (Config.Instance.ParentalControlEnabled && !this.ParentalAllowed)
                {
                    Application.CurrentInstance.DisplayPopupPlay = false; //PIN screen mucks with turning this off
                    Kernel.Instance.ParentalControls.PlayProtected(this, resume, queue);
                }
                else PlaySecure(resume, queue);
            }
        }

        public void PlaySecure(bool resume, bool queue)
        {
            try
            {
                if (this.IsPlayable || this.IsFolder)
                {

                    if (PlayableItem.PlaybackController != Application.CurrentInstance.PlaybackController && PlayableItem.PlaybackController.RequiresExternalPage)
                    {
                        Application.CurrentInstance.OpenExternalPlaybackPage(this);
                    }
                    this.PlayableItem.QueueItem = queue;
                    this.PlayableItem.Play(this.PlayState, resume);
                    if (!this.IsFolder && this.TopParent != null) this.TopParent.AddNewlyWatched(this); //add to recent watched list if not a whole folder
                    Async.Queue("Resume state updater", () =>
                    {
                        Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => FirePropertyChanged("CanResume")); //force UI to update
                    }, 10 * 1000);
                }
            }
            catch (Exception)
            {
                MediaCenterEnvironment ev = Microsoft.MediaCenter.Hosting.AddInHost.Current.MediaCenterEnvironment;
                ev.Dialog(Application.CurrentInstance.StringData("ContentErrorDial") + "\n" + baseItem.Path, Application.CurrentInstance.StringData("ContentErrorCapDial"), DialogButtons.Ok, 60, true);
            }
        }

        private void Play(bool resume)
        {
            Play(resume, false);
        }


        public void Queue()
        {
            Play(false, true);
        }

        public void Play()
        {
            Play(false);
        }
        public void Resume()
        {
            Play(true);
        }

        public bool CanResume
        {
            get
            {
                return PlayState == null ? false : PlayState.CanResume;
            }
        }
        public string RecentDateString
        {
            get
            {
                switch (Application.CurrentInstance.RecentItemOption)
                {
                    case "watched":
                        string runTimeStr = "";
                        string watchTimeStr = "";
                        if (this.PlayState.PositionTicks > 0)
                        {
                            TimeSpan watchTime = new TimeSpan(this.PlayState.PositionTicks);
                            watchTimeStr = " " + watchTime.TotalMinutes.ToString("F0") + " ";
                            if (!String.IsNullOrEmpty(this.RunningTimeString))
                            {
                                runTimeStr = Kernel.Instance.StringData.GetString("OfEHS") + " " + RunningTimeString;
                            }
                            else
                            {
                                runTimeStr = "mins"; //have watched time but not running time so tack on 'mins'
                            }
                        }
                        return Kernel.Instance.StringData.GetString("WatchedEHS") + watchTimeStr + runTimeStr + " " +
                            Kernel.Instance.StringData.GetString("OnEHS") + " " + LastPlayedString;
                    default:
                        return Kernel.Instance.StringData.GetString("AddedOnEHS") + " " + CreatedDateString;
                }
            }
        }
        public void RecentItemsChanged()
        {
            FirePropertyChanged("QuickListItems");
        }
        public string LastPlayedString
        {
            get
            {
                if (PlayState == null) return "";
                return PlayState.LastPlayed == DateTime.MinValue ? "" : PlayState.LastPlayed.ToShortDateString();
            }
        }

        private PlaybackStatus PlayState
        {
            get
            {
                if (playstate == null)
                {

                    Media media = baseItem as Media;

                    if (media != null)
                    {
                        playstate = media.PlaybackStatus;
                        // if we want any chance to reclaim memory we are going to have to use 
                        // weak event handlers
                        playstate.WasPlayedChanged += new EventHandler<EventArgs>(PlaybackStatusPlayedChanged);
                        PlaybackStatusPlayedChanged(this, null);
                    }
                }
                return playstate;
            }
        }

        void PlaybackStatusPlayedChanged(object sender, EventArgs e)
        {
            lock (watchLock)
                unwatchedCountCache = -1;
            FirePropertyChanged("HaveWatched");
            FirePropertyChanged("UnwatchedCount");
            FirePropertyChanged("ShowUnwatched");
            FirePropertyChanged("UnwatchedCountString");
            FirePropertyChanged("PlayState");
        }

        #endregion

        #region watch tracking

        public bool HaveWatched
        {
            get
            {
                return UnwatchedCount == 0;
            }
        }

        public bool ShowUnwatched
        {
            get { return ((Config.Instance.ShowUnwatchedCount) && (this.UnwatchedCountString.Length > 0)); }
        }

        public string UnwatchedCountString
        {
            get
            {
                if (this.IsPlayable)
                    return "";
                int i = this.UnwatchedCount;
                return (i == 0) ? "" : i.ToString();
            }
        }

        public virtual int UnwatchedCount
        {
            get
            {
                int count = 0;
                if (baseItem is Video)
                {
                    var video = baseItem as Video;
                    if (video != null && !video.PlaybackStatus.WasPlayed)
                    {
                        count = 1;
                    }
                }
                return count;
            }
        }

        public void ToggleWatched()
        {
            Logger.ReportVerbose("Start ToggleWatched() initial value: " + HaveWatched.ToString());
            SetWatched(!this.HaveWatched);
            lock (watchLock)
                unwatchedCountCache = -1;
            FirePropertyChanged("HaveWatched");
            FirePropertyChanged("UnwatchedCount");
            FirePropertyChanged("ShowUnwatched");
            FirePropertyChanged("UnwatchedCountString");
            Logger.ReportVerbose("  ToggleWatched() changed to: " + HaveWatched.ToString());
            //HACK: This sort causes errors in detail lists, further debug necessary
            //this.PhysicalParent.Children.Sort();
        }

        internal virtual void SetWatched(bool value)
        {
            if (IsPlayable)
            {
                if (value != HaveWatched)
                {
                    if (value && PlayState.PlayCount == 0)
                    {
                        PlayState.PlayCount = 1;
                        //remove ourselves from the unwatched list as well
                        if (this.PhysicalParent != null)
                        {
                            this.PhysicalParent.RemoveRecentlyUnwatched(this); //thought about asynch'ing this but its a list of 20 items...
                        }
                        //don't add to watched list as we didn't really watch it (and it might just clutter up the list)
                        Application.CurrentInstance.Information.AddInformationString(string.Format(Application.CurrentInstance.StringData("SetWatchedProf"), this.Name));
                    }
                    else
                    {
                        PlayState.PlayCount = 0;
                        //remove ourselves from the watched list as well
                        if (this.PhysicalParent != null)
                        {
                            this.PhysicalParent.RemoveNewlyWatched(this); //thought about asynch'ing this but its a list of 20 items...
                        }
                        Application.CurrentInstance.Information.AddInformationString(string.Format(Application.CurrentInstance.StringData("ClearWatchedProf"), this.Name));
                    }
                    PlayState.Save();
                    lock (watchLock)
                        unwatchedCountCache = -1;
                }
            }

        }

        #endregion


        #region Metadata loading and refresh

        public void RefreshMetadata()
        {
            Application.CurrentInstance.Information.AddInformationString(Application.CurrentInstance.StringData("RefreshProf") + " " + this.Name);
            Async.Queue("UI Triggered Metadata Loader", () =>
            {
                baseItem.RefreshMetadata(MetadataRefreshOptions.Force);
                // force images to reload
                primaryImage = null;
                bannerImage = null;
                primaryImageSmall = null;
                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ => this.FireAllPropertiesChanged());
            });
        }

        #endregion


        public bool IsPlayable
        {
            get
            {
                return baseItem is Media;
            }
        }

        public bool IsFolder
        {
            get
            {
                return baseItem is Folder;
            }
        }

        public FolderModel Season
        {
            get
            {

                FolderModel season = null;
                Episode episode = baseItem as Episode;
                FolderModel parent = PhysicalParent;

                if (episode != null)
                {
                    season = ItemFactory.Instance.Create(episode.Season) as FolderModel;
                }

                return season;
            }
        }


        public FolderModel Series
        {
            get
            {

                FolderModel series = null;

                Episode episode = baseItem as Episode;
                Season season = baseItem as Season;

                FolderModel parent = PhysicalParent;
                FolderModel grandParent = null;

                if (parent != null)
                {
                    grandParent = PhysicalParent.PhysicalParent;
                }
                /*
                if (parent != null && parent.baseItem is Series) {
                    series = parent;
                }
                */
                if (series == null)
                {

                    if (episode != null)
                    {

                        if (grandParent != null && grandParent.baseItem is Series)
                        {
                            series = grandParent;
                        }

                        if (series == null)
                        {
                            series = ItemFactory.Instance.Create(episode.Series) as FolderModel;
                        }

                    }
                    else if (series != null)
                    {
                        series = ItemFactory.Instance.Create(season.Parent) as FolderModel;

                    }
                }
                return series;
            }
        }

        public string SeasonNumber
        {
            get
            {
                Episode episode = baseItem as Episode;
                if (episode != null)
                {
                    if (episode.SeasonNumber != null)
                        return episode.SeasonNumber;
                    else
                        return "";
                }
                else
                    return "";
            }
        }

        public string EpisodeNumber
        {
            get
            {
                Episode episode = baseItem as Episode;
                if (episode != null)
                {
                    if (episode.EpisodeNumber != null)
                        return episode.EpisodeNumber;
                    else
                        return "";
                }
                else
                    return "";
            }
        }

        // this is a shortcut for MCML
        public void ProcessCommand(RemoteCommand command)
        {
            PlayableItem.PlaybackController.ProcessCommand(command);
        }

        public IPlaybackController PlaybackController
        {
            get
            {
                return this.PlayableItem.PlaybackController;
            }
        }

        internal PlayableItem PlayableItem
        {
            get
            {
                if (!IsPlayable && !IsFolder) return null;

                Media media = baseItem as Media;

                if (media != null && playable == null)
                    lock (this)
                        if (playable == null)
                        {
                            playable = PlayableItemFactory.Instance.Create(media);
                        }

                if (playable != null)
                    return playable;

                Folder folder = baseItem as Folder;
                if (folder != null && playable == null)
                    lock (this)
                        if (playable == null)
                        {
                            playable = PlayableItemFactory.Instance.Create(folder);
                        }

                return playable;
            }
        }

        public bool ContainsTrailers
        {
            get
            {
                var movie = BaseItem as Movie;
                return (movie != null && movie.ContainsTrailers);
            }
        }

        public void PlayTrailers()
        {
            var movie = BaseItem as Movie;
            if (movie.ContainsTrailers)
            {
                var trailerFiles = movie.TrailerFiles.ToArray();
                string filename = null;
                if (trailerFiles.Length == 1)
                {
                    filename = trailerFiles[0];
                }
                if (trailerFiles.Length > 1)
                {
                    filename = PlayableItem.CreateWPLPlaylist(BaseItem.Name, trailerFiles);
                }

                if (filename != null)
                {
                    foreach (var controller in Kernel.Instance.PlaybackControllers)
                    {
                        if (controller.CanPlay(filename))
                        {
                            controller.PlayMedia(filename);
                            controller.GoToFullScreen();
                            break;
                        }
                    }
                }
            }
        }

        #region Dynamic Data Support
        class FakeLateBoundOldDictionary : System.Collections.IDictionary
        {

            BaseItem item;

            public FakeLateBoundOldDictionary(BaseItem item)
            {
                this.item = item;
            }

            public void Add(object key, object value)
            {
                throw new NotImplementedException();
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool Contains(object key)
            {
                throw new NotImplementedException();
            }

            public System.Collections.IDictionaryEnumerator GetEnumerator()
            {
                throw new NotImplementedException();
            }

            public bool IsFixedSize
            {
                get { throw new NotImplementedException(); }
            }

            public bool IsReadOnly
            {
                get { throw new NotImplementedException(); }
            }

            public System.Collections.ICollection Keys
            {
                get { throw new NotImplementedException(); }
            }

            public void Remove(object key)
            {
                throw new NotImplementedException();
            }

            public System.Collections.ICollection Values
            {
                get { throw new NotImplementedException(); }
            }

            public object this[object key]
            {
                get
                {
                    return item.GetType().GetProperty(key.ToString()).GetValue(item, null);
                }
                set
                {
                    throw new NotImplementedException();
                }
            }


            public void CopyTo(Array array, int index)
            {
                throw new NotImplementedException();
            }

            public int Count
            {
                get { throw new NotImplementedException(); }
            }

            public bool IsSynchronized
            {
                get { throw new NotImplementedException(); }
            }

            public object SyncRoot
            {
                get { throw new NotImplementedException(); }
            }


            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }

        }

        public System.Collections.IDictionary DynamicProperties
        {
            get
            {
                return new FakeLateBoundOldDictionary(this.BaseItem);
            }
        }

    }
        #endregion


}
