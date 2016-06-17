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
using System.Collections.Generic;
using System.Linq;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Utilities;
using MediaPortal.Common.MediaManagement.Helpers;
using System.Reflection;

namespace MediaPortal.Extensions.OnlineLibraries
{
  public abstract class ApiWrapper<TImg, TLang>
  {
    private TLang _preferredLanguage;
    private TLang _defaultLanguage;
    private string _cachePath;

    public const int MAX_LEVENSHTEIN_DIST = 4;

    private enum AudioValueToCheck
    {
      ArtistLax,
      AlbumLax,
      Year,
      TrackNum,
      ArtistStrict,
      AlbumStrict,
      Compilation,
      Discs,
      Language,
    }

    /// <summary>
    /// Sets the preferred language.
    /// </summary>
    /// <param name="lang">Language used by API</param>
    public void SetPreferredLanguage(TLang lang)
    {
      _preferredLanguage = lang;
    }

    /// <summary>
    /// Returns the language that matches the value set by <see cref="SetPreferredLanguage"/> or the default language.
    /// </summary>
    public TLang PreferredLanguage
    {
      get { return _preferredLanguage == null ? _defaultLanguage : _preferredLanguage; }
    }

    /// <summary>
    /// Sets the default language to use when no matches are found.
    /// </summary>
    /// <param name="lang">Language used by API</param>
    public void SetDefaultLanguage(TLang lang)
    {
      _defaultLanguage = lang;
    }

    /// <summary>
    /// Returns the language that matches the value set by <see cref="SetDefaultLanguage"/>.
    /// </summary>
    public TLang DefaultLanguage
    {
      get { return _defaultLanguage; }
    }

    /// <summary>
    /// Sets path to use for caching downloads.
    /// </summary>
    /// <param name="path">The path to use for caching downloads</param>
    public void SetCachePath(string path)
    {
      _cachePath = path;
    }

    /// <summary>
    /// The path to use for caching downloads as set by <see cref="SetCachePath"/>.
    /// </summary>
    public string CachePath
    {
      get { return _cachePath; }
    }

    #region Movies

    /// <summary>
    /// Search for Movie.
    /// </summary>
    /// <param name="movieSearch">Movie search parameters</param>
    /// <param name="language">Language, if <c>null</c> it takes the <see cref="PreferredLanguage"/></param>
    /// <param name="movies">Returns the list of matches.</param>
    /// <returns><c>true</c> if at least one Movie was found.</returns>
    public virtual bool SearchMovie(MovieInfo movieSearch, TLang language, out List<MovieInfo> movies)
    {
      movies = null;
      return false;
    }

    /// <summary>
    /// Search for unique matches of Movie. This method tries to find the best matching Movie in following order:
    /// - Exact match using PreferredLanguage
    /// - Exact match using DefaultLanguage
    /// - If movies name contains " - ", it splits on this and tries to runs again using the first part (combined titles)
    /// </summary>
    /// <param name="movieSearch">Movie search parameters</param>
    /// <param name="language">Language, if <c>null</c> it takes the <see cref="PreferredLanguage"/></param>
    /// <param name="movieOnline">Returns movie information with only the Ids of the matched movie</param>
    /// <returns><c>true</c> if at exactly one Movie was found.</returns>
    public bool SearchMovieUniqueAndUpdate(MovieInfo movieSearch, TLang language)
    {
      List<MovieInfo> movies;
      language = language != null ? language : PreferredLanguage;

      if (!SearchMovie(movieSearch, language, out movies))
        return false;
      if (TestMovieMatch(movieSearch, ref movies))
      {
        movieSearch.CopyIdsFrom(movies[0]);
        return true;
      }

      if (movies.Count == 0 && !language.Equals(_defaultLanguage))
      {
        if (!SearchMovie(movieSearch, _defaultLanguage, out movies))
          return false;

        // If also no match in default language is found, we will look for combined movies names:
        // i.e. "Sanctuary - Wächter der Kreaturen" is not found, but "Sanctuary" is.
        if (!TestMovieMatch(movieSearch, ref movies) && movieSearch.MovieName.Text.Contains("-"))
        {
          LanguageText originalName = movieSearch.MovieName;
          string namePart = movieSearch.MovieName.Text.Split(new [] { '-' })[0].Trim();
          movieSearch.MovieName = new LanguageText(namePart);
          if(SearchMovieUniqueAndUpdate(movieSearch, language))
            return true;
          movieSearch.MovieName = originalName;
          return false;
        }
        if (movies.Count == 1)
        {
          movieSearch.CopyIdsFrom(movies[0]);
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Tests for movie matches. 
    /// </summary>
    /// <param name="movieSearch">Movie search parameters</param>
    /// <param name="movies">Potential online matches. The collection will be modified inside this method.</param>
    /// <returns><c>true</c> if unique match</returns>
    protected virtual bool TestMovieMatch(MovieInfo movieSearch, ref List<MovieInfo> movies)
    {
      // Exact match in preferred language
      ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Test Match for \"{0}\"", movieSearch);

      if (movies.Count == 1)
      {
        if (GetLevenshteinDistance(movies[0], movieSearch) <= MAX_LEVENSHTEIN_DIST)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", movieSearch);
          return true;
        }
        // No valid match, clear list to allow further detection ways
        movies.Clear();
        return false;
      }

      // Multiple matches
      if (movies.Count > 1)
      {
        ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches for \"{0}\" ({1}). Try to find exact name match.", movieSearch, movies.Count);
        var exactMatches = movies.FindAll(s => s.MovieName.Text == movieSearch.MovieName.Text || s.OriginalName == movieSearch.MovieName.Text || GetLevenshteinDistance(s, movieSearch) == 0);
        if (exactMatches.Count == 1)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", movieSearch);
          movies = exactMatches;
          return true;
        }

        if (exactMatches.Count > 1)
        {
          // Try to match the year, if available
          if (movieSearch.ReleaseDate.HasValue)
          {
            var yearFiltered = exactMatches.FindAll(s => s.ReleaseDate.HasValue && s.ReleaseDate.Value.Year == movieSearch.ReleaseDate.Value.Year);
            if (yearFiltered.Count == 1)
            {
              ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", movieSearch);
              movies = yearFiltered;
              return true;
            }
          }
        }

        movies = movies.Where(s => GetLevenshteinDistance(s, movieSearch) <= MAX_LEVENSHTEIN_DIST).ToList();

        if (movies.Count > 1)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches for exact name \"{0}\" ({1}). Try to find match for preferred language {2}.", movieSearch, movies.Count, PreferredLanguage);
          movies = movies.FindAll(s => s.Languages.Contains(PreferredLanguage.ToString()) || s.Languages.Count == 0);
        }

