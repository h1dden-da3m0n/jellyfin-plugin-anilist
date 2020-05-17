﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Anime.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Anime.Providers.AniList
{
    /// <summary>
    /// Based on the new API from AniList
    /// 🛈 This code works with the API Interface (v2) from AniList
    /// 🛈 https://anilist.gitbooks.io/anilist-apiv2-docs
    /// 🛈 THIS IS AN UNOFFICAL API INTERFACE FOR EMBY
    /// </summary>
    public class AniListApi
    {
        private static readonly HttpClient _httpClient;
        private const string SearchLink = @"https://graphql.anilist.co/api/v2?query=
query ($query: String, $type: MediaType) {
  Page {
    media(search: $query, type: $type) {
      id
      title {
        romaji
        english
        native
      }
      coverImage {
        medium
        large
      }
      format
      type
      averageScore
      popularity
      episodes
      season
      hashtag
      isAdult
      startDate {
        year
        month
        day
      }
      endDate {
        year
        month
        day
      }
    }
  }
}&variables={ ""query"":""{0}"",""type"":""ANIME""}";
        public string AniList_anime_link = @"https://graphql.anilist.co/api/v2?query=query($id: Int!, $type: MediaType) {
  Media(id: $id, type: $type)
    {
      id
      title {
        romaji
        english
        native
        userPreferred
      }
      startDate {
        year
        month
        day
      }
      endDate {
        year
        month
        day
      }
      coverImage {
        large
        medium
      }
      bannerImage
      format
      type
      status
      episodes
      chapters
      volumes
      season
      seasonYear
      description
      averageScore
      meanScore
      genres
      synonyms
      duration
      tags {
        id
        name
        category
      }
      nextAiringEpisode {
        airingAt
        timeUntilAiring
        episode
      }
    }
}&variables={ ""id"":""{0}"",""type"":""ANIME""}";
        private const string AniList_anime_char_link = @"https://graphql.anilist.co/api/v2?query=query($id: Int!, $type: MediaType, $page: Int = 1) {
  Media(id: $id, type: $type) {
    id
    characters(page: $page, sort: [ROLE]) {
      pageInfo {
        total
        perPage
        hasNextPage
        currentPage
        lastPage
      }
      edges {
        node {
          id
          name {
            first
            last
          }
          image {
            medium
            large
          }
        }
        role
        voiceActors {
          id
          name {
            first
            last
            native
          }
          image {
            medium
            large
          }
          language
        }
      }
    }
  }
}&variables={ ""id"":""{0}"",""type"":""ANIME""}";

        static AniListApi()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);
        }

        /// <summary>
        /// API call to get the anime with the id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<Media> GetAnime(string id)
        {
            RootObject WebContent = await WebRequestAPI(AniList_anime_link.Replace("{0}",id));
            return WebContent.data.Media;
        }

        public async Task<List<PersonInfo>> GetPersonInfo(int id, CancellationToken cancellationToken)
        {
            List<PersonInfo> lpi = new List<PersonInfo>();
            RootObject WebContent = await WebRequestAPI(AniList_anime_char_link.Replace("{0}", id.ToString()));
            foreach (Edge edge in WebContent.data.Media.characters.edges)
            {
                PersonInfo pi = new PersonInfo();
                pi.Name = edge.node.name.first+" "+ edge.node.name.last;
                pi.ImageUrl = edge.node.image.large;
                pi.Role = edge.role;
            }
            return lpi;
        }

        /// <summary>
        /// API call to search a title and return the first result
        /// </summary>
        /// <param name="title"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Media> Search_GetSeries(string title, CancellationToken cancellationToken)
        {
            // Reimplemented instead of calling Search_GetSeries_list() for efficiency
            RootObject WebContent = await WebRequestAPI(SearchLink.Replace("{0}", title));
            foreach (Media media in WebContent.data.Page.media) {
                return media;
            }
            return null;
        }

        /// <summary>
        /// API call to search a title and return a list back
        /// </summary>
        /// <param name="title"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<List<Media>> Search_GetSeries_list(string title, CancellationToken cancellationToken)
        {
            RootObject WebContent = await WebRequestAPI(SearchLink.Replace("{0}", title));
            return WebContent.data.Page.media;
        }

        /// <summary>
        /// SEARCH Title
        /// </summary>
        public async Task<string> FindSeries(string title, CancellationToken cancellationToken)
        {
            Media result = await Search_GetSeries(title, cancellationToken);
            if (result != null)
            {
                return result.id.ToString();
            }
            result = await Search_GetSeries(await Equals_check.Clear_name(title, cancellationToken), cancellationToken);
            if (result != null)
            {
                return result.id.ToString();
            }
            return null;
        }

        /// <summary>
        /// GET website content from the link
        /// </summary>
        public async Task<RootObject> WebRequestAPI(string link)
        {
            using (HttpContent content = new FormUrlEncodedContent(Enumerable.Empty<KeyValuePair<string, string>>()))
            using (var response = await _httpClient.PostAsync(link, content).ConfigureAwait(false))
            using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                return await JsonSerializer.DeserializeAsync<RootObject>(responseStream).ConfigureAwait(false);
            }
        }
    }
}
