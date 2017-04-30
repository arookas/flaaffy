
using arookas.IO.Binary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace arookas {

	abstract class MidiReader {

		int mFormat, mTrackCount, mDivision;
		List<TrackChunkInfo> mTracks;
		TrackChunkInfo mCurrentTrack;
		aBinaryReader mReader;
		int mRunningStatus;
		bool mEndOfTrack;

		protected int Format { get { return mFormat; } }
		protected int TrackCount { get { return mTrackCount; } }
		protected int Division { get { return mDivision; } }

		protected void LoadMidi(Stream stream) {
			mReader = new aBinaryReader(stream, Endianness.Big, Encoding.ASCII);
			mReader.PushAnchor();
			mTracks = new List<TrackChunkInfo>();

			var mthd = false;
			var tracks = 0;

			while (!mReader.IsAtEndOfStream) {
				if (mReader.BytesRemaining < 8) {
					mareep.WriteWarning("MIDI: incomplete chunk at file end.\n");
					break;
				}

				var id = mReader.ReadString(4);
				var size = mReader.ReadS32();
				var start = mReader.Position;

				switch (id) {
					case "MThd": mthd = true; LoadMThd(size); break;
					case "MTrk": ++tracks; LoadMTrk(size); break;
				}

				mReader.Goto(start + size);
			}

			if (!mthd) {
				mareep.WriteError("MIDI: missing header chunk.");
			}

			if (tracks != mTrackCount) {
				mareep.WriteWarning("MIDI: track count mismatch (header says {0}, found {1}).\n", mTrackCount, tracks);
				mTrackCount = tracks;
			}

			if (mFormat == 0 && mTrackCount != 1) {
				mareep.WriteWarning("MIDI: format-0 requires a single track.");
			}
		}
		void LoadMThd(int size) {
			if (size < 6) {
				mareep.WriteError("MIDI: bad header size '{0}'.", size);
			}

			mFormat = mReader.Read16();

			if (mFormat > 2) {
				mareep.WriteError("MIDI: unknown format '{0}'.", mFormat);
			}

			mTrackCount = mReader.Read16();
			mDivision = mReader.Read16();

			if ((mDivision & 0x8000) != 0) {
				mareep.WriteError("MIDI: SMTPE divisions are not supported.");
			}
		}
		void LoadMTrk(int size) {
			var info = new TrackChunkInfo();
			info.start = mReader.Position;
			info.size = size;
			mTracks.Add(info);
		}

		protected void GotoTrack(int index) {
			if (index < 0 || index > mTrackCount) {
				throw new ArgumentOutOfRangeException("index");
			}

			mEndOfTrack = false;
			mRunningStatus = -1;
			mCurrentTrack = mTracks[index];
			mReader.Goto(mCurrentTrack.start);
		}
		protected bool ReadEvent(out EventInfo info) {
			info = new EventInfo();

			if (mReader.Position >= (mCurrentTrack.start + mCurrentTrack.size)) {
				if (!mEndOfTrack) {
					mareep.WriteWarning("MIDI: non-terminated track.\n");
				}
				return false;
			} else if (mEndOfTrack) {
				mareep.WriteError("MIDI: last event in track is not EOT marker.");
			}

			info.delta = mReader.ReadVLQ();
			var status = mReader.Read8();

			if ((status & 0x80) == 0) {
				if (mRunningStatus < 0) {
					mareep.WriteError("MIDI: invalid running status.");
				}

				mReader.Step(-1);
				status = (byte)mRunningStatus;
			}

			if (status < 0xF0) {
				info.type = (EventType)(status & 0xF0);
				info.channel = (status & 0x0F);

				switch (info.type) {
					case EventType.NoteOff: {
						info.key = mReader.Read8();
						info.velocity = mReader.Read8();
						break;
					}
					case EventType.NoteOn: {
						info.key = mReader.Read8();
						info.velocity = mReader.Read8();
						break;
					}
					case EventType.Aftertouch: {
						info.key = mReader.Read8();
						info.pressure = mReader.Read8();
						break;
					}
					case EventType.ControlChange: {
						info.controller = mReader.Read8();
						info.value = mReader.Read8();
						break;
					}
					case EventType.ProgramChange: {
						info.program = mReader.Read8();
						break;
					}
					case EventType.ChannelPressure: {
						info.pressure = mReader.Read8();
						break;
					}
					case EventType.PitchWheel: {
						var low = mReader.Read8();
						var high = mReader.Read8();
						info.pitch = ((high << 7) | low);
						break;
					}
				}
			} else if (status == 0xF0) {
				info.type = EventType.SysEx;
				info.data = mReader.Read8s(mReader.ReadVLQ());
			} else if (status == 0xF7) {
				info.type = EventType.EscapeSequence;
				info.data = mReader.Read8s(mReader.ReadVLQ());
			} else if (status == 0xFF) {
				info.type = EventType.Meta;
				info.metatype = (MetaEventType)mReader.Read8();
				var length = mReader.ReadVLQ();
				var start = mReader.Position;

				switch (info.metatype) {
					case MetaEventType.SequenceNumber: info.sequencenumber = mReader.Read16(); break;
					case MetaEventType.Text: info.text = mReader.ReadString(length); break;
					case MetaEventType.Copyright: info.text = mReader.ReadString(length); break;
					case MetaEventType.SequenceName: info.text = mReader.ReadString(length); break;
					case MetaEventType.InstrumentName: info.text = mReader.ReadString(length); break;
					case MetaEventType.Lyric: info.text = mReader.ReadString(length); break;
					case MetaEventType.Marker: info.text = mReader.ReadString(length); break;
					case MetaEventType.CuePoint: info.text = mReader.ReadString(length); break;
					case MetaEventType.ChannelPrefix: info.channelprefix = mReader.Read8(); break;
					case MetaEventType.Tempo: info.tempo = mReader.Read24(); break;
					case MetaEventType.EndOfTrack: mEndOfTrack = true; break;
					case MetaEventType.SequencerSpecific: info.data = mReader.Read8s(length); break;
				}

				mReader.Goto(start + length);
			} else {
				mareep.WriteError("MIDI: unknown status '{0:X2}'.", status);
			}

			mRunningStatus = (status < 0xF0 ? status : -1);

			return true;
		}

		struct TrackChunkInfo {

			public long start;
			public int size;

		}

		protected enum EventType {

			NoteOff = 0x80,
			NoteOn = 0x90,
			Aftertouch = 0xA0,
			ControlChange = 0xB0,
			ProgramChange = 0xC0,
			ChannelPressure = 0xD0,
			PitchWheel = 0xE0,
			SysEx = 0xF0,
			EscapeSequence = 0xF7,
			Meta = 0xFF,

		}

		protected enum MetaEventType {

			SequenceNumber = 0x0,
			Text = 0x1,
			Copyright = 0x2,
			SequenceName = 0x3,
			InstrumentName = 0x4,
			Lyric = 0x5,
			Marker = 0x6,
			CuePoint = 0x7,
			ChannelPrefix = 0x20,
			EndOfTrack = 0x2F,
			Tempo = 0x51,
			SequencerSpecific = 0x7F,

		}

		protected struct EventInfo {

			public int delta;
			public EventType type;
			public MetaEventType metatype;
			
			public int channel;
			public int key, velocity;
			public int pressure;
			public int controller, value;
			public int program;
			public int pitch;

			public int sequencenumber;
			public string text;
			public int channelprefix;
			public int tempo;
			public byte[] data;

		}

	}

}
