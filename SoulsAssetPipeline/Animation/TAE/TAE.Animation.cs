﻿using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoulsAssetPipeline.Animation
{
    public partial class TAE : SoulsFile<TAE>
    {

        /// <summary>
        /// Controls an individual animation.
        /// </summary>
        public class Animation
        {
            public enum MiniHeaderType : uint
            {
                /// <summary>
                /// Standard AnimMiniHeader with three flags, 2 of which can reference specific parts of another animation.
                /// </summary>
                Standard = 0,

                /// <summary>
                /// AnimMiniHeader that signifies that the animation fully imports the motion data and all events from another animation.
                /// </summary>
                ImportOtherAnim = 1
            }

            public abstract class AnimMiniHeader
            {
                /// <summary>
                /// Type of AnimMiniHeader that this is.
                /// </summary>
                public abstract MiniHeaderType Type { get; }
                internal abstract void ReadInner(BinaryReaderEx br);
                internal abstract void WriteInner(BinaryWriterEx bw);

                /// <summary>
                /// Gets a clone of this not tied by reference.
                /// </summary>
                public abstract AnimMiniHeader GetClone();

                /// <summary>
                /// Standard MiniHeader with three flags, 2 of which can reference specific parts of another animation.
                /// </summary>
                public sealed class Standard : AnimMiniHeader
                {
                    /// <summary>
                    /// Type of AnimMiniHeader that this is.
                    /// </summary>
                    public override MiniHeaderType Type => MiniHeaderType.Standard;

                    /// <summary>
                    /// Gets a clone of this not tied by reference.
                    /// </summary>
                    public override AnimMiniHeader GetClone()
                    {
                        var newClone = new Standard();

                        newClone.IsLoopByDefault = IsLoopByDefault;
                        newClone.AllowDelayLoad = AllowDelayLoad;
                        newClone.ImportsHKX = ImportsHKX;

                        newClone.ImportHKXSourceAnimID = ImportHKXSourceAnimID;

                        return newClone;
                    }

                    /// <summary>
                    /// Makes the animation loop by default. Only relevant for animations not controlled by
                    /// ESD or HKS such as ObjAct animations.
                    /// </summary>
                    public bool IsLoopByDefault { get; set; } = false;

                    /// <summary>
                    /// Whether to import the HKX (actual motion data) of the animation with the ID of <see cref="ImportHKXSourceAnimID"/>.
                    /// </summary>
                    public bool ImportsHKX { get; set; } = false;

                    /// <summary>
                    /// Whether to allow this animation to be loaded from delayload anibnds such as the c0000_cXXXX.anibnd player throw anibnds.
                    /// </summary>
                    public bool AllowDelayLoad { get; set; } = false;

                    /// <summary>
                    /// Anim ID to import HKX from. Only functional if
                    /// <see cref="ImportsHKX"/> is enabled.
                    /// </summary>
                    public int ImportHKXSourceAnimID { get; set; } = 0;

                    internal override void ReadInner(BinaryReaderEx br)
                    {
                        IsLoopByDefault = br.ReadByte() != 0;
                        ImportsHKX = br.ReadByte() != 0;
                        AllowDelayLoad = br.ReadByte() != 0;
                        br.ReadByte();

                        ImportHKXSourceAnimID = br.ReadInt32();
                    }

                    internal override void WriteInner(BinaryWriterEx bw)
                    {
                        bw.WriteBoolean(IsLoopByDefault);
                        bw.WriteBoolean(ImportsHKX);
                        bw.WriteBoolean(AllowDelayLoad);
                        bw.WriteByte(0);

                        bw.WriteInt32(ImportHKXSourceAnimID);
                    }
                }

                /// <summary>
                /// AnimMiniHeader that signifies that the animation fully imports the motion data and all events from another animation.
                /// </summary>
                public sealed class ImportOtherAnim : AnimMiniHeader
                {
                    /// <summary>
                    /// Type of AnimMiniHeader that this is.
                    /// </summary>
                    public override MiniHeaderType Type => MiniHeaderType.ImportOtherAnim;

                    /// <summary>
                    /// Gets a clone of this not tied by reference.
                    /// </summary>
                    public override AnimMiniHeader GetClone()
                    {
                        var newClone = new ImportOtherAnim();

                        newClone.ImportFromAnimID = ImportFromAnimID;
                        newClone.Unknown = Unknown;

                        return newClone;
                    }

                    /// <summary>
                    /// ID of animation from which to import motion dat and all events.
                    /// </summary>
                    public int ImportFromAnimID { get; set; } = 0;

                    /// <summary>
                    /// Unknown usage.
                    /// </summary>
                    public int Unknown { get; set; } = -1;

                    internal override void ReadInner(BinaryReaderEx br)
                    {
                        ImportFromAnimID = br.ReadInt32();
                        Unknown = br.ReadInt32();
                    }

                    internal override void WriteInner(BinaryWriterEx bw)
                    {
                        bw.WriteInt32(ImportFromAnimID);
                        bw.WriteInt32(Unknown);
                    }
                }
            }

            /// <summary>
            /// ID number of this animation.
            /// </summary>
            public long ID { get; set; }

            /// <summary>
            /// Timed events in this animation.
            /// </summary>
            public List<Event> Events;

            /// <summary>
            /// Unknown groups of events.
            /// </summary>
            public List<EventGroup> EventGroups;

            /// <summary>
            /// The mini-header of this animation entry.
            /// </summary>
            public AnimMiniHeader MiniHeader { get; set; } = null;

            /// <summary>
            /// Unknown.
            /// </summary>
            public string AnimFileName { get; set; }

            /// <summary>
            /// Creates a new empty animation with the specified properties.
            /// </summary>
            public Animation(long id, AnimMiniHeader miniHeader, string animFileName)
            {
                ID = id;
                MiniHeader = miniHeader;
                AnimFileName = animFileName;
                Events = new List<Event>();
                EventGroups = new List<EventGroup>();
            }

            internal Animation(BinaryReaderEx br, TAEFormat format,
                out bool lastEventNeedsParamGen, out long animFileOffset,
                out long lastEventParamOffset)
            {
                lastEventNeedsParamGen = false;
                lastEventParamOffset = 0;
                ID = br.ReadVarint();
                long offset = br.ReadVarint();
                br.StepIn(offset);
                {
                    int eventCount;
                    long eventHeadersOffset;
                    int eventGroupCount;
                    long eventGroupsOffset;
                    long timesOffset;

                    if (format == TAEFormat.DS1)
                    {
                        eventCount = br.ReadInt32();
                        eventHeadersOffset = br.ReadVarint();
                        eventGroupCount = br.ReadInt32();
                        eventGroupsOffset = br.ReadVarint();
                        br.ReadInt32(); // Times count
                        timesOffset = br.ReadVarint(); // Times offset
                        animFileOffset = br.ReadVarint();

                        //For DeS assert 5 int32 == 0 here
                    }
                    else
                    {
                        eventHeadersOffset = br.ReadVarint();
                        eventGroupsOffset = br.ReadVarint();
                        timesOffset = br.ReadVarint(); // Times offset
                        animFileOffset = br.ReadVarint();
                        eventCount = br.ReadInt32();
                        eventGroupCount = br.ReadInt32();
                        br.ReadInt32(); // Times count
                        br.AssertInt32(0);
                    }

                    var eventHeaderOffsets = new List<long>(eventCount);
                    var eventParameterOffsets = new List<long>(eventCount);
                    Events = new List<Event>(eventCount);
                    br.StepIn(eventHeadersOffset);
                    {
                        for (int i = 0; i < eventCount; i++)
                        {
                            eventHeaderOffsets.Add(br.Position);
                            Events.Add(Event.Read(br, out long pOffset, format));
                            eventParameterOffsets.Add(pOffset);

                            if (i > 0)
                            {
                                //  Go to previous event's parameters
                                br.StepIn(eventParameterOffsets[i - 1]);
                                {
                                    // Read the space between the previous event's parameter start and the start of this event data.
                                    long gapBetweenEventParamOffsets = eventParameterOffsets[i] - eventParameterOffsets[i - 1];
                                    // Subtract to account for the current event's type and offset 
                                    Events[i - 1].ReadParameters(br, (int)(gapBetweenEventParamOffsets - (br.VarintLong ? 16 : 8)));
                                }
                                br.StepOut();
                            }
                        }
                    }
                    br.StepOut();

                    if (eventCount > 0)
                    {
                        if (eventGroupsOffset == 0)
                        {
                            lastEventNeedsParamGen = true;
                            lastEventParamOffset = eventParameterOffsets[eventCount - 1];
                        }
                        else
                        {
                            // Go to last event's parameters
                            br.StepIn(eventParameterOffsets[eventCount - 1]);
                            {
                                // Read the space between the last event's parameter start and the start of the event groups.
                                Events[eventCount - 1].ReadParameters(br, (int)(eventGroupsOffset - eventParameterOffsets[eventCount - 1]));
                            }
                            br.StepOut();
                        }
                    }

                    EventGroups = new List<EventGroup>(eventGroupCount);
                    br.StepIn(eventGroupsOffset);
                    {
                        for (int i = 0; i < eventGroupCount; i++)
                            EventGroups.Add(new EventGroup(br, eventHeaderOffsets, format));
                    }
                    br.StepOut();

                    br.StepIn(animFileOffset);
                    {
                        var miniHeaderType = br.ReadEnum32<MiniHeaderType>();

                        if (br.VarintLong)
                            br.AssertInt32(0);

                        br.AssertVarint(br.Position + (br.VarintLong ? 8 : 4));
                        long animFileNameOffset = br.ReadVarint();

                        //if (AnimFileReference)
                        //{
                        //    ReferenceID = br.ReadInt32();

                        //    UnkReferenceFlag1 = br.ReadBoolean();
                        //    ReferenceIsTAEOnly = br.ReadBoolean();
                        //    ReferenceIsHKXOnly = br.ReadBoolean();
                        //    LoopByDefault = br.ReadBoolean();
                        //}
                        //else
                        //{
                        //    UnkReferenceFlag1 = br.ReadBoolean();
                        //    ReferenceIsTAEOnly = br.ReadBoolean();
                        //    ReferenceIsHKXOnly = br.ReadBoolean();
                        //    LoopByDefault = br.ReadBoolean();

                        //    ReferenceID = br.ReadInt32();
                        //}

                        if (miniHeaderType == MiniHeaderType.Standard)
                        {
                            MiniHeader = new AnimMiniHeader.Standard();
                        }
                        else if (miniHeaderType == MiniHeaderType.ImportOtherAnim)
                        {
                            MiniHeader = new AnimMiniHeader.ImportOtherAnim();
                        }
                        else
                        {
                            throw new NotImplementedException($"{nameof(AnimMiniHeader)} type not implemented yet.");
                        }

                        MiniHeader.ReadInner(br);

                        if (format != TAEFormat.DS1)
                        {
                            br.AssertVarint(0);
                            br.AssertVarint(0);
                        }
                        else
                        {
                            br.AssertVarint(0);

                            if (MiniHeader.Type == MiniHeaderType.ImportOtherAnim)
                                br.AssertVarint(0);
                        }

                        if (animFileNameOffset < br.Length && animFileNameOffset != timesOffset)
                        {
                            if (br.GetInt64(animFileNameOffset) != 1)
                            {
                                var floatCheck = br.GetSingle(animFileNameOffset);
                                if (!(floatCheck >= 0.016667f && floatCheck <= 100))
                                {
                                    AnimFileName = br.GetUTF16(animFileNameOffset);
                                }
                            }
                        }

                        AnimFileName = AnimFileName ?? "";

                        // When Reference is false, there's always a filename.
                        // When true, there's usually not, but sometimes there is, and I cannot figure out why.
                        // Thus, this stupid hack to achieve byte-perfection.
                        //var animNameCheck = AnimFileName.ToLower();
                        //if (!(animNameCheck.EndsWith(".hkt") 
                        //    || (format == TAEFormat.SDT && animNameCheck.EndsWith("hkt")) 
                        //    || animNameCheck.EndsWith(".hkx") 
                        //    || animNameCheck.EndsWith(".sib") 
                        //    || animNameCheck.EndsWith(".hkxwin")))
                        //    AnimFileName = "";

                    }
                    br.StepOut();
                }
                br.StepOut();
            }

            internal void WriteHeader(BinaryWriterEx bw, int i)
            {
                bw.WriteVarint(ID);
                bw.ReserveVarint($"AnimationOffset{i}");
            }

            internal void WriteBody(BinaryWriterEx bw, int i, TAEFormat format)
            {
                bw.FillVarint($"AnimationOffset{i}", bw.Position);

                if (format == TAEFormat.DS1)
                {
                    bw.WriteInt32(Events.Count);
                    bw.ReserveVarint($"EventHeadersOffset{i}");
                    bw.WriteInt32(EventGroups.Count);
                    bw.ReserveVarint($"EventGroupHeadersOffset{i}");
                    bw.ReserveInt32($"TimesCount{i}");
                    bw.ReserveVarint($"TimesOffset{i}");
                    bw.ReserveVarint($"AnimFileOffset{i}");
                    //For DeS write 5 int32 == 0
                }
                else
                {
                    bw.ReserveVarint($"EventHeadersOffset{i}");
                    bw.ReserveVarint($"EventGroupHeadersOffset{i}");
                    bw.ReserveVarint($"TimesOffset{i}");
                    bw.ReserveVarint($"AnimFileOffset{i}");
                    bw.WriteInt32(Events.Count);
                    bw.WriteInt32(EventGroups.Count);
                    bw.ReserveInt32($"TimesCount{i}");
                    bw.WriteInt32(0);
                }
            }

            internal void WriteAnimFile(BinaryWriterEx bw, int i, TAEFormat format)
            {
                bw.FillVarint($"AnimFileOffset{i}", bw.Position);
                bw.WriteVarint((int)MiniHeader.Type);
                bw.WriteVarint(bw.Position + (bw.VarintLong ? 8 : 4));
                bw.ReserveVarint("AnimFileNameOffset");

                //if (AnimFileReference)
                //{
                //    bw.WriteInt32(ReferenceID);

                //    bw.WriteBoolean(UnkReferenceFlag1);
                //    bw.WriteBoolean(ReferenceIsTAEOnly);
                //    bw.WriteBoolean(ReferenceIsHKXOnly);
                //    bw.WriteBoolean(LoopByDefault);
                //}
                //else
                //{
                //    bw.WriteBoolean(UnkReferenceFlag1);
                //    bw.WriteBoolean(ReferenceIsTAEOnly);
                //    bw.WriteBoolean(ReferenceIsHKXOnly);
                //    bw.WriteBoolean(LoopByDefault);

                //    bw.WriteInt32(ReferenceID);
                //}

                MiniHeader.WriteInner(bw);

                if (format != TAEFormat.DS1)
                {
                    bw.WriteVarint(0);
                    bw.WriteVarint(0);
                }
                else
                {
                    bw.WriteVarint(0);

                    if (MiniHeader.Type == MiniHeaderType.ImportOtherAnim)
                        bw.WriteVarint(0);
                }

                bw.FillVarint("AnimFileNameOffset", bw.Position);
                if (AnimFileName != "")
                {
                    bw.WriteUTF16(AnimFileName, true);

                    if (format != TAEFormat.DS1)
                        bw.Pad(0x10);
                }
            }

            internal Dictionary<float, long> WriteTimes(BinaryWriterEx bw, int animIndex, TAEFormat format)
            {
                var times = new SortedSet<float>();

                foreach (Event evt in Events)
                {
                    times.Add(evt.StartTime);
                    times.Add(evt.EndTime);
                }

                bw.FillInt32($"TimesCount{animIndex}", times.Count);

                if (times.Count == 0)
                    bw.FillVarint($"TimesOffset{animIndex}", 0);
                else
                    bw.FillVarint($"TimesOffset{animIndex}", bw.Position);

                var timeOffsets = new Dictionary<float, long>();
                foreach (float time in times)
                {
                    timeOffsets[time] = bw.Position;
                    bw.WriteSingle(time);
                }

                if (format != TAEFormat.DS1)
                    bw.Pad(0x10);

                return timeOffsets;
            }

            internal List<long> WriteEventHeaders(BinaryWriterEx bw, int animIndex, Dictionary<float, long> timeOffsets)
            {
                var eventHeaderOffsets = new List<long>(Events.Count);
                if (Events.Count > 0)
                {
                    bw.FillVarint($"EventHeadersOffset{animIndex}", bw.Position);
                    for (int i = 0; i < Events.Count; i++)
                    {
                        eventHeaderOffsets.Add(bw.Position);
                        Events[i].WriteHeader(bw, animIndex, i, timeOffsets);
                    }
                }
                else
                {
                    bw.FillVarint($"EventHeadersOffset{animIndex}", 0);
                }
                return eventHeaderOffsets;
            }

            internal void WriteEventData(BinaryWriterEx bw, int i, TAEFormat format)
            {
                for (int j = 0; j < Events.Count; j++)
                    Events[j].WriteData(bw, i, j, format);
            }

            internal void WriteEventGroupHeaders(BinaryWriterEx bw, int i, TAEFormat format)
            {
                if (EventGroups.Count > 0)
                {
                    bw.FillVarint($"EventGroupHeadersOffset{i}", bw.Position);
                    for (int j = 0; j < EventGroups.Count; j++)
                        EventGroups[j].WriteHeader(bw, i, j, format);
                }
                else
                {
                    bw.FillVarint($"EventGroupHeadersOffset{i}", 0);
                }
            }

            internal void WriteEventGroupData(BinaryWriterEx bw, int i, List<long> eventHeaderOffsets, TAEFormat format)
            {
                for (int j = 0; j < EventGroups.Count; j++)
                    EventGroups[j].WriteData(bw, i, j, eventHeaderOffsets, format);
            }
        }

    }
}