        if (movies.Count > 1)
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches found for \"{0}\" (count: {1})", movieSearch, movies.Count);

        return movies.Count == 1;
      }
      return false;
    }

    public virtual bool UpdateFromOnlineMovie(MovieInfo movie, TLang language, bool cacheOnly)
    {
      return false;
    }

    public virtual bool UpdateFromOnlineMovieCollection(MovieCollectionInfo collection, TLang language, bool cacheOnly)
    {
      return false;
    }

    #endregion

    #region Series

    /// <summary>
    /// Search for Series.
    /// </summary>
    /// <param name="episodeSearch">Episode search parameters.</param>
    /// <param name="series">Returns the list of matches.</param>
    /// <returns><c>true</c> if at least one Episode was found.</returns>
    public virtual bool SearchSeriesEpisode(EpisodeInfo episodeSearch, TLang language, out List<EpisodeInfo> episodes)
    {
      episodes = null;
      return false;
    }

    /// <summary>
    /// Search for unique matches of Series names. This method tries to find the best matching Series in following order:
    /// - Exact match using PreferredLanguage
    /// - Exact match using DefaultLanguage
    /// - If series name contains " - ", it splits on this and tries to runs again using the first part (combined titles)
    /// </summary>
    /// <param name="episodeSearch">Episode search parameters.</param>
    /// <param name="series">Returns the list of matches.</param>
    /// <returns><c>true</c> if only one Episode was found.</returns>
    public bool SearchSeriesEpisodeUniqueAndUpdate(EpisodeInfo episodeSearch, TLang language)
    {
      List<EpisodeInfo> episodes;
      language = language != null ? language : PreferredLanguage;

      if (!SearchSeriesEpisode(episodeSearch, language, out episodes))
        return false;
      if (TestSeriesEpisodeMatch(episodeSearch, ref episodes))
      {
        episodeSearch.CopyIdsFrom(episodes[0]);
        return true;
      }

      if (episodes.Count == 0 && !language.Equals(_defaultLanguage))
      {
        if (!SearchSeriesEpisode(episodeSearch, _defaultLanguage, out episodes))
          return false;

        // If also no match in default language is found, we will look for combined movies names:
        // i.e. "Sanctuary - Wächter der Kreaturen" is not found, but "Sanctuary" is.
        if (!TestSeriesEpisodeMatch(episodeSearch, ref episodes) && episodeSearch.SeriesName.Text.Contains("-"))
        {
          LanguageText originalName = episodeSearch.SeriesName;
          string namePart = episodeSearch.SeriesName.Text.Split(new[] { '-' })[0].Trim();
          episodeSearch.SeriesName = new LanguageText(namePart);
          if (SearchSeriesEpisodeUniqueAndUpdate(episodeSearch, language))
            return true;
          episodeSearch.SeriesName = originalName;
          return false;
        }
        if (episodes.Count == 1)
        {
          episodeSearch.CopyIdsFrom(episodes[0]);
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Tests for episode matches. 
    /// </summary>
    /// <param name="seriesSearch">Series search parameters.</param>
    /// <param name="episodes">Potential online matches. The collection will be modified inside this method.</param>
    /// <returns><c>true</c> if unique match</returns>
    protected virtual bool TestSeriesEpisodeMatch(EpisodeInfo episodeSearch, ref List<EpisodeInfo> episodes)
    {
      // Exact match in preferred language
      ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Test Match for \"{0}\"", episodeSearch);

      if (episodes.Count == 1)
      {
        if (GetLevenshteinDistance(episodes[0], episodeSearch) <= MAX_LEVENSHTEIN_DIST)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", episodeSearch);
          return true;
        }
        // No valid match, clear list to allow further detection ways
        episodes.Clear();
        return false;
      }

      if (episodeSearch.EpisodeNumbers.Count > 0)
      {
        var episodeFiltered = episodes.FindAll(e => episodeSearch.EpisodeNumbers.All(i => e.EpisodeNumbers.Contains(i)));
        if (episodeFiltered.Count == 1)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", episodeSearch);
          episodes = episodeFiltered;
          return true;
        }
      }

      // Multiple matches
      if (episodes.Count > 1)
      {
        ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches for \"{0}\" ({1}). Try to find exact name match.", episodeSearch, episodes.Count);
        var exactMatches = episodes.FindAll(e => e.EpisodeName.Text == episodeSearch.EpisodeName.Text || GetLevenshteinDistance(e, episodeSearch) == 0);
        if (exactMatches.Count == 1)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", episodeSearch);
          episodes = exactMatches;
          return true;
        }

        episodes = episodes.Where(e => GetLevenshteinDistance(e, episodeSearch) <= MAX_LEVENSHTEIN_DIST).ToList();
        if (episodes.Count > 1)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches for exact name \"{0}\" ({1}). Try to find match for preferred language {2}.", episodeSearch, episodes.Count, PreferredLanguage);
          episodes = episodes.FindAll(s => s.Languages.Contains(PreferredLanguage.ToString()) || s.Languages.Count == 0);
        }

        if (episodes.Count > 1)
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches found for \"{0}\" (count: {1})", episodeSearch, episodes.Count);
      }
      return episodes.Count == 1;
    }

    /// <summary>
    /// Search for Series.
    /// </summary>
    /// <param name="seriesSearch">Series search parameters.</param>
    /// <param name="series">Returns the list of matches.</param>
    /// <returns><c>true</c> if at least one Series was found.</returns>
    public virtual bool SearchSeries(SeriesInfo seriesSearch, TLang language, out List<SeriesInfo> series)
    {
      series = null;
      return false;
    }

    /// <summary>
    /// Search for unique matches of Series names. This method tries to find the best matching Series in following order:
    /// - Exact match using PreferredLanguage
    /// - Exact match using DefaultLanguage
    /// - If series name contains " - ", it splits on this and tries to runs again using the first part (combined titles)
    /// </summary>
    /// <param name="seriesSearch">Series search parameters.</param>
    /// <param name="series">Returns the list of matches.</param>
    /// <returns><c>true</c> if only one Series was found.</returns>
    public bool SearchSeriesUniqueAndUpdate(SeriesInfo seriesSearch, TLang language)
    {
      List<SeriesInfo> series;
      language = language != null ? language : PreferredLanguage;

      if (!SearchSeries(seriesSearch, language, out series))
        return false;
      if (TestSeriesMatch(seriesSearch, ref series))
      {
        seriesSearch.CopyIdsFrom(series[0]);
        return true;
      }

      if (series.Count == 0 && !language.Equals(_defaultLanguage))
      {
        if (!SearchSeries(seriesSearch, _defaultLanguage, out series))
          return false;

        // If also no match in default language is found, we will look for combined movies names:
        // i.e. "Sanctuary - Wächter der Kreaturen" is not found, but "Sanctuary" is.
        if (!TestSeriesMatch(seriesSearch, ref series) && seriesSearch.SeriesName.Text.Contains("-"))
        {
          LanguageText originalName = seriesSearch.SeriesName;
          string namePart = seriesSearch.SeriesName.Text.Split(new[] { '-' })[0].Trim();
          seriesSearch.SeriesName = new LanguageText(namePart);
          if (SearchSeriesUniqueAndUpdate(seriesSearch, language))
            return true;
          seriesSearch.SeriesName = originalName;
          return false;
        }
        if (series.Count == 1)
        {
          seriesSearch.CopyIdsFrom(series[0]);
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Tests for series matches. 
    /// </summary>
    /// <param name="seriesSearch">Series search parameters.</param>
    /// <param name="series">Potential online matches. The collection will be modified inside this method.</param>
    /// <returns><c>true</c> if unique match</returns>
    protected virtual bool TestSeriesMatch(SeriesInfo seriesSearch, ref List<SeriesInfo> series)
    {
      // Exact match in preferred language
      ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Test Match for \"{0}\"", seriesSearch);

      if (series.Count == 1)
      {
        if (GetLevenshteinDistance(series[0], seriesSearch) <= MAX_LEVENSHTEIN_DIST)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", seriesSearch);
          return true;
        }
        // No valid match, clear list to allow further detection ways
        series.Clear();
        return false;
      }

      // Multiple matches
      if (series.Count > 1)
      {
        ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches for \"{0}\" ({1}). Try to find exact name match.", seriesSearch, series.Count);
        var exactMatches = series.FindAll(s => s.SeriesName.Text == seriesSearch.SeriesName.Text || s.OriginalName == seriesSearch.OriginalName || GetLevenshteinDistance(s, seriesSearch) == 0);
        if (exactMatches.Count == 1)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", seriesSearch);
          series = exactMatches;
          return true;
        }

        if (exactMatches.Count > 1)
        {
          // Try to match the year, if available
          if (seriesSearch.FirstAired.HasValue)
          {
            var yearFiltered = exactMatches.FindAll(s => s.FirstAired.HasValue && s.FirstAired.Value.Year == seriesSearch.FirstAired.Value.Year);
            if (yearFiltered.Count == 1)
            {
              ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", seriesSearch);
              series = yearFiltered;
              return true;
            }
          }
        }

        series = series.Where(s => GetLevenshteinDistance(s, seriesSearch) <= MAX_LEVENSHTEIN_DIST).ToList();

        if (series.Count > 1)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches for exact name \"{0}\" ({1}). Try to find match for preferred language {2}.", seriesSearch, series.Count, PreferredLanguage);
          series = series.FindAll(s => s.Languages.Contains(PreferredLanguage.ToString()) || s.Languages.Count == 0);
        }

        if (series.Count > 1)
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches found for \"{0}\" (count: {1})", seriesSearch, series.Count);

      }
      return series.Count == 1;
    }

    public virtual bool UpdateFromOnlineSeries(SeriesInfo series, TLang language, bool cacheOnly)
    {
      return false;
    }

    public virtual bool UpdateFromOnlineSeriesSeason(SeasonInfo season, TLang language, bool cacheOnly)
    {
      return false;
    }

    public virtual bool UpdateFromOnlineSeriesEpisode(EpisodeInfo episode, TLang language, bool cacheOnly)
    {
      return false;
    }

    protected virtual void SetMultiEpisodeDetails(EpisodeInfo episodeInfo, List<EpisodeInfo> episodeMatches)
    {
      episodeInfo.ImdbId = episodeMatches.First().ImdbId;
      episodeInfo.TvdbId = episodeMatches.First().TvdbId;
      episodeInfo.MovieDbId = episodeMatches.First().MovieDbId;
      episodeInfo.TvMazeId = episodeMatches.First().TvMazeId;
      episodeInfo.TvRageId = episodeMatches.First().TvRageId;

      episodeInfo.SeriesImdbId = episodeMatches.First().SeriesImdbId;
      episodeInfo.SeriesMovieDbId = episodeMatches.First().SeriesMovieDbId;
      episodeInfo.SeriesTvdbId = episodeMatches.First().SeriesTvdbId;
      episodeInfo.SeriesTvRageId = episodeMatches.First().SeriesTvRageId;
      episodeInfo.SeriesTvMazeId = episodeMatches.First().SeriesTvMazeId;
      episodeInfo.SeriesName = episodeMatches.First().SeriesName;
      episodeInfo.SeriesFirstAired = episodeMatches.First().SeriesFirstAired;

      episodeInfo.SeasonNumber = episodeMatches.First().SeasonNumber;
      episodeInfo.EpisodeNumbers = episodeMatches.SelectMany(x => x.EpisodeNumbers).ToList();
      episodeInfo.DvdEpisodeNumbers = episodeMatches.SelectMany(x => x.DvdEpisodeNumbers).ToList();
      episodeInfo.FirstAired = episodeMatches.First().FirstAired;
      episodeInfo.TotalRating = episodeMatches.Sum(e => e.TotalRating) / episodeMatches.Count; // Average rating
      episodeInfo.RatingCount = episodeMatches.Sum(e => e.RatingCount) / episodeMatches.Count; // Average rating count
      episodeInfo.EpisodeName = string.Join("; ", episodeMatches.OrderBy(e => e.EpisodeNumbers[0]).Select(e => e.EpisodeName.Text).ToArray());
      episodeInfo.EpisodeName.DefaultLanguage = episodeMatches.First().EpisodeName.DefaultLanguage;
      episodeInfo.Summary = string.Join("\r\n\r\n", episodeMatches.OrderBy(e => e.EpisodeNumbers[0]).
        Select(e => string.Format("{0,02}) {1}", e.EpisodeNumbers[0], e.Summary.Text)).ToArray());
      episodeInfo.Summary.DefaultLanguage = episodeMatches.First().Summary.DefaultLanguage;

      episodeInfo.Genres = episodeMatches.SelectMany(e => e.Genres).Distinct().ToList();
      episodeInfo.Actors = episodeMatches.SelectMany(e => e.Actors).Distinct().ToList();
      episodeInfo.Directors = episodeMatches.SelectMany(e => e.Directors).Distinct().ToList();
      episodeInfo.Writers = episodeMatches.SelectMany(e => e.Writers).Distinct().ToList();
    }

    protected virtual void SetEpisodeDetails(EpisodeInfo episodeInfo, EpisodeInfo episodeMatch)
    {
      episodeInfo.ImdbId = episodeMatch.ImdbId;
      episodeInfo.TvdbId = episodeMatch.TvdbId;
      episodeInfo.MovieDbId = episodeMatch.MovieDbId;
      episodeInfo.TvMazeId = episodeMatch.TvMazeId;
      episodeInfo.TvRageId = episodeMatch.TvRageId;

      episodeInfo.SeriesImdbId = episodeMatch.SeriesImdbId;
      episodeInfo.SeriesMovieDbId = episodeMatch.SeriesMovieDbId;
      episodeInfo.SeriesTvdbId = episodeMatch.SeriesTvdbId;
      episodeInfo.SeriesTvRageId = episodeMatch.SeriesTvRageId;
      episodeInfo.SeriesTvMazeId = episodeMatch.SeriesTvMazeId;
      episodeInfo.SeriesName = episodeMatch.SeriesName;
      episodeInfo.SeriesFirstAired = episodeMatch.SeriesFirstAired;

      episodeInfo.SeasonNumber = episodeMatch.SeasonNumber;
      episodeInfo.EpisodeNumbers = episodeMatch.EpisodeNumbers;
      episodeInfo.DvdEpisodeNumbers = episodeMatch.DvdEpisodeNumbers;
      episodeInfo.FirstAired = episodeMatch.FirstAired;
      episodeInfo.TotalRating = episodeMatch.TotalRating;
      episodeInfo.RatingCount = episodeMatch.RatingCount;
      episodeInfo.EpisodeName = episodeMatch.EpisodeName;
      episodeInfo.Summary = episodeMatch.Summary;

      episodeInfo.Genres = episodeMatch.Genres;
      episodeInfo.Actors = episodeMatch.Actors;
      episodeInfo.Directors = episodeMatch.Directors;
      episodeInfo.Writers = episodeMatch.Writers;
    }

    #endregion

    #region Persons

    /// <summary>
    /// Search for Person.
    /// </summary>
    /// <param name="personSearch">Person search parameters.</param>
    /// <param name="language">Language, if <c>null</c> it takes the <see cref="PreferredLanguage"/></param>
    /// <param name="persons">Returns the list of matches.</param>
    /// <returns><c>true</c> if at least one Person was found.</returns>
    public virtual bool SearchPerson(PersonInfo personSearch, TLang language, out List<PersonInfo> persons)
    {
      persons = null;
      return false;
    }

    /// <summary>
    /// Search for unique matches of Persons. This method tries to find the best matching Movie in following order:
    /// - Exact match using PreferredLanguage
    /// - Exact match using DefaultLanguage
    /// </summary>
    /// <param name="personSearch">Person search parameters.</param>
    /// <param name="language">Language, if <c>null</c> it takes the <see cref="PreferredLanguage"/></param>
    /// <param name="persons">Returns the list of matches.</param>
    /// <returns><c>true</c> if at exactly one Person was found.</returns>
    public bool SearchPersonUniqueAndUpdate(PersonInfo personSearch, TLang language)
    {
      List<PersonInfo> persons;
      language = language != null ? language : PreferredLanguage;

      if (!SearchPerson(personSearch, language, out persons))
        return false;
      if (TestPersonMatch(personSearch, ref persons))
      {
        personSearch.CopyIdsFrom(persons[0]);
        return true;
      }

      if (persons.Count == 0 && !language.Equals(_defaultLanguage))
      {
        if (!SearchPerson(personSearch, _defaultLanguage, out persons))
          return false;
        if(persons.Count == 1)
        {
          personSearch.CopyIdsFrom(persons[0]);
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Tests for person matches. 
    /// </summary>
    /// <param name="personSearch">Person search parameters.</param>
    /// <param name="persons">Potential online matches. The collection will be modified inside this method.</param>
    /// <returns><c>true</c> if unique match</returns>
    protected virtual bool TestPersonMatch(PersonInfo personSearch, ref List<PersonInfo> persons)
    {
      // Exact match in preferred language
      ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Test Match for \"{0}\"", personSearch);

      if (persons.Count == 1)
      {
        if (GetLevenshteinDistance(persons[0], personSearch) <= MAX_LEVENSHTEIN_DIST)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", personSearch);
          return true;
        }
        // No valid match, clear list to allow further detection ways
        persons.Clear();
        return false;
      }

      // Multiple matches
      if (persons.Count > 1)
      {
        ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches for \"{0}\" ({1}). Try to find exact name match.", personSearch, persons.Count);
        var exactMatches = persons.FindAll(p => p.Name == personSearch.Name || GetLevenshteinDistance(p, personSearch) == 0);
        if (exactMatches.Count == 1)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", personSearch);
          persons = exactMatches;
          return true;
        }

        persons = persons.Where(p => GetLevenshteinDistance(p, personSearch) <= MAX_LEVENSHTEIN_DIST).ToList();
        if (persons.Count > 1)
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches found for \"{0}\" (count: {1})", personSearch, persons.Count);

      }
      return persons.Count == 1;
    }

    public virtual bool UpdateFromOnlineMoviePerson(PersonInfo person, TLang language, bool cacheOnly)
    {
      return false;
    }

    public virtual bool UpdateFromOnlineSeriesPerson(PersonInfo person, TLang language, bool cacheOnly)
    {
      return false;
    }

    public virtual bool UpdateFromOnlineMusicPerson(PersonInfo person, TLang language, bool cacheOnly)
    {
      return false;
    }

    #endregion

    #region Characters

    /// <summary>
    /// Search for Character.
    /// </summary>
    /// <param name="characterSearch">Character search parameters.</param>
    /// <param name="language">Language, if <c>null</c> it takes the <see cref="PreferredLanguage"/></param>
    /// <param name="persons">Returns the list of matches.</param>
    /// <returns><c>true</c> if at least one Person was found.</returns>
    public virtual bool SearchCharacter(CharacterInfo characterSearch, TLang language, out List<CharacterInfo> characters)
    {
      characters = null;
      return false;
    }

    /// <summary>
    /// Search for unique matches of Character. This method tries to find the best matching Movie in following order:
    /// - Exact match using PreferredLanguage
    /// - Exact match using DefaultLanguage
    /// </summary>
    /// <param name="characterSearch">Character search parameters.</param>
    /// <param name="language">Language, if <c>null</c> it takes the <see cref="PreferredLanguage"/></param>
    /// <param name="persons">Returns the list of matches.</param>
    /// <returns><c>true</c> if at exactly one Person was found.</returns>
    public bool SearchCharacterUniqueAndUpdate(CharacterInfo characterSearch, TLang language)
    {
      List<CharacterInfo> characters;
      language = language != null ? language : PreferredLanguage;

      if (!SearchCharacter(characterSearch, language, out characters))
        return false;
      if (TestCharacterMatch(characterSearch, ref characters))
      {
        characterSearch.CopyIdsFrom(characters[0]);
        return true;
      }

      if (characters.Count == 0 && !language.Equals(_defaultLanguage))
      {
        if (!SearchCharacter(characterSearch, _defaultLanguage, out characters))
          return false;
        if(characters.Count == 1)
        {
          characterSearch.CopyIdsFrom(characters[0]);
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Tests for Character matches. 
    /// </summary>
    /// <param name="characterSearch">Character search parameters.</param>
    /// <param name="persons">Potential online matches. The collection will be modified inside this method.</param>
    /// <returns><c>true</c> if unique match</returns>
    protected virtual bool TestCharacterMatch(CharacterInfo characterSearch, ref List<CharacterInfo> characters)
    {
      // Exact match in preferred language
      ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Test Match for \"{0}\"", characterSearch);

      if (characters.Count == 1)
      {
        if (GetLevenshteinDistance(characters[0], characterSearch) <= MAX_LEVENSHTEIN_DIST)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", characterSearch);
          return true;
        }
        // No valid match, clear list to allow further detection ways
        characters.Clear();
        return false;
      }

      // Multiple matches
      if (characters.Count > 1)
      {
        ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches for \"{0}\" ({1}). Try to find exact name match.", characterSearch, characters.Count);
        var exactMatches = characters.FindAll(p => p.Name == characterSearch.Name || GetLevenshteinDistance(p, characterSearch) <= MAX_LEVENSHTEIN_DIST);
        if (exactMatches.Count == 1)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", characterSearch);
          characters = exactMatches;
          return true;
        }

        characters = characters.Where(p => GetLevenshteinDistance(p, characterSearch) <= MAX_LEVENSHTEIN_DIST).ToList();
        if (characters.Count > 1)
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches found for \"{0}\" (count: {1})", characterSearch, characters.Count);

      }
      return characters.Count == 1;
    }

    public virtual bool UpdateFromOnlineMovieCharacter(CharacterInfo character, TLang language, bool cacheOnly)
    {
      return false;
    }

    public virtual bool UpdateFromOnlineSeriesCharacter(CharacterInfo character, TLang language, bool cacheOnly)
    {
      return false;
    }

    #endregion

    #region Companies

    /// <summary>
    /// Search for Company.
    /// </summary>
    /// <param name="companySearch">Company search parameters.</param>
    /// <param name="language">Language, if <c>null</c> it takes the <see cref="PreferredLanguage"/></param>
    /// <param name="companies">Returns the list of matches.</param>
    /// <returns><c>true</c> if at least one Company was found.</returns>
    public virtual bool SearchCompany(CompanyInfo companySearch, TLang language, out List<CompanyInfo> companies)
    {
      companies = null;
      return false;
    }

    /// <summary>
    /// Search for unique matches of Company names. This method tries to find the best matching Movie in following order:
    /// - Exact match using PreferredLanguage
    /// - Exact match using DefaultLanguage
    /// </summary>
    /// <param name="companySearch">Company search parameters.</param>
    /// <param name="language">Language, if <c>null</c> it takes the <see cref="PreferredLanguage"/></param>
    /// <param name="companies">Returns the list of matches.</param>
    /// <returns><c>true</c> if at exactly one Company was found.</returns>
    public bool SearchCompanyUniqueAndUpdate(CompanyInfo companySearch, TLang language)
    {
      List<CompanyInfo> companies;
      language = language != null ? language : PreferredLanguage;

      if (!SearchCompany(companySearch, language, out companies))
        return false;
      if (TestCompanyMatch(companySearch, ref companies))
      {
        companySearch.CopyIdsFrom(companies[0]);
        return true;
      }

      if (companies.Count == 0 && !language.Equals(_defaultLanguage))
      {
        if (!SearchCompany(companySearch, _defaultLanguage, out companies))
          return false;

        if(companies.Count == 1)
        {
          companySearch.CopyIdsFrom(companies[0]);
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Tests for Company matches. 
    /// </summary>
    /// <param name="companySearch">Company search parameters.</param>
    /// <param name="companies">Potential online matches. The collection will be modified inside this method.</param>
    /// <returns><c>true</c> if unique match</returns>
    protected virtual bool TestCompanyMatch(CompanyInfo companySearch, ref List<CompanyInfo> companies)
    {
      // Exact match in preferred language
      ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Test Match for \"{0}\"", companySearch);

      if (companies.Count == 1)
      {
        if (GetLevenshteinDistance(companies[0], companySearch) <= MAX_LEVENSHTEIN_DIST)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", companySearch);
          return true;
        }
        // No valid match, clear list to allow further detection ways
        companies.Clear();
        return false;
      }

      // Multiple matches
      if (companies.Count > 1)
      {
        ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches for \"{0}\" ({1}). Try to find exact name match.", companySearch, companies.Count);
        var exactMatches = companies.FindAll(c => c.Name == companySearch.Name || GetLevenshteinDistance(c, companySearch) == 0);
        if (exactMatches.Count == 1)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", companySearch);
          companies = exactMatches;
          return true;
        }

        companies = companies.Where(c => GetLevenshteinDistance(c, companySearch) <= MAX_LEVENSHTEIN_DIST).ToList();
        if (companies.Count > 1)
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches found for \"{0}\" (count: {1})", companySearch, companies.Count);

      }
      return companies.Count == 1;
    }

    public virtual bool UpdateFromOnlineMovieCompany(CompanyInfo company, TLang language, bool cacheOnly)
    {
      return false;
    }

    public virtual bool UpdateFromOnlineSeriesCompany(CompanyInfo company, TLang language, bool cacheOnly)
    {
      return false;
    }

    public virtual bool UpdateFromOnlineMusicCompany(CompanyInfo company, TLang language, bool cacheOnly)
    {
      return false;
    }

    #endregion

    #region Music

    public virtual bool SearchTrack(TrackInfo trackSearch, TLang language, out List<TrackInfo> tracks)
    {
      tracks = null;
      return false;
    }

    public bool SearchTrackUniqueAndUpdate(TrackInfo trackSearch, TLang language)
    {
      List<TrackInfo> tracks;
      language = language != null ? language : PreferredLanguage;

      if (!SearchTrack(trackSearch, language, out tracks))
        return false;
      if (TestTrackMatch(trackSearch, ref tracks))
      {
        trackSearch.CopyIdsFrom(tracks[0]);
        return true;
      }

      if (tracks.Count == 0 && !language.Equals(_defaultLanguage))
      {
        if (!SearchTrack(trackSearch, _defaultLanguage, out tracks))
          return false;
        if (tracks.Count == 1)
        {
          trackSearch.CopyIdsFrom(tracks[0]);
          return true;
        }
      }
      return false;
    }

    protected virtual bool TestTrackMatch(TrackInfo trackSearch, ref List<TrackInfo> tracks)
    {
      if (tracks.Count == 1)
      {
        if (GetLevenshteinDistance(tracks[0], trackSearch) <= MAX_LEVENSHTEIN_DIST)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name +  ": Unique match found \"{0}\"!", trackSearch);
          return true;
        }
        // No valid match, clear list to allow further detection ways
        tracks.Clear();
        return false;
      }

      // Multiple matches
      if (tracks.Count > 1)
      {
        ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches for \"{0}\" ({1}). Try to find exact name match.", trackSearch, tracks.Count);
        var exactMatches = tracks.FindAll(t => t.TrackName == trackSearch.TrackName || GetLevenshteinDistance(t, trackSearch) == 0);
        if (exactMatches.Count == 1)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", trackSearch);
          tracks = exactMatches;
          return true;
        }

        if (exactMatches.Count > 1)
        {
          var lastGood = exactMatches;
          foreach (AudioValueToCheck checkValue in Enum.GetValues(typeof(AudioValueToCheck)))
          {
            if (checkValue == AudioValueToCheck.ArtistLax && trackSearch.Artists != null && trackSearch.Artists.Count > 0)
              exactMatches = exactMatches.FindAll(t => CompareArtists(t.Artists, trackSearch.Artists, false));

            if (checkValue == AudioValueToCheck.AlbumLax && !string.IsNullOrEmpty(trackSearch.Album))
              exactMatches = exactMatches.FindAll(t => GetLevenshteinDistance(t.CloneBasicAlbum(), trackSearch.CloneBasicAlbum()) <= MAX_LEVENSHTEIN_DIST);

            if (checkValue == AudioValueToCheck.ArtistStrict && trackSearch.Artists != null && trackSearch.Artists.Count > 0)
              exactMatches = exactMatches.FindAll(t => CompareArtists(t.Artists, trackSearch.Artists, true));

            if (checkValue == AudioValueToCheck.AlbumStrict && !string.IsNullOrEmpty(trackSearch.Album))
              exactMatches = exactMatches.FindAll(t => t.Album == trackSearch.Album || GetLevenshteinDistance(t.CloneBasicAlbum(), trackSearch.CloneBasicAlbum()) == 0);

            if (checkValue == AudioValueToCheck.Year && trackSearch.ReleaseDate.HasValue)
              exactMatches = exactMatches.FindAll(t => t.ReleaseDate.HasValue && t.ReleaseDate.Value.Year == trackSearch.ReleaseDate.Value.Year);

            if (checkValue == AudioValueToCheck.TrackNum && trackSearch.TrackNum > 0)
              exactMatches = exactMatches.FindAll(t => t.TrackNum > 0 && t.TrackNum == trackSearch.TrackNum);

            if (checkValue == AudioValueToCheck.Discs)
              exactMatches = exactMatches.FindAll(t => t.DiscNum > 0 || t.TotalDiscs > 0);

            if (checkValue == AudioValueToCheck.Language)
            {
              exactMatches = exactMatches.FindAll(t => t.Languages.Contains(PreferredLanguage.ToString()) || t.Languages.Count == 0);
              if (exactMatches.Count == 0)
                exactMatches = lastGood.FindAll(t => t.Languages.Contains(DefaultLanguage.ToString()) || t.Languages.Count == 0);
            }

            if (exactMatches.Count == 0) //Too many were removed restore last good
              exactMatches = lastGood;
            else
              lastGood = exactMatches;

            if (exactMatches.Count == 1)
            {
              ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\" [{1}]!", trackSearch, checkValue.ToString());
              tracks = exactMatches;
              return true;
            }
          }

          tracks = lastGood;
        }

        if (tracks.Count > 1)
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches found for \"{0}\" (count: {1})", trackSearch, tracks.Count);

        return tracks.Count == 1;
      }
      return false;
    }

    public virtual bool SearchTrackAlbum(AlbumInfo albumSearch, TLang language, out List<AlbumInfo> albums)
    {
      albums = null;
      return false;
    }

    public bool SearchTrackAlbumUniqueAndUpdate(AlbumInfo albumSearch, TLang language)
    {
      List<AlbumInfo> albums;
      language = language != null ? language : PreferredLanguage;

      if (!SearchTrackAlbum(albumSearch, language, out albums))
        return false;
      if (TestAlbumMatch(albumSearch, ref albums))
      {
        albumSearch.CopyIdsFrom(albums[0]);
        return true;
      }

      if (albums.Count == 0 && !language.Equals(_defaultLanguage))
      {
        if (!SearchTrackAlbum(albumSearch, _defaultLanguage, out albums))
          return false;
        if (albums.Count == 1)
        {
          albumSearch.CopyIdsFrom(albums[0]);
          return true;
        }
      }
      return false;
    }

    protected virtual bool TestAlbumMatch(AlbumInfo albumSearch, ref List<AlbumInfo> albums)
    {
      if (albums.Count == 1)
      {
        if (GetLevenshteinDistance(albums[0], albumSearch) <= MAX_LEVENSHTEIN_DIST)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", albumSearch);
          return true;
        }
        // No valid match, clear list to allow further detection ways
        albums.Clear();
        return false;
      }

      // Multiple matches
      if (albums.Count > 1)
      {
        ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches for \"{0}\" ({1}). Try to find exact name match.", albumSearch, albums.Count);
        var exactMatches = albums.FindAll(t => t.Album == albumSearch.Album || GetLevenshteinDistance(t, albumSearch) == 0);
        if (exactMatches.Count == 1)
        {
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\"!", albumSearch);
          albums = exactMatches;
          return true;
        }

        if (exactMatches.Count > 1)
        {
          var lastGood = exactMatches;
          foreach (AudioValueToCheck checkValue in Enum.GetValues(typeof(AudioValueToCheck)))
          {
            if (checkValue == AudioValueToCheck.ArtistLax && albumSearch.Artists != null && albumSearch.Artists.Count > 0)
              exactMatches = exactMatches.FindAll(a => CompareArtists(a.Artists, albumSearch.Artists, false));

            if (checkValue == AudioValueToCheck.AlbumLax && !string.IsNullOrEmpty(albumSearch.Album))
              exactMatches = exactMatches.FindAll(a => GetLevenshteinDistance(a, albumSearch) <= MAX_LEVENSHTEIN_DIST);

            if (checkValue == AudioValueToCheck.ArtistStrict && albumSearch.Artists != null && albumSearch.Artists.Count > 0)
              exactMatches = exactMatches.FindAll(a => CompareArtists(a.Artists, albumSearch.Artists, true));

            if (checkValue == AudioValueToCheck.AlbumStrict && !string.IsNullOrEmpty(albumSearch.Album))
              exactMatches = exactMatches.FindAll(a => a.Album == albumSearch.Album || GetLevenshteinDistance(a, albumSearch) == 0);

            if (checkValue == AudioValueToCheck.Year && albumSearch.ReleaseDate.HasValue)
              exactMatches = exactMatches.FindAll(a => a.ReleaseDate.HasValue && a.ReleaseDate.Value.Year == albumSearch.ReleaseDate.Value.Year);

            if (checkValue == AudioValueToCheck.TrackNum && albumSearch.TotalTracks > 0)
              exactMatches = exactMatches.FindAll(a => a.TotalTracks > 0 && a.TotalTracks == albumSearch.TotalTracks);

            if (checkValue == AudioValueToCheck.Discs)
              exactMatches = exactMatches.FindAll(a => a.DiscNum > 0 || a.TotalDiscs > 0);

            if (checkValue == AudioValueToCheck.Language)
            {
              exactMatches = exactMatches.FindAll(a => a.Languages.Contains(PreferredLanguage.ToString()) || a.Languages.Count == 0);
              if (exactMatches.Count == 0)
                exactMatches = lastGood.FindAll(a => a.Languages.Contains(DefaultLanguage.ToString()) || a.Languages.Count == 0);
            }

            if (checkValue == AudioValueToCheck.Compilation)
              exactMatches = exactMatches.FindAll(s => s.Compilation == false);

            if (exactMatches.Count == 0) //Too many were removed restore last good
              exactMatches = lastGood;
            else
              lastGood = exactMatches;

            if (exactMatches.Count == 1)
            {
              ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Unique match found \"{0}\" [{1}]!", albumSearch, checkValue.ToString());
              albums = exactMatches;
              return true;
            }
          }

          albums = lastGood;
        }

        if (albums.Count > 1)
          ServiceRegistration.Get<ILogger>().Debug(GetType().Name + ": Multiple matches found for \"{0}\" (count: {1})", albumSearch, albums.Count);

        return albums.Count == 1;
      }
      return false;
    }

    public virtual bool UpdateFromOnlineMusicTrack(TrackInfo track, TLang language, bool cacheOnly)
    {
      return false;
    }

    public virtual bool UpdateFromOnlineMusicTrackAlbum(AlbumInfo album, TLang language, bool cacheOnly)
    {
      return false;
    }

    private bool CompareArtists(List<PersonInfo> trackArtists, List<PersonInfo> searchArtists, bool strict)
    {
      if (strict)
      {
        foreach (PersonInfo trackArtist in trackArtists)
        {
          bool artistFound = false;
          foreach (PersonInfo searchArtist in searchArtists)
            if (trackArtist == searchArtist || GetLevenshteinDistance(trackArtist, searchArtist) == 0)
            {
              artistFound = true;
              break;
            }
          if (!artistFound)
            return false;
        }
      }
      else
      {
        foreach (PersonInfo trackArtist in trackArtists)
        {
          foreach (PersonInfo searchArtist in searchArtists)
          {
            if (GetLevenshteinDistance(trackArtist, searchArtist) <= MAX_LEVENSHTEIN_DIST)
              return true;
          }
        }
      }
      return false;
    }

    #endregion

    #region FanArt

    public virtual bool GetFanArt<T>(T infoObject, TLang language, string scope, out FanArtImageCollection<TImg> images)
    {
      images = null;
      return false;
    }

    public virtual bool DownloadFanArt(string id, TImg image, string scope, string type)
    {
      return false;
    }

    public virtual bool DownloadSeriesSeasonFanArt(string id, int seasonNo, TImg image, string scope, string type)
    {
      return false;
    }

    public virtual bool DownloadSeriesEpisodeFanArt(string id, int seasonNo, int episodeNo, TImg image, string scope, string type)
    {
      return false;
    }

    #endregion

    #region Name comparing

    /// <summary>
    /// Removes special characters and compares the remaining strings. Strings are processed by <see cref="RemoveCharacters"/> before comparing.
    /// The result is <c>true</c>, if the cleaned strings are equal or have a Levenshtein distance less or equal to <see cref="MAX_LEVENSHTEIN_DIST"/>.
    /// </summary>
    /// <param name="name1">Name 1</param>
    /// <param name="name2">Name 2</param>
    /// <returns><c>true</c> if similar or equal</returns>
    protected bool IsSimilarOrEqual(string name1, string name2)
    {
      return string.Equals(RemoveCharacters(name1), RemoveCharacters(name2)) || StringUtils.GetLevenshteinDistance(name1, name2) <= MAX_LEVENSHTEIN_DIST;
    }

    /// <summary>
    /// Returns the Levenshtein distance for a <see cref="MovieSearchResult"/> and a given <paramref name="movieName"/>.
    /// It considers both <see cref="OnlineMovieMatch.MovieName"/> and <see cref="OnlineMovieMatch.MovieOriginalName"/>
    /// </summary>
    /// <param name="movieOnline">Movie search result</param>
    /// <param name="movieSearch">Movie search parameters</param>
    /// <returns>Levenshtein distance</returns>
    protected int GetLevenshteinDistance(MovieInfo movieOnline, MovieInfo movieSearch)
    {
      string cleanedName = RemoveCharacters(movieSearch.MovieName.Text);
      if (string.IsNullOrEmpty(movieOnline.OriginalName))
        return StringUtils.GetLevenshteinDistance(RemoveCharacters(movieOnline.MovieName.Text), cleanedName);
      else
        return Math.Min(
        StringUtils.GetLevenshteinDistance(RemoveCharacters(movieOnline.MovieName.Text), cleanedName),
        StringUtils.GetLevenshteinDistance(RemoveCharacters(movieOnline.OriginalName), cleanedName)
        );
    }

    protected int GetLevenshteinDistance(EpisodeInfo episodeOnline, EpisodeInfo episodeSearch)
    {
      string cleanedName = RemoveCharacters(episodeSearch.EpisodeName.Text);
      return StringUtils.GetLevenshteinDistance(RemoveCharacters(episodeOnline.EpisodeName.Text), cleanedName);
    }

    protected int GetLevenshteinDistance(SeriesInfo seriesOnline, SeriesInfo seriesSearch)
    {
      string cleanedName = RemoveCharacters(seriesSearch.SeriesName.Text);
      if (string.IsNullOrEmpty(seriesOnline.OriginalName))
        return StringUtils.GetLevenshteinDistance(RemoveCharacters(seriesOnline.SeriesName.Text), cleanedName);
      else
        return Math.Min(
          StringUtils.GetLevenshteinDistance(RemoveCharacters(seriesOnline.SeriesName.Text), cleanedName),
          StringUtils.GetLevenshteinDistance(RemoveCharacters(seriesOnline.OriginalName), cleanedName)
        );
    }

    protected int GetLevenshteinDistance(PersonInfo personOnline, PersonInfo personSearch)
    {
      string cleanedName = RemoveCharacters(personSearch.Name);
      return StringUtils.GetLevenshteinDistance(RemoveCharacters(personOnline.Name), cleanedName);
    }

    protected int GetLevenshteinDistance(CharacterInfo characterOnline, CharacterInfo characterSearch)
    {
      string cleanedName = RemoveCharacters(characterSearch.Name);
      return StringUtils.GetLevenshteinDistance(RemoveCharacters(characterOnline.Name), cleanedName);
    }

    protected int GetLevenshteinDistance(CompanyInfo company, CompanyInfo companySearch)
    {
      string cleanedName = RemoveCharacters(companySearch.Name);
      return StringUtils.GetLevenshteinDistance(RemoveCharacters(company.Name), cleanedName);
    }

    protected int GetLevenshteinDistance(TrackInfo trackOnline, TrackInfo trackSearch)
    {
      string cleanedName = RemoveCharacters(trackSearch.TrackName);
      return StringUtils.GetLevenshteinDistance(RemoveCharacters(trackOnline.TrackName), cleanedName);
    }

    protected int GetLevenshteinDistance(AlbumInfo albumOnline, AlbumInfo albumSearch)
    {
      string cleanedName = RemoveCharacters(albumSearch.Album);
      return StringUtils.GetLevenshteinDistance(RemoveCharacters(albumOnline.Album), cleanedName);
    }

    /// <summary>
    /// Replaces characters that are not necessary for comparing (like whitespaces) and diacritics. The result is returned as <see cref="string.ToLowerInvariant"/>.
    /// </summary>
    /// <param name="name">Name to clean up</param>
    /// <returns>Cleaned string</returns>
    protected string RemoveCharacters(string name)
    {
      if (string.IsNullOrEmpty(name))
        return name;

      name = name.ToLowerInvariant();
      string result = new[] { "-", ",", "/", ":", " ", " ", ".", "'", "(", ")", "[", "]", "teil", "part" }.Aggregate(name, (current, s) => current.Replace(s, ""));
      result = result.Replace("&", "and");
      return StringUtils.RemoveDiacritics(result);
    }

    #endregion
  }
}