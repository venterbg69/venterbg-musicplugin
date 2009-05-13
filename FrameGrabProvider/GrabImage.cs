﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MediaBrowser.Library.ImageManagement;

namespace FrameGrabProvider {
    class GrabImage : LibraryImage {

        protected override string LocalFilename {
            get {
                return System.IO.Path.Combine(cachePath, Id.ToString() + ".png");
            }
        }

        public override string GetLocalImagePath() {
            lock (Lock) {
                if (File.Exists(LocalFilename)) {
                    return LocalFilename;
                }

                // path without grab://
                string video = this.Path.Substring(7);

                Plugin.Logger.ReportInfo("Trying to extract thumbnail for " + video);

                if (ThumbCreator.CreateThumb(video, LocalFilename, 0.2)) {
                    return LocalFilename;
                } else {
                    Plugin.Logger.ReportWarning("Failed to grab thumbnail for " + video);
                    return null;
                }

            }

        }
    }
}
