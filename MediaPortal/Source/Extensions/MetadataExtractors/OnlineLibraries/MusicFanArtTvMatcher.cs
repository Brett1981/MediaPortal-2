#region Copyright (C) 2007-2015 Team MediaPortal

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

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using MediaPortal.Common;
using MediaPortal.Common.Localization;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement.Helpers;
using MediaPortal.Common.PathManager;
using MediaPortal.Extensions.OnlineLibraries.Matches;
using MediaPortal.Extensions.OnlineLibraries.FanArtTV;
using MediaPortal.Extensions.OnlineLibraries.Libraries.FanArtTVV3.Data;
using System.Collections.Generic;
using System.Linq;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;

namespace MediaPortal.Extensions.OnlineLibraries
{
  /// <summary>
  /// <see cref="MusicFanArtTvMatcher"/> is used to download music images from FanArt.tv.
  /// </summary>
  public class MusicFanArtTvMatcher : BaseMatcher<TrackMatch, string>
  {
    #region Static instance

    public static MusicFanArtTvMatcher Instance
    {
      get { return ServiceRegistration.Get<MusicFanArtTvMatcher>(); }
    }

    #endregion

    #region Constants

    public static string CACHE_PATH = ServiceRegistration.Get<IPathManager>().GetPath(@"<DATA>\FanArtTV\");
    protected static string _matchesSettingsFile = Path.Combine(CACHE_PATH, "TrackMatches.xml");
    protected static TimeSpan MAX_MEMCACHE_DURATION = TimeSpan.FromHours(12);

    protected override string MatchesSettingsFile
    {
      get { return _matchesSettingsFile; }
    }

    #endregion

    #region Fields

    protected DateTime _memoryCacheInvalidated = DateTime.MinValue;
    protected ConcurrentDictionary<string, TrackInfo> _memoryCache = new ConcurrentDictionary<string, TrackInfo>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Contains the initialized FanArtTvWrapper.
    /// </summary>
    private FanArtTVWrapper _fanArt;

    #endregion

    /// <summary>
    /// Tries to lookup the music from FanArt.tv and downloads images.
    /// </summary>
    /// <param name="trackInfo">Track to check</param>
    /// <returns><c>true</c> if successful</returns>
    public bool FindAndUpdateTrack(TrackInfo trackInfo)
    {
      try
      {
        // Try online lookup
        if (!Init())
          return false;

        if (!string.IsNullOrEmpty(trackInfo.MusicBrainzId))
        {
          TrackInfo oldTrackInfo;
          CheckCacheAndRefresh();
          if (_memoryCache.TryGetValue(trackInfo.MusicBrainzId, out oldTrackInfo))
          {
            //Already downloaded
            return true;
          }

          FanArtAlbumDetails thumbs;
          if (trackInfo.Thumbnail == null && !string.IsNullOrEmpty(trackInfo.AlbumMusicBrainzGroupId) && _fanArt.GetAlbumFanArt(trackInfo.AlbumMusicBrainzGroupId, out thumbs))
          {
            if (thumbs.Albums.ContainsKey(trackInfo.AlbumMusicBrainzGroupId))
            {
              // Get Thumbnail
              FanArtThumb thumb = thumbs.Albums[trackInfo.AlbumMusicBrainzGroupId].AlbumCovers.OrderByDescending(b => b.Likes).First();
              string category = "Covers";
              if (_fanArt.DownloadFanArt(trackInfo.AlbumMusicBrainzGroupId, thumb, category))
              {
                trackInfo.Thumbnail = _fanArt.GetFanArt(trackInfo.AlbumMusicBrainzGroupId, thumb, category);
                return true;
              }
            }
          }

          if (_memoryCache.TryAdd(trackInfo.MusicBrainzId, trackInfo))
          {
            ScheduleDownload(trackInfo.MusicBrainzId);
            return true;
          }
        }
        return false;
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Debug("MusicFanArtTvMatcher: Exception while processing track {0}", ex, trackInfo.ToString());
        return false;
      }
    }

    public bool UpdateAlbum(AlbumInfo albumInfo)
    {
      try
      {
        FanArtAlbumDetails thumbs;
        if (albumInfo.Thumbnail != null)
          return false;

        if (albumInfo.Thumbnail == null && !string.IsNullOrEmpty(albumInfo.MusicBrainzGroupId) && _fanArt.GetAlbumFanArt(albumInfo.MusicBrainzGroupId, out thumbs))
        {
          if (thumbs.Albums.ContainsKey(albumInfo.MusicBrainzGroupId))
          {
            // Get Thumbnail
            FanArtThumb thumb = thumbs.Albums[albumInfo.MusicBrainzGroupId].AlbumCovers.OrderByDescending(b => b.Likes).First();
            string category = "Covers";
            if (_fanArt.DownloadFanArt(albumInfo.MusicBrainzGroupId, thumb, category))
            {
              albumInfo.Thumbnail = _fanArt.GetFanArt(albumInfo.MusicBrainzGroupId, thumb, category);
              return true;
            }
          }
        }

        return false;
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Debug("MusicFanArtTvMatcher: Exception while processing album {0}", ex, albumInfo.ToString());
        return false;
      }
    }

    public bool UpdateAlbumPersons(AlbumInfo albumInfo, string occupation)
    {
      return UpdatePersons(albumInfo.Artists, occupation);
    }

    public bool UpdateTrackPersons(TrackInfo trackInfo, string occupation)
    {
      return UpdatePersons(trackInfo.Artists, occupation);
    }

    private bool UpdatePersons(List<PersonInfo> persons, string occupation)
    {
      try
      {
        if (occupation != PersonAspect.OCCUPATION_ARTIST)
          return false;

        // Try online lookup
        if (!Init())
          return false;

        FanArtArtistThumbs artistThumbs;
        foreach (PersonInfo person in persons)
        {
          if (person.Thumbnail == null && !string.IsNullOrEmpty(person.MusicBrainzId) &&
            _fanArt.GetArtistFanArt(person.MusicBrainzId, out artistThumbs))
          {
            // Get Thumbnail
            FanArtThumb thumb = artistThumbs.ArtistThumbnails.OrderByDescending(b => b.Likes).First();
            string category = "Thumbnails";
            if (_fanArt.DownloadFanArt(person.MusicBrainzId, thumb, category))
            {
              person.Thumbnail = _fanArt.GetFanArt(person.MusicBrainzId, thumb, category);
            }
          }
        }
        return true;
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Debug("MusicFanArtTvMatcher: Exception while processing persons", ex);
        return false;
      }
    }

    public bool UpdateAlbumCompanies(AlbumInfo albumInfo, string type)
    {
      try
      {
        if (type != CompanyAspect.COMPANY_MUSIC_LABEL)
          return false;

        // Try online lookup
        if (!Init())
          return false;

        FanArtLabelThumbs labelThumbs;
        foreach (CompanyInfo company in albumInfo.MusicLabels)
        {
          if (company.Thumbnail == null && !string.IsNullOrEmpty(company.MusicBrainzId) &&
            _fanArt.GetLabelFanArt(company.MusicBrainzId, out labelThumbs))
          {
            // Get Thumbnail
            FanArtLabelThumb thumb = labelThumbs.LabelLogos.OrderByDescending(b => b.Likes).First();
            string category = "Logos";
            if (_fanArt.DownloadFanArt(company.MusicBrainzId, thumb, category))
            {
              company.Thumbnail = _fanArt.GetFanArt(company.MusicBrainzId, thumb, category);
            }
          }
        }
        return true;
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Debug("MusicFanArtTvMatcher: Exception while processing companies", ex);
        return false;
      }
    }

    /// <summary>
    /// Check if the memory cache should be cleared and starts an online update of (file-) cached series information.
    /// </summary>
    private void CheckCacheAndRefresh()
    {
      if (DateTime.Now - _memoryCacheInvalidated <= MAX_MEMCACHE_DURATION)
        return;
      _memoryCache.Clear();
      _memoryCacheInvalidated = DateTime.Now;

      // TODO: when updating track information is implemented, start here a job to do it
    }

    public override bool Init()
    {
      if (!base.Init())
        return false;

      if (_fanArt != null)
        return true;

      try
      {
        _fanArt = new FanArtTVWrapper();
        bool res = _fanArt.Init(CACHE_PATH);
        // Try to lookup online content in the configured language
        CultureInfo currentCulture = ServiceRegistration.Get<ILocalization>().CurrentCulture;
        _fanArt.SetPreferredLanguage(currentCulture.TwoLetterISOLanguageName);
        return res;
      }
      catch (Exception)
      {
        return false;
      }
    }

    protected override void DownloadFanArt(string mbId)
    {
      try
      {
        if (string.IsNullOrEmpty(mbId))
          return;

        ServiceRegistration.Get<ILogger>().Debug("MusicFanArtTvMatcher Download: Started for ID {0}", mbId);

        TrackInfo trackInfo;
        if (!_memoryCache.TryGetValue(mbId, out trackInfo))
          return;

        if (!Init())
          return;

        FanArtAlbumDetails thumbs;
        if (!_fanArt.GetAlbumFanArt(trackInfo.AlbumMusicBrainzGroupId, out thumbs))
          return;

        if (thumbs.Albums.ContainsKey(trackInfo.AlbumMusicBrainzGroupId) == true)
        {
          // Save Album Covers and CD Art
          ServiceRegistration.Get<ILogger>().Debug("MusicFanArtTvMatcher Download: Begin saving album banners for ID {0}", mbId);
          SaveBanners(trackInfo.AlbumMusicBrainzGroupId, thumbs.Albums[trackInfo.AlbumMusicBrainzGroupId].AlbumCovers.OrderByDescending(b => b.Likes).ToList(), "Covers");
          SaveBanners(trackInfo.AlbumMusicBrainzGroupId, thumbs.Albums[trackInfo.AlbumMusicBrainzGroupId].CDArts.OrderByDescending(b => b.Likes).ToList(), "CDArt");
        }

        ServiceRegistration.Get<ILogger>().Debug("MusicFanArtTvMatcher Download: Begin saving artist banners for ID {0}", mbId);
        FanArtArtistThumbs artistThumbs;
        foreach (PersonInfo person in trackInfo.Artists.Where(p => !string.IsNullOrEmpty(p.MusicBrainzId)))
        {
          if (_fanArt.GetArtistFanArt(person.MusicBrainzId, out artistThumbs))
          {
            SaveBanners(person.MusicBrainzId, artistThumbs.ArtistBanners.OrderByDescending(b => b.Likes).ToList(), "Banners");
            SaveBanners(person.MusicBrainzId, artistThumbs.ArtistFanart.OrderByDescending(b => b.Likes).ToList(), "Backdrops");
            SaveBanners(person.MusicBrainzId, artistThumbs.HDArtistLogos.OrderByDescending(b => b.Likes).ToList(), "Logos");
            SaveBanners(person.MusicBrainzId, artistThumbs.ArtistThumbnails.OrderByDescending(b => b.Likes).ToList(), "Thumbnails");
          }
        }
        foreach (PersonInfo person in trackInfo.AlbumArtists.Where(p => !string.IsNullOrEmpty(p.MusicBrainzId)))
        {
          if (_fanArt.GetArtistFanArt(person.MusicBrainzId, out artistThumbs))
          {
            SaveBanners(person.MusicBrainzId, artistThumbs.ArtistBanners.OrderByDescending(b => b.Likes).ToList(), "Banners");
            SaveBanners(person.MusicBrainzId, artistThumbs.ArtistFanart.OrderByDescending(b => b.Likes).ToList(), "Backdrops");
            SaveBanners(person.MusicBrainzId, artistThumbs.HDArtistLogos.OrderByDescending(b => b.Likes).ToList(), "Logos");
            SaveBanners(person.MusicBrainzId, artistThumbs.ArtistThumbnails.OrderByDescending(b => b.Likes).ToList(), "Thumbnails");
          }
        }

        ServiceRegistration.Get<ILogger>().Debug("MusicFanArtTvMatcher Download: Begin saving label banners for ID {0}", mbId);
        FanArtLabelThumbs labelThumbs;
        foreach (CompanyInfo company in trackInfo.MusicLabels.Where(l => !string.IsNullOrEmpty(l.MusicBrainzId)))
        {
          if (_fanArt.GetLabelFanArt(company.MusicBrainzId, out labelThumbs))
          {
            SaveBanners(company.MusicBrainzId, labelThumbs.LabelLogos.OrderByDescending(b => b.Likes).ToList(), "Logos");
          }
        }

        ServiceRegistration.Get<ILogger>().Debug("MusicFanArtTvMatcher Download: Finished ID {0}", mbId);

        TrackMatch onlineMatch = new TrackMatch
        {
          ItemName = mbId,
          Id = mbId
        };

        // Save cache
        _storage.TryAddMatch(onlineMatch);

        // Remember we are finished
        FinishDownloadFanArt(mbId);
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Debug("MusicFanArtTvMatcher: Exception downloading FanArt for ID {0}", ex, mbId);
      }
    }

    private int SaveBanners(string id, IEnumerable<FanArtThumb> banners, string category)
    {
      if (banners == null)
        return 0;

      int idx = 0;
      foreach (FanArtThumb banner in banners)
      {
        if (idx >= MAX_FANART_IMAGES)
          break;
        if (_fanArt.DownloadFanArt(id, banner, category))
          idx++;
      }
      ServiceRegistration.Get<ILogger>().Debug("MusicFanArtTvMatcher Download: Saved {0} {1}", idx, category);
      return idx;
    }
  }
}