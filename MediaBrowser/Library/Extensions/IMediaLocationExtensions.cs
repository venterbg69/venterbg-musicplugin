﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Filesystem;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Library.Providers.TVDB;
using System.IO;

namespace MediaBrowser.Library.Extensions {
    static class IMediaLocationExtensions {

        public static bool IsHidden(this IMediaLocation location) {
            return (location.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
        } 

        public static bool IsIso(this IMediaLocation location) {
            return Helper.IsIso(location.Path);
        } 

        public static bool IsVideo(this IMediaLocation location) {
            return Helper.IsVideo(location.Path);
        }

        public static bool IsVob(this IMediaLocation location) {
            return Helper.IsVob(location.Path);
        }

        public static bool IsSeriesFolder(this IMediaLocation location) { 
            IFolderMediaLocation folder = location as IFolderMediaLocation;
            if (folder != null) {

                if (TVUtils.IsSeasonFolder(folder.Path))
                    return false;

                int i = 0;

                foreach (IMediaLocation child in folder.Children) {
                    if (child is IFolderMediaLocation &&
                        TVUtils.IsSeasonFolder(child.Path))
                        return true; // we have found at least one season folder
                    else
                        i++;
                    if (i >= 3)
                        return false; // a folder with more than 3 non-season folders in will not be counted as a series
                }

                foreach (IMediaLocation child in folder.Children) {
                    if (!(child is IFolderMediaLocation)  &&
                        child.IsVideo() &&
                        TVUtils.EpisodeNumberFromFile(child.Path, false) != null)
                        return true;
                }
            }
            return false;
        }

        public static MediaType GetVideoMediaType(this IMediaLocation location)
        {
            //figure out media type from file extension
            switch (location.Path.Substring(location.Path.Length - 4).ToLower())
            {
                case ".mkv":
                    return MediaType.Mkv;
                case ".avi":
                    return MediaType.Avi;
                case ".wmv":
                    return MediaType.Wmv;
                case ".mp4":
                case ".m4v":
                    return MediaType.Mp4;
                case ".dvr-ms":
                case ".wtv":
                    return MediaType.DVRMS;
                default:
                    return MediaType.Unknown;
            }
        }

        public static bool IsVodcast(this IMediaLocation location) {
            return location.Path.ToLower().EndsWith(".vodcast");
        }
    }
}
