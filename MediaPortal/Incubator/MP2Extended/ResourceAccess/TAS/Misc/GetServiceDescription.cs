﻿using System.Collections.Generic;
using HttpServer;
using HttpServer.Exceptions;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Plugins.MP2Extended.MAS.General;
using MediaPortal.Plugins.MP2Extended.TAS.Misc;
using MediaPortal.Plugins.SlimTv.Interfaces;

namespace MediaPortal.Plugins.MP2Extended.ResourceAccess.TAS.Misc
{
  internal class GetServiceDescription : IRequestMicroModuleHandler
  {
    public dynamic Process(IHttpRequest request)
    { 
      WebTVServiceDescription webTVServiceDescription = new WebTVServiceDescription
      {
        ApiVersion = GlobalVersion.API_VERSION,
        HasConnectionToTVServer = ServiceRegistration.IsRegistered<ITvProvider>(),
        ServiceVersion = GlobalVersion.VERSION
      };

      return webTVServiceDescription;
    }

    internal static ILogger Logger
    {
      get { return ServiceRegistration.Get<ILogger>(); }
    }
  }
}