#region Copyright (C) 2007-2017 Team MediaPortal

/*
    Copyright (C) 2007-2017 Team MediaPortal
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

using System.Collections.Generic;
using MediaPortal.UiComponents.Media.General;
using MediaPortal.UiComponents.Media.Models.ScreenData;
using MediaPortal.UiComponents.Media.Models.Sorting;
using MediaPortal.Common.MediaManagement.MLQueries;
using MediaPortal.UiComponents.Media.Helpers;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;

namespace MediaPortal.UiComponents.Media.Models.NavigationModel
{
  internal class SeriesNavigationInitializer : BaseNavigationInitializer
  {
    internal static IEnumerable<string> RESTRICTED_MEDIA_CATEGORIES = new List<string> { Models.MediaNavigationMode.Series }; // "Series"

    protected SeriesFilterByNameScreenData _seriesScreen;

    public SeriesNavigationInitializer()
    {
      _mediaNavigationMode = Models.MediaNavigationMode.Series;
      _mediaNavigationRootState = Consts.WF_STATE_ID_SERIES_NAVIGATION_ROOT;
      _viewName = Consts.RES_SERIES_VIEW_NAME;
      _necessaryMias = Consts.NECESSARY_EPISODE_MIAS;
      _optionalMias = Consts.OPTIONAL_EPISODE_MIAS;
      _restrictedMediaCategories = RESTRICTED_MEDIA_CATEGORIES;
    }

    public override void InitMediaNavigation(out string mediaNavigationMode, out NavigationData navigationData)
    {
      base.InitMediaNavigation(out mediaNavigationMode, out navigationData);
      //Series filters modify the necessary/optional mia types of the current query view specification.
      //The series screen needs to return them back to the root episode mias so it needs to know what they are.
      //We need to set them after we have called InitMediaNavigation above as that call may modify the optional mia types.
      _seriesScreen.SetRootMiaTypes(navigationData.BaseViewSpecification.NecessaryMIATypeIds, navigationData.BaseViewSpecification.OptionalMIATypeIds);
    }

    protected override void Prepare()
    {
      base.Prepare();

      //Update filter by adding the user filter to the already loaded filters
      IFilter userFilter = CertificationHelper.GetUserCertificateFilter(_necessaryMias);
      if (userFilter != null)
      {
        _filter = BooleanCombinationFilter.CombineFilters(BooleanOperator.And, userFilter,
          BooleanCombinationFilter.CombineFilters(BooleanOperator.And, _filters));
      }
      else
      {
        _filter = BooleanCombinationFilter.CombineFilters(BooleanOperator.And, _filters);
      }

      userFilter = CertificationHelper.GetUserCertificateFilter(new[] { SeriesAspect.ASPECT_ID });
      //Set linked aspect filter
      if (!_linkedAspectFilters.ContainsKey(SeriesAspect.ASPECT_ID))
        _linkedAspectFilters.Add(SeriesAspect.ASPECT_ID, userFilter);
      else
        _linkedAspectFilters[SeriesAspect.ASPECT_ID] = userFilter;

      _defaultScreen = _seriesScreen = new SeriesFilterByNameScreenData();
      _availableScreens = new List<AbstractScreenData>
      {
        new SeriesShowItemsScreenData(_genericPlayableItemCreatorDelegate),
        // C# doesn't like it to have an assignment inside a collection initializer
        _defaultScreen,
        new SeriesFilterBySeasonScreenData(),
        new VideosFilterByLanguageScreenData(),
        new VideosFilterByPlayCountScreenData(),
        new SeriesFilterByGenreScreenData(),
        new SeriesFilterByCertificationScreenData(),
        new SeriesEpisodeFilterByActorScreenData(),
        new SeriesEpisodeFilterByCharacterScreenData(),
        new SeriesFilterByCompanyScreenData(),
        new SeriesFilterByTvNetworkScreenData(),
        new SeriesSimpleSearchScreenData(_genericPlayableItemCreatorDelegate),
      };
      _defaultSorting = new SeriesSortByEpisode();
      _availableSortings = new List<Sorting.Sorting>
      {
        _defaultSorting,
        new SeriesSortByDVDEpisode(),
        new VideoSortByFirstGenre(),
        new SeriesSortByCertification(),
        new SeriesSortByFirstActor(),
        new SeriesSortByFirstCharacter(),
        new VideoSortByFirstActor(),
        new VideoSortByFirstCharacter(),
        new VideoSortByFirstDirector(),
        new VideoSortByFirstWriter(),
        new SeriesSortByFirstTvNetwork(),
        new SeriesSortByFirstProductionStudio(),
        new SeriesSortBySeasonTitle(),
        new SeriesSortByEpisodeTitle(),
        new SortByTitle(),
        new SortBySortTitle(),
        new SortByName(),
        new SortByFirstAiredDate(),
        new SortByAddedDate(),
        new SortBySystem(),
      };
      _defaultGrouping = null;
      _availableGroupings = new List<Sorting.Sorting>
      {
        //_defaultGrouping,
        new SeriesSortByEpisode(),
        new SeriesSortByDVDEpisode(),
        new VideoSortByFirstGenre(),
        new SeriesSortByCertification(),
        new SeriesSortByFirstActor(),
        new SeriesSortByFirstCharacter(),
        new VideoSortByFirstActor(),
        new VideoSortByFirstCharacter(),
        new VideoSortByFirstDirector(),
        new VideoSortByFirstWriter(),
        new SeriesSortByFirstTvNetwork(),
        new SeriesSortByFirstProductionStudio(),
        new SeriesSortBySeasonTitle(),
        new SeriesSortByEpisodeTitle(),
        new SortByTitle(),
        new SortBySortTitle(),
        new SortByName(),
        new SortByFirstAiredDate(),
        new SortByAddedDate(),
        new SortBySystem(),
      };
    }
  }
}
