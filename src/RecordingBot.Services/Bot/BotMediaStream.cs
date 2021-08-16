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

using Microsoft.Graph.Communications.Calls;
using Microsoft.Graph.Communications.Calls.Media;
using Microsoft.Graph.Communications.Common;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Skype.Bots.Media;
using Microsoft.Skype.Internal.Media.Services.Common;
using Newtonsoft.Json;
using RecordingBot.Services.Contract;
using RecordingBot.Services.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace RecordingBot.Services.Bot
{
    /// <summary>
    /// Class responsible for streaming audio and video.
    /// </summary>
    public class BotMediaStream : ObjectRootDisposable
    {
        /// <summary>
        /// The participants
        /// </summary>
        internal List<IParticipant> participants;

        /// <summary>
        /// The audio socket
        /// </summary>
        private readonly IAudioSocket _audioSocket;
        /// <summary>
        /// The media stream
        /// </summary>
        private readonly IMediaStream _mediaStream;
        /// <summary>
        /// The event publisher
        /// </summary>
        private readonly IEventPublisher _eventPublisher;

        /// <summary>
        /// The call identifier
        /// </summary>
        private readonly string _callId;
        private readonly List<IVideoSocket> videoSockets;
        private IVideoSocket vbssSocket;
        private IVideoSocket oneVideo;
        private List<MediaPayload> vbssData;
        private Dictionary<int, string> socketUserMapping;
        private Dictionary<string, List<MediaPayload>> userVideoData;
        private readonly ILocalMediaSession mediaSession;

        /// <summary>
        /// Return the last read 'audio quality of experience data' in a serializable structure
        /// </summary>
        /// <value>The audio quality of experience data.</value>
        public SerializableAudioQualityOfExperienceData AudioQualityOfExperienceData { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BotMediaStream" /> class.
        /// </summary>
        /// <param name="mediaSession">he media session.</param>
        /// <param name="callId">The call identity</param>
        /// <param name="logger">The logger.</param>
        /// <param name="eventPublisher">Event Publisher</param>
        /// <param name="settings">Azure settings</param>
        /// <exception cref="InvalidOperationException">A mediaSession needs to have at least an audioSocket</exception>
        public BotMediaStream(
            ILocalMediaSession mediaSession,
            string callId,
            IGraphLogger logger,
            IEventPublisher eventPublisher,
            IAzureSettings settings
        )
            : base(logger)
        {
            ArgumentVerifier.ThrowOnNullArgument(mediaSession, nameof(mediaSession));
            ArgumentVerifier.ThrowOnNullArgument(logger, nameof(logger));
            ArgumentVerifier.ThrowOnNullArgument(settings, nameof(settings));

            this.participants = new List<IParticipant>();

            _eventPublisher = eventPublisher;
            _callId = callId;
            _mediaStream = new MediaStream(
                settings,
                logger,
                mediaSession.MediaSessionId.ToString()
            );

            // Subscribe to the audio media.
            this._audioSocket = mediaSession.AudioSocket;
            this.mediaSession = mediaSession;
            if (this._audioSocket == null)
            {
                throw new InvalidOperationException("A mediaSession needs to have at least an audioSocket");
            }


            this._audioSocket.AudioMediaReceived += this.OnAudioMediaReceived;
            oneVideo = this.mediaSession.VideoSocket;
            if (this.oneVideo != null) {
                mediaSession.VideoSocket.VideoMediaReceived += this.OnVideoMediaReceived;
                oneVideo.VideoMediaReceived += this.OnVideoMediaReceived;
            }

            this.videoSockets = this.mediaSession.VideoSockets?.ToList();
            // videoParticipants.AddRange(new uint[this.videoSockets.Count()]);
            if (this.videoSockets?.Any() == true)
            {
                this.videoSockets.ForEach(videoSocket => {
                    try
                    {
                        videoSocket.VideoMediaReceived += this.OnVideoMediaReceived;
                    }
                    catch (Exception e)
                    {

                        System.Console.WriteLine("ERROR" + e.Message);
                    }
                });
            }

            // Subscribe to the VBSS media.
            this.vbssSocket = this.mediaSession.VbssSocket;
            if (this.vbssSocket != null)
            {
                mediaSession.VbssSocket.VideoMediaReceived += this.OnVbssMediaReceived;
            }
            this.vbssData = new List<MediaPayload>();
            this.socketUserMapping = new Dictionary<int, string>();
            this.userVideoData = new Dictionary<string, List<MediaPayload>>();
        }

        /// <summary>
        /// Gets the participants.
        /// </summary>
        /// <returns>List&lt;IParticipant&gt;.</returns>
        public List<IParticipant> GetParticipants()
        {
            return participants;
        }

        /// <summary>
        /// Gets the audio quality of experience data.
        /// </summary>
        /// <returns>SerializableAudioQualityOfExperienceData.</returns>
        public SerializableAudioQualityOfExperienceData GetAudioQualityOfExperienceData()
        {
            AudioQualityOfExperienceData = new SerializableAudioQualityOfExperienceData(this._callId, this._audioSocket.GetQualityOfExperienceData());
            return AudioQualityOfExperienceData;
        }

        /// <summary>
        /// Stops the media.
        /// </summary>
        public async Task StopMedia()
        {
            await _mediaStream.End();
            // Event - Stop media occurs when the call stops recording
            _eventPublisher.Publish("StopMediaStream", "Call stopped recording");
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            try
            {
                base.Dispose(disposing);
                this._audioSocket.AudioMediaReceived -= this.OnAudioMediaReceived;
                if (this.videoSockets?.Any() == true)
                {
                    this.videoSockets.ForEach(videoSocket => videoSocket.VideoMediaReceived -= this.OnVideoMediaReceived);
                }
                // Subscribe to the VBSS media.
                if (this.vbssSocket != null)
                {
                    this.mediaSession.VbssSocket.VideoMediaReceived -= this.OnVbssMediaReceived;
                }
            }
            catch (Exception e)
            {
                var innerMessage = e.InnerException == null ? string.Empty : e.InnerException.Message;
                var innerStack = e.InnerException == null ? string.Empty : e.InnerException.StackTrace;
            }
            try
            {
                // Saving raw vbss
                string vbssJson = JsonConvert.SerializeObject(this.vbssData);
                System.IO.File.WriteAllText($"C:\\vbssData.json", vbssJson);
                foreach (var key in this.userVideoData.Keys)
                {
                    // Saving raw video
                    string videoJson = JsonConvert.SerializeObject(this.userVideoData[key]);
                    byte[] videoBytes = Encoding.UTF8.GetBytes(videoJson);
                    System.IO.File.WriteAllText($"C:\\{key}VideoData.json", videoJson);
                }
            }
            catch (Exception e)
            {
                var innerMessage = e.InnerException == null ? string.Empty : e.InnerException.Message;
                var innerStack = e.InnerException == null ? string.Empty : e.InnerException.StackTrace;
            }
        }

        // Event Dispose of the bot media stream object
        /* _eventPublisher.Publish("MediaStreamDispose", disposing.ToString());

         base.Dispose(disposing);

         this._audioSocket.AudioMediaReceived -= this.OnAudioMediaReceived;

         if (this.videoSockets?.Any() == true)
         {
             this.videoSockets.ForEach(videoSocket => videoSocket.VideoMediaReceived -= this.OnVideoMediaReceived);
         }

         // Subscribe to the VBSS media.
         if (this.vbssSocket != null)
         {
             this.mediaSession.VbssSocket.VideoMediaReceived -= this.OnVbssMediaReceived;
         }
         try
         {
             // Saving raw vbss
             string vbssJson = JsonConvert.SerializeObject(this.vbssData);
             System.IO.File.WriteAllText($"vbssData.json", vbssJson);
             foreach (var key in this.userVideoData.Keys)
             {
                 var filename = "test";
                 // Saving raw video
                 string videoJson = JsonConvert.SerializeObject(this.userVideoData[key]);
                 byte[] videoBytes = Encoding.UTF8.GetBytes(videoJson);

                 System.IO.File.WriteAllText($"C:\\{filename}.json", videoJson);
             }

         }
         catch
         {
         }*/
    //}

        /// <summary>
        /// Receive audio from subscribed participant.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The audio media received arguments.</param>
            private async void OnAudioMediaReceived(object sender, AudioMediaReceivedEventArgs e)
        {
            
            this.GraphLogger.Info($"Received Audio: [AudioMediaReceivedEventArgs(Data=<{e.Buffer.Data.ToString()}>, Length={e.Buffer.Length}, Timestamp={e.Buffer.Timestamp})]");

            try
            {
                await _mediaStream.AppendAudioBuffer(e.Buffer, this.participants);
                e.Buffer.Dispose();
            }
            catch (Exception ex)
            {
                this.GraphLogger.Error(ex);
            }
            finally
            {
                e.Buffer.Dispose();
            }

        }
        public void Subscribe(MediaType mediaType, uint mediaSourceId, VideoResolution videoResolution, Microsoft.Graph.Identity participant, uint socketId = 0)
        {
            try
            {
                this.ValidateSubscriptionMediaType(mediaType);

                this.GraphLogger.Info($"Subscribing to the video source: {mediaSourceId} on socket: {socketId} with the preferred resolution: {videoResolution} and mediaType: {mediaType}");
                if (mediaType == MediaType.Vbss)
                {
                    if (this.vbssSocket == null)
                    {
                        this.GraphLogger.Warn($"vbss socket not initialized");
                    }
                    else
                    {
                        this.vbssSocket.Subscribe(videoResolution, mediaSourceId);
                        this.vbssData.Add(new MediaPayload
                        {
                            Data = null,
                            Timestamp = DateTime.UtcNow.Ticks,
                            Width = 0,
                            Height = 0,
                            ColorFormat = VideoColorFormat.H264,
                            FrameRate = 0,
                            Event = "Subscribed",
                            UserId = participant.Id,
                            DisplayName = participant.DisplayName,
                        });
                    }
                }
                else if (mediaType == MediaType.Video)
                {
                    if (this.videoSockets == null)
                    {
                        this.GraphLogger.Warn($"video sockets were not created");
                    }
                    else
                    {
                        if (!this.socketUserMapping.ContainsKey((int)socketId))
                        {
                            this.socketUserMapping.Add((int)socketId, participant.Id);
                        }
                        else
                        {
                            this.socketUserMapping[(int)socketId] = participant.Id;
                        }

                        if (!this.userVideoData.ContainsKey(participant.Id))
                        {
                            this.userVideoData.Add(participant.Id, new List<MediaPayload>());
                        }

                        /*
                        if (!socketVideoData.ContainsKey((int)socketId))
                        {
                            this.socketVideoData.Add((int)socketId, new List<MediaPayload>());
                        }
                        */
                        this.userVideoData[participant.Id].Add(new MediaPayload
                        {
                            Data = null,
                            Timestamp = DateTime.UtcNow.Ticks,
                            Width = 0,
                            Height = 0,
                            ColorFormat = VideoColorFormat.H264,
                            FrameRate = 0,
                            Event = "Subscribed",
                            UserId = participant.Id,
                            DisplayName = participant.DisplayName,
                        });
                        this.videoSockets[(int)socketId].Subscribe(videoResolution, mediaSourceId);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex.ToString());
                this.GraphLogger.Error(ex, $"Video Subscription failed for the socket: {socketId} and MediaSourceId: {mediaSourceId} with exception");
                System.Console.WriteLine($"Video Subscription failed for the socket: {socketId} and MediaSourceId: {mediaSourceId} with exception");
            }
        }
        public void Unsubscribe(MediaType mediaType, uint socketId = 0)
        {
            try
            {
                this.ValidateSubscriptionMediaType(mediaType);

                this.GraphLogger.Info($"Unsubscribing to video for the socket: {socketId} and mediaType: {mediaType}");

                if (mediaType == MediaType.Vbss)
                {
                    this.vbssSocket?.Unsubscribe();
                    this.vbssData.Add(new MediaPayload
                    {
                        Data = null,
                        Timestamp = DateTime.UtcNow.Ticks,
                        Width = 0,
                        Height = 0,
                        ColorFormat = VideoColorFormat.H264,
                        FrameRate = 0,
                        Event = "Unsubscribe"
                    });
                }
                else if (mediaType == MediaType.Video)
                {
                    this.videoSockets[(int)socketId]?.Unsubscribe();

                    this.userVideoData[this.socketUserMapping[(int)socketId]].Add(new MediaPayload
                    {
                        Data = null,
                        Timestamp = DateTime.UtcNow.Ticks,
                        Width = 0,
                        Height = 0,
                        ColorFormat = VideoColorFormat.H264,
                        FrameRate = 0,
                        Event = "Unsubscribe",
                    });
                }
            }
            catch (Exception ex)
            {
                this.GraphLogger.Error(ex, $"Unsubscribing to video failed for the socket: {socketId} with exception");
            }
        }
        private void ValidateSubscriptionMediaType(MediaType mediaType)
        {
            if (mediaType != MediaType.Vbss && mediaType != MediaType.Video)
            {
                throw new ArgumentOutOfRangeException($"Invalid mediaType: {mediaType}");
            }
        }
        private async void OnVideoMediaReceived(object sender, VideoMediaReceivedEventArgs e)
        {
            
            this.GraphLogger.Info($"[{e.SocketId}]: Received Video: [VideoMediaReceivedEventArgs(Data=<{e.Buffer.Data.ToString()}>, Length={e.Buffer.Length}, Timestamp={e.Buffer.Timestamp}, Width={e.Buffer.VideoFormat.Width}, Height={e.Buffer.VideoFormat.Height}, ColorFormat={e.Buffer.VideoFormat.VideoColorFormat}, FrameRate={e.Buffer.VideoFormat.FrameRate})]");
            System.Console.WriteLine($"[{e.SocketId}]: Received Video: [VideoMediaReceivedEventArgs(Data=<{e.Buffer.Data.ToString()}>, Length={e.Buffer.Length}, Timestamp={e.Buffer.Timestamp}, Width={e.Buffer.VideoFormat.Width}, Height={e.Buffer.VideoFormat.Height}, ColorFormat={e.Buffer.VideoFormat.VideoColorFormat}, FrameRate={e.Buffer.VideoFormat.FrameRate})]");
            // this.log += DateTimeOffset.UtcNow.ToString() + $"[{e.SocketId}]: Received Video: [VideoMediaReceivedEventArgs(Data=<{e.Buffer.Data.ToString()}>, Length={e.Buffer.Length}, Timestamp={e.Buffer.Timestamp}, Width={e.Buffer.VideoFormat.Width}, Height={e.Buffer.VideoFormat.Height}, ColorFormat={e.Buffer.VideoFormat.VideoColorFormat}, FrameRate={e.Buffer.VideoFormat.FrameRate})]\n";
            try
            {
                int length = (int)e.Buffer.Length;
                {
                    // this.log += DateTimeOffset.UtcNow.ToString() + "Creating new byte array\n";
                    byte[] second = new byte[length];
                    // this.log += DateTimeOffset.UtcNow.ToString() + "Copy from pointer to byte array\n";
                    Marshal.Copy(e.Buffer.Data, second, 0, length);
                    this.userVideoData[this.socketUserMapping[e.SocketId]].Add(new MediaPayload
                    {
                        Data = second,
                        Timestamp = e.Buffer.Timestamp,
                        Width = e.Buffer.VideoFormat.Width,
                        Height = e.Buffer.VideoFormat.Height,
                        ColorFormat = e.Buffer.VideoFormat.VideoColorFormat,
                        FrameRate = e.Buffer.VideoFormat.FrameRate,
                    });
                }
            }
            catch (Exception ex)
            {
                this.GraphLogger.Warn("Exception");
            }

            e.Buffer.Dispose();
            /*            try
                        {
                            await _mediaStream.AppendVideoBuffer(e.Buffer, this.participants);
                            e.Buffer.Dispose();
                        }
                        catch (Exception ex)
                        {
                            this.GraphLogger.Error(ex);
                        }
                        finally
                        {
                            e.Buffer.Dispose();
                        }*/
        }
        private async void OnVbssMediaReceived(object sender, VideoMediaReceivedEventArgs e)
        {
            this.GraphLogger.Info($"[{e.SocketId}]: Received VBSS: [VideoMediaReceivedEventArgs(Data=<{e.Buffer.Data.ToString()}>, Length={e.Buffer.Length}, Timestamp={e.Buffer.Timestamp}, Width={e.Buffer.VideoFormat.Width}, Height={e.Buffer.VideoFormat.Height}, ColorFormat={e.Buffer.VideoFormat.VideoColorFormat}, FrameRate={e.Buffer.VideoFormat.FrameRate})]");
            // this.log += DateTimeOffset.UtcNow.ToString() + $"[{e.SocketId}]: Received VBSS: [VideoMediaReceivedEventArgs(Data=<{e.Buffer.Data.ToString()}>, Length={e.Buffer.Length}, Timestamp={e.Buffer.Timestamp}, Width={e.Buffer.VideoFormat.Width}, Height={e.Buffer.VideoFormat.Height}, ColorFormat={e.Buffer.VideoFormat.VideoColorFormat}, FrameRate={e.Buffer.VideoFormat.FrameRate})]\n";
            try
            {
                int length = (int)e.Buffer.Length;

                // this.log += DateTimeOffset.UtcNow.ToString() + "Creating new byte array\n";
                byte[] second = new byte[length];
                // this.log += DateTimeOffset.UtcNow.ToString() + "Copy from pointer to byte array\n";
                Marshal.Copy(e.Buffer.Data, second, 0, length);

                this.vbssData.Add(new MediaPayload
                {
                    Data = second,
                    Timestamp = e.Buffer.Timestamp,
                    Width = e.Buffer.VideoFormat.Width,
                    Height = e.Buffer.VideoFormat.Height,
                    ColorFormat = e.Buffer.VideoFormat.VideoColorFormat,
                    FrameRate = e.Buffer.VideoFormat.FrameRate,
                });

            }
            catch (Exception ex)
            {
                this.GraphLogger.Warn("Exception");
            }

            e.Buffer.Dispose();
        }
    }
}
