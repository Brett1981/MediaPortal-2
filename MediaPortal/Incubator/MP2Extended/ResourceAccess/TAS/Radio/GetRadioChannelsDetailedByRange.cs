﻿using System;
using System.Collections.Generic;
using System.Linq;
using HttpServer;
using HttpServer.Sessions;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Plugins.MP2Extended.Attributes;
using MediaPortal.Plugins.MP2Extended.Common;
using MediaPortal.Plugins.MP2Extended.Exceptions;
using MediaPortal.Plugins.MP2Extended.Extensions;
using MediaPortal.Plugins.MP2Extended.ResourceAccess.TAS.Tv.BaseClasses;
using MediaPortal.Plugins.MP2Extended.TAS.Tv;
using MediaPortal.Plugins.SlimTv.Interfaces;
using MediaPortal.Plugins.SlimTv.Interfaces.Items;
using MediaPortal.Plugins.SlimTv.Interfaces.UPnP.Items;
using Newtonsoft.Json;

namespace MediaPortal.Plugins.MP2Extended.ResourceAccess.TAS.Radio
{
  [ApiFunctionDescription(Type = ApiFunctionDescription.FunctionType.Json, Summary = "")]
  [ApiFunctionParam(Name = "groupId", Type = typeof(int), Nullable = false)]
  [ApiFunctionParam(Name = "start", Type = typeof(int), Nullable = false)]
  [ApiFunctionParam(Name = "end", Type = typeof(int), Nullable = false)]
  [ApiFunctionParam(Name = "sort", Type = typeof(WebSortField), Nullable = true)]
  [ApiFunctionParam(Name = "order", Type = typeof(WebSortOrder), Nullable = true)]
  internal class GetRadioChannelsDetailedByRange : BaseChannelDetailed
  {
    public IList<WebChannelDetailed> Process(int start, int end, int? groupId, WebSortField? sort, WebSortOrder? order)
    {
      List<WebChannelDetailed> output = new List<WebChannelDetailed>();

      if (!ServiceRegistration.IsRegistered<ITvProvider>())
        throw new BadRequestException("GetRadioChannelsDetailedByRange: ITvProvider not found");

      IChannelAndGroupInfo channelAndGroupInfo = ServiceRegistration.Get<ITvProvider>() as IChannelAndGroupInfo;
        

      IList<IChannelGroup> channelGroups = new List<IChannelGroup>();
      if (groupId == null)
        channelAndGroupInfo.GetChannelGroups(out channelGroups);
      else
      {
        channelGroups.Add(new ChannelGroup() { ChannelGroupId = groupId.Value });
      }

      foreach (var group in channelGroups)
      {
        // get channel for goup
        IList<IChannel> channels = new List<IChannel>();
        if (!channelAndGroupInfo.GetChannels(group, out channels))
          continue;

        output.AddRange(channels.Where(x => x.MediaType == MediaType.Radio).Select(channel => ChannelDetailed(channel)));
      }

      // sort
      if (sort != null && order != null)
      {
        output = output.SortChannelList(sort, order).ToList();
      }

      output = output.TakeRange(start, end).ToList();

      return output;
    }

    internal static ILogger Logger
    {
      get { return ServiceRegistration.Get<ILogger>(); }
    }
  }
}