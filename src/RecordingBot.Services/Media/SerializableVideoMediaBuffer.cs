/*public SerializableVideoMediaBuffer(VideoMediaBuffer buffer, List<IParticipant> participants)
{
    this.participants = participants;

    Length = buffer.Length;

    Timestamp = buffer.Timestamp;

    if (Length > 0)
    {
        Buffer = new byte[Length];
        Marshal.Copy(buffer.Data, Buffer, 0, (int)Length);
    }

}*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Graph.Communications.Calls;
using Microsoft.Skype.Bots.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using RecordingBot.Services.Bot;

namespace RecordingBot.Services.Media
{
    class SerializableVideoMediaBuffer:IDisposable
    {

        /// <summary>
        /// Gets or sets the active speakers.
        /// </summary>
        /// <value>The active speakers.</value>
        public uint[] ActiveSpeakers { get; set; }
        private Dictionary<string, List<MediaPayload>> userVideoData;
        /// <summary>
        /// Gets or sets the length.
        /// </summary>
        /// <value>The length.</value>
        public long Length { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this instance is silence.
        /// </summary>
        /// <value><c>true</c> if this instance is silence; otherwise, <c>false</c>.</value>
        public bool IsSilence { get; set; }
        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        /// <value>The timestamp.</value>
        public long Timestamp { get; set; }
        /// <summary>
        /// Gets or sets the buffer.
        /// </summary>
        /// <value>The buffer.</value>
        public byte[] Buffer { get; set; }

        /// <summary>
        /// Gets or sets the serializable unmixed audio buffers.
        /// </summary>
        /// <value>The serializable unmixed audio buffers.</value>
        public SerializableUnmixedAudioBuffer[] SerializableUnmixedAudioBuffers { get; set; }

        /// <summary>
        /// The participants
        /// </summary>
        private List<IParticipant> participants;

        /// <summary>
        /// Initializes a new instance of the <see cref="SerializableAudioMediaBuffer" /> class.

        /// </summary>
        public SerializableVideoMediaBuffer()
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SerializableAudioMediaBuffer" /> class.

        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="participants">The participants.</param>
        public SerializableVideoMediaBuffer(VideoMediaBuffer Buffer, List<IParticipant> participants)
        {
            System.Console.WriteLine("WRITING THE VIDEO SECOND");
            System.Console.WriteLine(Buffer.Data);
            /*this.participants = participants;

            Length = Buffer.Length;

            Timestamp = Buffer.Timestamp;

           
            var buffer = new byte[Length];
            Marshal.Copy(Buffer.Data, buffer, 0, (int)Length);
            return new MediaPayload
            {
                Data = buffer,
                Timestamp = Buffer.Timestamp,
                Width = Buffer.VideoFormat.Width,
                Height = Buffer.VideoFormat.Height,
                ColorFormat = Buffer.VideoFormat.VideoColorFormat,
                FrameRate = Buffer.VideoFormat.FrameRate,
            };*/


        }

        /// <summary>
        /// Gets the participant from msi.
        /// </summary>
        /// <param name="msi">The msi.</param>
        /// <returns>IParticipant.</returns>
        private IParticipant _getParticipantFromMSI(uint msi)
        {
            return this.participants.SingleOrDefault(x => x.Resource.IsInLobby == false && x.Resource.MediaStreams.Any(y => y.SourceId == msi.ToString()));
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            SerializableUnmixedAudioBuffers = null;
            Buffer = null;
        }

        /// <summary>
        /// Class SerializableUnmixedAudioBuffer.
        /// </summary>
        public class SerializableUnmixedAudioBuffer
        {
            /// <summary>
            /// Gets or sets the active speaker identifier.
            /// </summary>
            /// <value>The active speaker identifier.</value>
            public uint ActiveSpeakerId { get; set; }
            /// <summary>
            /// Gets or sets the length.
            /// </summary>
            /// <value>The length.</value>
            public long Length { get; set; }
            /// <summary>
            /// Gets or sets the original sender timestamp.
            /// </summary>
            /// <value>The original sender timestamp.</value>
            public long OriginalSenderTimestamp { get; set; }
            /// <summary>
            /// Gets or sets the display name.
            /// </summary>
            /// <value>The display name.</value>
            public string DisplayName { get; set; }
            /// <summary>
            /// Gets or sets the ad identifier.
            /// </summary>
            /// <value>The ad identifier.</value>
            public string AdId { get; set; }
            /// <summary>
            /// Gets or sets the additional data.
            /// </summary>
            /// <value>The additional data.</value>
            public IDictionary<string, object> AdditionalData { get; set; }

            /// <summary>
            /// Gets or sets the buffer.
            /// </summary>
            /// <value>The buffer.</value>
            public byte[] Buffer { get; set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="SerializableUnmixedAudioBuffer" /> class.

            /// </summary>
            public SerializableUnmixedAudioBuffer()
            {

            }

            /// <summary>
            /// Initializes a new instance of the <see cref="SerializableUnmixedAudioBuffer" /> class.

            /// </summary>
            /// <param name="buffer">The buffer.</param>
            /// <param name="participant">The participant.</param>
            public SerializableUnmixedAudioBuffer(UnmixedAudioBuffer buffer, IParticipant participant)
            {
                ActiveSpeakerId = buffer.ActiveSpeakerId;
                Length = buffer.Length;
                OriginalSenderTimestamp = buffer.OriginalSenderTimestamp;

                var i = AddParticipant(participant);

                if (i != null)
                {
                    DisplayName = i.DisplayName;
                    AdId = i.Id;
                }
                else
                {
                    DisplayName = participant?.Resource?.Info?.Identity?.User?.DisplayName;
                    AdId = participant?.Resource?.Info?.Identity?.User?.Id;
                    AdditionalData = participant?.Resource?.Info?.Identity?.User?.AdditionalData;
                }

                Buffer = new byte[Length];
                Marshal.Copy(buffer.Data, Buffer, 0, (int)Length);
            }

            /// <summary>
            /// Adds the participant.
            /// </summary>
            /// <param name="p">The p.</param>
            /// <returns>Identity.</returns>
            private Identity AddParticipant(IParticipant p)
            {
                if (p?.Resource?.Info?.Identity?.AdditionalData != null)
                {
                    foreach (var i in p.Resource.Info.Identity.AdditionalData)
                    {
                        if (i.Key != "applicationInstance" && i.Value is Identity)
                        {
                            return i.Value as Identity;
                        }
                    }
                }
                return null;
            }
        }
    }
}

