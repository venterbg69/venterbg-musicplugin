﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Persistance;

namespace MediaBrowser.Library.Entities {
    public class Series : Folder, IShow {

        [Persist]
        public string MpaaRating { get; set; }

        [Persist]
        public Single? ImdbRating { get; set; }

        [Persist]
        public List<Actor> Actors { get; set; }

        [Persist]
        public List<string> Directors { get; set; }

        [Persist]
        public List<string> Genres { get; set; }

        [Persist]
        public List<string> Studios { get; set; }

        [Persist]
        public int? RunningTime { get; set; }

        [Persist]
        public string Status { get; set; }

        [Persist]
        public string TVDBSeriesId { get; set; }

        [Persist]
        public string AspectRatio { get; set; }

        //no persist so we don't muck the cache - this isn't presently used as 'series' don't have a single year
        // but we need it to be compatable with index creation
        public int? ProductionYear { get; set; }
    }
}
