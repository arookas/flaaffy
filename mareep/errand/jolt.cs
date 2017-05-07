﻿
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace arookas.jolt {

	[Errand(Errand.Jolt)]
	class JoltErrand : MidiReader, IErrand {

		string mInput, mOutput;
		long mLoop;
		LoopTimeUnit mLoopTimeUnit;

		StreamWriter mWriter;

		Track mRootTrack;
		Track[] mChannelTracks;
		bool[] mChannelOpen;

		public JoltErrand() {
			mRootTrack = new Track();
			mChannelTracks = new Track[16];
			for (var i = 0; i < 16; ++i) {
				mChannelTracks[i] = new Track();
			}
			mChannelOpen = new bool[16];
			mLoop = -1;
		}

		public void LoadParams(string[] arguments) {
			var cmdline = new aCommandLine(arguments);
			aCommandLineParameter parameter;

			parameter = mareep.GetLastCmdParam(cmdline, "-input");

			if (parameter == null) {
				mareep.WriteError("JOLT: missing -input parameter.");
			} else if (parameter.Count == 0) {
				mareep.WriteError("JOLT: missing argument for -input parameter.");
			}

			mInput = parameter[0];

			parameter = mareep.GetLastCmdParam(cmdline, "-output");

			if (parameter == null) {
				mareep.WriteError("JOLT: missing -output parameter.");
			} else if (parameter.Count == 0) {
				mareep.WriteError("JOLT: missing argument for -output parameter.");
			}

			mOutput = parameter[0];

			parameter = mareep.GetLastCmdParam(cmdline, "-loop");

			if (parameter != null) {
				if (parameter.Count == 0) {
					mLoop = 0;
					mLoopTimeUnit = LoopTimeUnit.Pulses;
				}

				if (parameter.Count > 0 && !Int64.TryParse(parameter[0], NumberStyles.None, null, out mLoop)) {
					mareep.WriteError("JOLT: bad loop value '{0}'.", parameter[0]);
				}

				if (parameter.Count > 1) {
					switch (parameter[1].ToLowerInvariant()) {
						case "pulses":
						case "ticks": {
							mLoopTimeUnit = LoopTimeUnit.Pulses;
							break;
						}
						case "beats":
						case "quarters": {
							mLoopTimeUnit = LoopTimeUnit.Beats;
							break;
						}
						case "bars":
						case "measures": {
							mLoopTimeUnit = LoopTimeUnit.Measures;
							break;
						}
						default: mareep.WriteError("JOLT: bad time unit '{0}'.", parameter[1]); break;
					}
				}
			}
		}

		public void Perform() {
			using (var instream = mareep.OpenFile(mInput)) {
				LoadMidi(instream);

				if (Format == 2) {
					mareep.WriteError("JOLT: format-2 MIDIs are not supported.");
				}

				mareep.WriteMessage("Format-{0} MIDI {1} division {2} tracks\n", Format, Division, TrackCount);

				if (mLoop >= 0) {
					switch (mLoopTimeUnit) {
						case LoopTimeUnit.Beats: mLoop *= Division; break;
						case LoopTimeUnit.Measures: mLoop *= (Division * 4); break;
					}

					mareep.WriteMessage("Loop enabled: {0} ticks\n", mLoop, Division);
				}

				LoadTracks();
			}

			using (var outstream = mareep.CreateFile(mOutput)) {
				mWriter = new StreamWriter(outstream, Encoding.UTF8);
				mWriter.WriteLine();
				mWriter.WriteLine("# this file was generated by mareep");
				mWriter.WriteLine("# input file: {0}", Path.GetFileName(mInput));
				mWriter.WriteLine();

				WriteSeparator();
				mWriter.WriteLine();
				mWriter.WriteLine("ROOT_TRACK_BEGIN:");
				for (var i = 0; i < 16; ++i) {
					if (!mChannelOpen[i]) {
						continue;
					}

					mWriter.WriteLine("opentrack {0}, @TRACK_{0}_BEGIN", i);
				}
				mWriter.WriteLine();
				mWriter.WriteLine("timebase {0}h", Division);
				mWriter.WriteLine("load rpitch, 2b");
				mWriter.WriteLine();
				mRootTrack.Write(mWriter);
				mWriter.WriteLine("ROOT_TRACK_END:");
				mWriter.WriteLine();

				if (mLoop >= 0) {
					mWriter.WriteLine(".undefinelabel LOOP");
					mWriter.WriteLine();
				}

				for (var i = 0; i < 16; ++i) {
					if (!mChannelOpen[i]) {
						continue;
					}

					WriteSeparator();
					mWriter.WriteLine();
					mWriter.WriteLine("TRACK_{0}_BEGIN:", i);
					mWriter.WriteLine("synccpu 0");
					mChannelTracks[i].Write(mWriter);
					mWriter.WriteLine("TRACK_{0}_END:", i);
					mWriter.WriteLine();

					if (mLoop >= 0) {
						mWriter.WriteLine(".undefinelabel LOOP");
						mWriter.WriteLine();
					}

				}

				WriteSeparator();
				mWriter.WriteLine("# music sequences must be a multiple of 32 bytes");
				mWriter.WriteLine();
				mWriter.WriteLine(".align 32");
				mWriter.WriteLine();
				mWriter.Flush();
			}
		}

		void LoadTracks() {
			EventInfo ev;
			var duration = 0L;

			if (mLoop >= 0) {
				mRootTrack.AddEvent(mLoop, "LOOP: # -----------------------------------------------------------------------");

				for (var i = 0; i < 16; ++i) {
					if (mLoop >= 0) {
						mChannelTracks[i].AddEvent(mLoop, "LOOP: # -----------------------------------------------------------------------");
					}
				}
			}

			for (var i = 0; i < TrackCount; ++i) {
				GotoTrack(i);

				var time = 0L;

				while (ReadEvent(out ev)) {
					time += ev.delta;

					switch (ev.type) {
						case EventType.NoteOn: ReadNoteOn(time, ev); break;
						case EventType.NoteOff: ReadNoteOff(time, ev); break;
						case EventType.ControlChange: ReadControlChange(time, ev); break;
						case EventType.ProgramChange: ReadProgramChange(time, ev); break;
						case EventType.PitchWheel: ReadPitchWheel(time, ev); break;
						case EventType.Meta: {
							switch (ev.metatype) {
								case MetaEventType.Tempo: ReadTempo(time, ev); break;
								case MetaEventType.EndOfTrack: {
									if (duration < time) {
										duration = time;
									}
									break;
								}
							}
							break;
						}
					}
				}
			}

			var finalcommand = (mLoop >= 0 ? "jmp @LOOP" : "finish");

			mRootTrack.AddEvent(duration, finalcommand);

			for (var i = 0; i < 16; ++i) {
				if (!mChannelOpen[i]) {
					continue;
				}

				mChannelTracks[i].AddEvent(duration, finalcommand);
			}
		}

		void ReadNoteOn(long time, EventInfo ev) {
			if (ev.velocity > 0) {
				mChannelTracks[ev.channel].AddNoteOn(time, ev.key, ev.velocity);
				mChannelOpen[ev.channel] = true;
			} else {
				ReadNoteOff(time, ev);
			}
		}
		void ReadNoteOff(long time, EventInfo ev) {
			mChannelTracks[ev.channel].AddNoteOff(time, ev.key);
			mChannelOpen[ev.channel] = true;
		}
		void ReadControlChange(long time, EventInfo ev) {
			var open = true;

			switch (ev.controller) {
				case 7: mChannelTracks[ev.channel].AddEvent(time, "timedparam 0, {0}s", ev.value); break;
				case 10: mChannelTracks[ev.channel].AddEvent(time, "timedparam 3, {0}s", ev.value); break;
				case 32: mChannelTracks[ev.channel].AddEvent(time, "load rbank, {0}b", ev.value); break;
				default: open = false; break;
			}

			mChannelOpen[ev.channel] |= open;
		}
		void ReadProgramChange(long time, EventInfo ev) {
			var program = ev.program;
			if (ev.channel == 10) {
				program = (228 + (program % 12));
			}
			mChannelTracks[ev.channel].AddEvent(time, "load rprogram, {0}b", program);
			mChannelOpen[ev.channel] = true;
		}
		void ReadPitchWheel(long time, EventInfo ev) {
			mChannelTracks[ev.channel].AddEvent(time, "timedparam 1, {0}h", (ev.pitch - 8192));
			mChannelOpen[ev.channel] = true;
		}
		void ReadTempo(long time, EventInfo ev) {
			mRootTrack.AddEvent(time, "tempo {0}h", (60000000 / ev.tempo));
		}

		void WriteSeparator() {
			mWriter.WriteLine("# -----------------------------------------------------------------------------");
		}

		enum LoopTimeUnit {

			Pulses,
			Beats,
			Measures,

		}

	}

	class Track {

		List<Event> mEvents;
		int[] mActiveNotes;

		public int Count { get { return mEvents.Count; } }
		public long Duration { get { return (mEvents.Count > 0 ? mEvents[mEvents.Count - 1].Time : 0); } }

		public Track() {
			mEvents = new List<Event>();
			mActiveNotes = new int[7];
			for (var i = 0; i < 7; ++i) {
				mActiveNotes[i] = -1;
			}
		}

		public bool AddNoteOn(int key, int velocity) {
			return AddNoteOn(Duration, key, velocity);
		}
		public bool AddNoteOn(long time, int key, int velocity) {
			for (var i = 0; i < mActiveNotes.Length; ++i) {
				if (mActiveNotes[i] < 0 || mActiveNotes[i] == key) {
					mActiveNotes[i] = key;
					AddEvent(time, "noteon {0}, {1}, {2}", mareep.ConvertKey(key), velocity, (i + 1));
					return true;
				}
			}
			return false;
		}
		public bool AddNoteOff(int key) {
			return AddNoteOff(Duration, key);
		}
		public bool AddNoteOff(long time, int key) {
			for (var i = 0; i < 7; ++i) {
				if (mActiveNotes[i] == key) {
					mActiveNotes[i] = -1;
					AddEvent(time, "noteoff {0}", (i + 1));
					return true;
				}
			}
			return false;
		}
		public void AddEvent(string message) {
			AddEvent(Duration, message);
		}
		public void AddEvent(long time, string message) {
			AddEvent(new Event(time, message));
		}
		public void AddEvent(string format, params object[] arguments) {
			AddEvent(Duration, format, arguments);
		}
		public void AddEvent(long time, string format, params object[] arguments) {
			AddEvent(new Event(time, format, arguments));
		}
		void AddEvent(Event ev) {
			int i;
			for (i = 0; i < mEvents.Count; ++i) {
				if (mEvents[i].Time > ev.Time) {
					break;
				}
			}
			mEvents.Insert(i, ev);
		}

		public void Write(TextWriter writer) {
			var time = 0L;
			foreach (var ev in mEvents) {
				WriteWait(ev.Time - time, writer);
				ev.Write(writer);
				time = ev.Time;
			}
		}
		void WriteWait(long delta, TextWriter writer) {
			while (delta > 0) {
				var delay = System.Math.Min(delta, 0xFFFFFF);
				if (delay > 0xFFFF) {
					writer.WriteLine("wait {0}q", delay);
				} else if (delay > 0xFF) {
					writer.WriteLine("wait {0}h", delay);
				} else {
					writer.WriteLine("wait {0}b", delay);
				}
				delta -= delay;
			}
		}

		class Event {

			long mTime;
			string mText;

			public long Time { get { return mTime; } }

			public Event(long time, string message) : this(time, "{0}", message) { }
			public Event(long time, string format, params object[] arguments) {
				mTime = time;
				mText = String.Format(format, arguments);
			}

			public void Write(TextWriter writer) {
				writer.WriteLine(mText);
			}

		}

	}

}
