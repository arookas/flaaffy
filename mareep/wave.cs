
using System;
using System.Collections;
using System.Collections.Generic;

namespace arookas {
	
	class Wave {

		WaveFormat mFormat;
		int mRootKey;
		float mSampleRate;
		bool mLoop;
		int mLoopStart, mLoopEnd;
		int mHistoryLast, mHistoryPenult;
		int mWaveStart, mWaveSize, mSampleCount;
		string mFileName;
		int mWaveId;

		public WaveFormat Format {
			get { return mFormat; }
			set {
				if (!value.IsDefined()) {
					throw new ArgumentOutOfRangeException("value");
				}

				mFormat = value;
			}
		}
		public int RootKey {
			get { return mRootKey; }
			set {
				if (value < 0 || value > 127) {
					throw new ArgumentOutOfRangeException("value");
				}

				mRootKey = value;
			}
		}
		public float SampleRate {
			get { return mSampleRate; }
			set { mSampleRate = value; }
		}
		public bool Loop {
			get { return mLoop; }
			set { mLoop = value; }
		}
		public int LoopStart {
			get { return mLoopStart; }
			set {
				if (value < 0) {
					throw new ArgumentOutOfRangeException("value");
				}

				mLoopStart = value;
			}
		}
		public int LoopEnd {
			get { return mLoopEnd; }
			set {
				if (value < 0) {
					throw new ArgumentOutOfRangeException("value");
				}

				mLoopEnd = value;
			}
		}
		public int HistoryLast {
			get { return mHistoryLast; }
			set { mHistoryLast = value; }
		}
		public int HistoryPenult {
			get { return mHistoryPenult; }
			set { mHistoryPenult = value; }
		}
		public int WaveStart {
			get { return mWaveStart; }
			set {
				if (value < 0) {
					throw new ArgumentOutOfRangeException("value");
				}

				mWaveStart = value;
			}
		}
		public int WaveSize {
			get { return mWaveSize; }
			set {
				if (value < 0) {
					throw new ArgumentOutOfRangeException("value");
				}

				mWaveSize = value;
			}
		}
		public int SampleCount {
			get { return mSampleCount; }
			set {
				if (value < 0) {
					throw new ArgumentOutOfRangeException("value");
				}

				mSampleCount = value;
			}
		}
		public string FileName {
			get { return mFileName; }
			set { mFileName = (value ?? ""); }
		}
		public int WaveId {
			get { return mWaveId; }
			set {
				if (value < 0) {
					throw new ArgumentOutOfRangeException("value");
				}

				mWaveId = value;
			}
		}

		public Wave() {
			mFileName = "";
		}

	}

	class WaveGroup : IEnumerable<Wave> {

		string mArchiveFileName;
		List<Wave> mWaves;

		public string ArchiveFileName {
			get { return mArchiveFileName; }
			set { mArchiveFileName = (value ?? ""); }
		}

		public int Count { get { return mWaves.Count; } }

		public Wave this[int index] {
			get {
				if (index < 0 || index >= mWaves.Count) {
					return null;
				}

				return mWaves[index];
			}
		}

		public WaveGroup() {
			mArchiveFileName = "";
			mWaves = new List<Wave>(128);
		}

		public bool Add(Wave wave) {
			if (wave == null || mWaves.Contains(wave)) {
				return false;
			}

			mWaves.Add(wave);
			return true;
		}
		public bool Insert(int index, Wave wave) {
			if (wave == null || mWaves.Contains(wave)) {
				return false;
			}
			
			if (index < 0 || index > mWaves.Count) {
				return false;
			}

			mWaves.Insert(index, wave);
			return true;
		}
		public bool RemoveAt(int index) {
			if (index < 0 || index >= mWaves.Count) {
				return false;
			}

			mWaves.RemoveAt(index);
			return true;
		}
		public bool Remove(Wave wave) {
			if (wave == null) {
				return false;
			}

			return mWaves.Remove(wave);
		}
		public void Clear() { mWaves.Clear(); }

		public IEnumerator<Wave> GetEnumerator() { return mWaves.GetEnumerator(); }
		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

	}

	class WaveBank : IEnumerable<WaveGroup> {

		string mName;
		List<WaveGroup> mWaveGroups;

		public string Name {
			get { return mName; }
			set { mName = (value ?? ""); }
		}
		public int Count { get { return mWaveGroups.Count; } }

		public WaveGroup this[int index] {
			get {
				if (index < 0 || index >= mWaveGroups.Count) {
					return null;
				}

				return mWaveGroups[index];
			}
		}

		public WaveBank() {
			mName = "";
			mWaveGroups = new List<WaveGroup>(16);
		}

		public bool Add(WaveGroup waveGroup) {
			if (waveGroup == null || mWaveGroups.Contains(waveGroup)) {
				return false;
			}

			mWaveGroups.Add(waveGroup);
			return true;
		}
		public bool Insert(int index, WaveGroup waveGroup) {
			if (waveGroup == null || mWaveGroups.Contains(waveGroup)) {
				return false;
			}

			if (index < 0 || index > mWaveGroups.Count) {
				return false;
			}

			mWaveGroups.Insert(index, waveGroup);
			return true;
		}
		public bool RemoveAt(int index) {
			if (index < 0 || index >= mWaveGroups.Count) {
				return false;
			}

			mWaveGroups.RemoveAt(index);
			return true;
		}
		public bool Remove(WaveGroup waveGroup) {
			if (waveGroup == null) {
				return false;
			}

			return mWaveGroups.Remove(waveGroup);
		}
		public void Clear() { mWaveGroups.Clear(); }

		public IEnumerator<WaveGroup> GetEnumerator() { return mWaveGroups.GetEnumerator(); }
		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

	}

	enum WaveFormat {
		Adpcm4,
		Adpcm2,
		Pcm8,
		Pcm16,
	}

	static partial class mareep {

		public static int CalculateSampleCount(WaveFormat format, int size) {
			switch (format) {
				case WaveFormat.Pcm8: return size;
				case WaveFormat.Pcm16: return (size / 2);
				case WaveFormat.Adpcm2: return (size / 5 * 16);
				case WaveFormat.Adpcm4: return (size / 9 * 16);
			}

			throw new ArgumentOutOfRangeException("format");
		}

	}

}
