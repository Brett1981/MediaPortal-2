﻿using System;
using System.Collections.Generic;
using System.Linq;
using HttpServer;
using HttpServer.Exceptions;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Plugins.MP2Extended.Common;
using MediaPortal.Plugins.MP2Extended.MAS;
using MediaPortal.Plugins.MP2Extended.MAS.TvShow;
using MediaPortal.Plugins.MP2Extended.ResourceAccess.MAS.TvShow.BaseClasses;

namespace MediaPortal.Plugins.MP2Extended.ResourceAccess.MAS.TvShow
{
  internal class GetTVEpisodeBasicById : BaseEpisodeBasic, IRequestMicroModuleHandler
  {
    public dynamic Process(IHttpRequest request)
    {
      HttpParam httpParam = request.Param;
      if (httpParam["id"].Value == null)
        throw new BadRequestException("GetTVEpisodeBasicById: no id is null");

      ISet<Guid> necessaryMIATypes = new HashSet<Guid>();
      necessaryMIATypes.Add(MediaAspect.ASPECT_ID);
      necessaryMIATypes.Add(ProviderResourceAspect.ASPECT_ID);
      necessaryMIATypes.Add(ImporterAspect.ASPECT_ID);
      necessaryMIATypes.Add(SeriesAspect.ASPECT_ID);

      MediaItem item = GetMediaItems.GetMediaItemById(httpParam["id"].Value, necessaryMIATypes);

      if (item == null)
        throw new BadRequestException(String.Format("GetTvEpisodeBasicById: No MediaItem found with id: {0}", httpParam["id"].Value));

      WebTVEpisodeBasic webTvEpisodeBasic = EpisodeBasic(item);

      return webTvEpisodeBasic;
    }

    internal static ILogger Logger
    {
      get { return ServiceRegistration.Get<ILogger>(); }
    }
  }
}