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

using System;
using System.Collections.Generic;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using System.Linq;
using MediaPortal.Common.General;
using MediaPortal.Common.MediaManagement.Helpers;
using MediaPortal.Extensions.OnlineLibraries.Matchers;
using MediaPortal.Extensions.OnlineLibraries;

namespace MediaPortal.Extensions.MetadataExtractors.SeriesMetadataExtractor
{
  class EpisodeSeriesRelationshipExtractor : IRelationshipRoleExtractor
  {
    private static readonly Guid[] ROLE_ASPECTS = { EpisodeAspect.ASPECT_ID };
    private static readonly Guid[] LINKED_ROLE_ASPECTS = { SeriesAspect.ASPECT_ID };
    private CheckedItemCache<EpisodeInfo> _checkCache = new CheckedItemCache<EpisodeInfo>(SeriesMetadataExtractor.MINIMUM_HOUR_AGE_BEFORE_UPDATE);
    private CheckedItemCache<SeriesInfo> _seriesCache = new CheckedItemCache<SeriesInfo>(SeriesMetadataExtractor.MINIMUM_HOUR_AGE_BEFORE_UPDATE);

    public Guid Role
    {
      get { return EpisodeAspect.ROLE_EPISODE; }
    }

    public Guid[] RoleAspects
    {
      get { return ROLE_ASPECTS; }
    }

    public Guid LinkedRole
    {
      get { return SeriesAspect.ROLE_SERIES; }
    }

    public Guid[] LinkedRoleAspects
    {
      get { return LINKED_ROLE_ASPECTS; }
    }

    public bool TryExtractRelationships(IDictionary<Guid, IList<MediaItemAspect>> aspects, out ICollection<IDictionary<Guid, IList<MediaItemAspect>>> extractedLinkedAspects, bool forceQuickMode)
    {
      extractedLinkedAspects = null;

      EpisodeInfo episodeInfo = new EpisodeInfo();
      if (!episodeInfo.FromMetadata(aspects))
        return false;

      if (_checkCache.IsItemChecked(episodeInfo))
        return false;

      SeriesInfo seriesInfo;
      if (!_seriesCache.TryGetCheckedItem(episodeInfo.CloneBasicInstance<SeriesInfo>(), out seriesInfo))
      {
        seriesInfo = episodeInfo.CloneBasicInstance<SeriesInfo>();
        OnlineMatcherService.UpdateSeries(seriesInfo, false, forceQuickMode);
        _seriesCache.TryAddCheckedItem(seriesInfo);
      }

      extractedLinkedAspects = new List<IDictionary<Guid, IList<MediaItemAspect>>>();
      IDictionary<Guid, IList<MediaItemAspect>> seriesAspects = new Dictionary<Guid, IList<MediaItemAspect>>();
      seriesInfo.SetMetadata(seriesAspects);

      if (aspects.ContainsKey(EpisodeAspect.ASPECT_ID))
      {
        bool episodeVirtual = true;
        if (MediaItemAspect.TryGetAttribute(aspects, MediaAspect.ATTR_ISVIRTUAL, false, out episodeVirtual))
        {
          MediaItemAspect.SetAttribute(seriesAspects, MediaAspect.ATTR_ISVIRTUAL, episodeVirtual);
        }
      }

      if (!seriesAspects.ContainsKey(ExternalIdentifierAspect.ASPECT_ID))
        return false;

      extractedLinkedAspects.Add(seriesAspects);
      return true;
    }

    public bool TryMatch(IDictionary<Guid, IList<MediaItemAspect>> extractedAspects, IDictionary<Guid, IList<MediaItemAspect>> existingAspects)
    {
      return existingAspects.ContainsKey(SeriesAspect.ASPECT_ID);
    }

    public bool TryGetRelationshipIndex(IDictionary<Guid, IList<MediaItemAspect>> aspects, IDictionary<Guid, IList<MediaItemAspect>> linkedAspects, out int index)
    {
      index = -1;

      SingleMediaItemAspect aspect;
      if (!MediaItemAspect.TryGetAspect(aspects, EpisodeAspect.Metadata, out aspect))
        return false;

      IEnumerable<object> indexes = aspect.GetCollectionAttribute<object>(EpisodeAspect.ATTR_EPISODE);
      if (indexes == null)
        return false;

      IList<object> episodeNums = indexes.ToList();
      if (episodeNums.Count == 0)
        return false;

      int episode = Int32.Parse(episodeNums.First().ToString());
      int season = aspect.GetAttributeValue<int>(EpisodeAspect.ATTR_SEASON);

      index = season * 100 + episode;
      return true;
    }

    internal static ILogger Logger
    {
      get { return ServiceRegistration.Get<ILogger>(); }
    }
  }
}