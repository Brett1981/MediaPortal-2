﻿using System;
using System.Security.Policy;
using HttpServer;
using HttpServer.Exceptions;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Plugins.MP2Extended.Common;
using MediaPortal.Plugins.MP2Extended.ResourceAccess.MAS.General;
using MediaPortal.Plugins.MP2Extended.ResourceAccess.WSS.stream;
using MediaPortal.Plugins.MP2Extended.WSS.General;

namespace MediaPortal.Plugins.MP2Extended.ResourceAccess.WSS.json.General
{
  internal class StartStreamWithStreamSelection : IRequestMicroModuleHandler
  {
    public dynamic Process(IHttpRequest request)
    {
      HttpParam httpParam = request.Param;
      
      string identifier = httpParam["identifier"].Value;
      string profileName = httpParam["profileName"].Value;
      string startPosition = httpParam["startPosition"].Value;
      string audioId = httpParam["audioId"].Value;
      string subtitleId = httpParam["subtitleId"].Value;

      if (identifier == null)
        throw new BadRequestException("StartStreamWithStreamSelection: identifier is null");
      if (profileName == null)
        throw new BadRequestException("StartStreamWithStreamSelection: profileName is null");
      if (startPosition == null)
        throw new BadRequestException("StartStreamWithStreamSelection: startPosition is null");
      if (audioId == null)
        throw new BadRequestException("StartStreamWithStreamSelection: audioId is null");
      if (subtitleId == null)
        throw new BadRequestException("StartStreamWithStreamSelection: subtitleId is null");

      long startPositionLong;
      if (!long.TryParse(startPosition, out startPositionLong))
        throw new BadRequestException(string.Format("StartStreamWithStreamSelection: Couldn't parse startPosition '{0}' to long", startPosition));

      int audioTrack;
      if (!int.TryParse(audioId, out audioTrack))
        throw new BadRequestException(string.Format("StartStreamWithStreamSelection: Couldn't parse audioId '{0}' to int", audioId));

      int subtitleTrack;
      if (!int.TryParse(subtitleId, out subtitleTrack))
        throw new BadRequestException(string.Format("StartStreamWithStreamSelection: Couldn't parse subtitleId '{0}' to int", subtitleId));

      if (!StreamControl.PROFILES.ContainsKey(profileName))
        throw new BadRequestException(string.Format("StartStreamWithStreamSelection: unknown profile: {0}", profileName));

      if (!StreamControl.ValidateIdentifie(identifier))
        throw new BadRequestException(string.Format("StartStreamWithStreamSelection: unknown identifier: {0}", identifier));

      EndPointProfile profile = StreamControl.PROFILES[profileName];

      StreamItem streamItem = StreamControl.GetStreamItem(identifier);
      streamItem.Profile = profile;
      streamItem.StartPosition = startPositionLong;

      StreamControl.UpdateStreamItem(identifier, streamItem);

      // Add the stream to the stream controler
      StreamControl.AddStreamItem(identifier, streamItem);

      // TODO: Return the proper URL
      return new WebStringResult { Result = ""};
    }

    internal static ILogger Logger
    {
      get { return ServiceRegistration.Get<ILogger>(); }
    }
  }
}