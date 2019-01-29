﻿#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2015 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

namespace MediaPortal.Plugins.WifiRemote.Messages.MediaInfo
{
    /// <summary>
    /// MediaTypes for mapping 
    /// </summary>
    public enum MpExtendedMediaTypes
    {
        Movie = 0,
        MusicTrack = 1,
        Picture = 2,
        TVEpisode = 3,
        File = 4,
        TVShow = 5,
        TVSeason = 6,
        MusicAlbum = 7,
        MusicArtist = 8,
        Folder = 9,
        Drive = 10,
        Tv = 12,
        Recording = 13
    }
}