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
  public class MusicFanArtTvMatcher : MusicMatcher<FanArtMovieThumb, string>
  {
    #region Static instance

    public static MusicFanArtTvMatcher Instance
    {
      get { return ServiceRegistration.Get<MusicFanArtTvMatcher>(); }
    }

    #endregion

    #region Constants

    public static string CACHE_PATH = ServiceRegistration.Get<IPathManager>().GetPath(@"<DATA>\FanArtTV\");
    protected static TimeSpan MAX_MEMCACHE_DURATION = TimeSpan.FromHours(12);

    #endregion

    #region Init

    public MusicFanArtTvMatcher() : 
      base(CACHE_PATH, MAX_MEMCACHE_DURATION)
    {
    }

    public override bool InitWrapper()
    {
      try
      {
        FanArtTVWrapper wrapper = new FanArtTVWrapper();
        // Try to lookup online content in the configured language
        CultureInfo currentCulture = ServiceRegistration.Get<ILocalization>().CurrentCulture;
        wrapper.SetPreferredLanguage(currentCulture.TwoLetterISOLanguageName);
        if (wrapper.Init(CACHE_PATH))
        {
          _wrapper = wrapper;
          return true;
        }
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("MusicFanArtTvMatcher: Error initializing wrapper", ex);
      }
      return false;
    }

    #endregion

    #region Translators

    protected override bool GetTrackAlbumId(AlbumInfo album, out string id)
    {
      id = null;
      if (!string.IsNullOrEmpty(album.MusicBrainzGroupId))
        id = album.MusicBrainzGroupId;
      return id != null;
    }

    protected override bool GetTrackId(TrackInfo track, out string id)
    {
      id = null;
      if (!string.IsNullOrEmpty(track.MusicBrainzId))
        id = track.MusicBrainzId;
      return id != null;
    }

    protected override bool SetTrackId(TrackInfo track, string id)
    {
      if (!string.IsNullOrEmpty(id))
      {
        track.MusicBrainzId = id;
        return true;
      }
      return false;
    }

    protected override bool GetCompanyId(CompanyInfo company, out string id)
    {
      id = null;
      if (!string.IsNullOrEmpty(company.MusicBrainzId))
        id = company.MusicBrainzId;
      return id != null;
    }

    protected override bool GetPersonId(PersonInfo person, out string id)
    {
      id = null;
      if (!string.IsNullOrEmpty(person.MusicBrainzId))
        id = person.MusicBrainzId;
      return id != null;
    }

    #endregion

    #region FanArt

    protected override bool VerifyFanArtImage(FanArtMovieThumb image)
    {
      if (image.Language == null || image.Language == _wrapper.PreferredLanguage)
        return true;
      return false;
    }

    #endregion
  }
}