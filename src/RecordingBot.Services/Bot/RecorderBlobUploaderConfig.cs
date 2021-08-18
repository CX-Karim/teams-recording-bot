// ***********************************************************************
// Assembly         : RecordingBot.Services
// Author           : JasonTheDeveloper
// Created          : 09-07-2020
//
// Last Modified By : dannygar
// Last Modified On : 09-07-2020
// ***********************************************************************
// <copyright file="BotMediaStream.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>
// <summary>The bot media stream.</summary>
// ***********************************************************************-

namespace RecordingBot.Services.Bot
{
    public class RecorderBlobUploaderConfig
    {
        public string ConnectionString { get; set; }
        public string ContainerName { get; set; }
        public string TableStorageConnectionString { get; set; }

    }
}