﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Plugins.Configuration;
using MediaBrowser.Library.Plugins;

namespace FilmTrailerPlugin {
    public class PluginOptions : PluginConfigurationOptions {
        [Label("Menu Name:")]
        [Default("Trailers")]
        public string MenuName { get; set; }
    }
}