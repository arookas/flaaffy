
using arookas.Collections;
using arookas.Xml;
using System;
using System.Collections;
using System.Collections.Generic;

namespace arookas {

	class DataFile {

		List<InstrumentBank> mBanks;
		List<WaveBank> mWaveBanks;

		public IEnumerable<InstrumentBank> Banks { get { return mBanks; } }
		public IEnumerable<WaveBank> WaveBanks { get { return mWaveBanks; } }

		public int BankCount { get { return mBanks.Count; } }
		public int WaveBankCount { get { return mWaveBanks.Count; } }

		public DataFile() {
			mBanks = new List<InstrumentBank>(16);
			mWaveBanks = new List<WaveBank>(16);
		}

		public static DataFile LoadXml(xElement element) {
			return null;
		}

	}

	class SoundTable : IEnumerable<SoundTableCategory> {

		SoundTableCategory[] mCategories;

		public int Count { get { return 18; } }
		public SoundTableCategory this[int index] { get { return mCategories[index]; } }

		public SoundTable() {
			mCategories = new SoundTableCategory[18];

			for (var i = 0; i < 18; ++i) {
				mCategories[i] = new SoundTableCategory();
			}
		}

		public IEnumerator<SoundTableCategory> GetEnumerator() { return mCategories.GetArrayEnumerator(); }
		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

	}

	class SoundTableCategory : IEnumerable<SoundTableEntry> {

		string mName;
		List<SoundTableEntry> mEntries;

		public string Name {
			get { return mName; }
			set { mName = (value ?? ""); }
		}

		public int Count { get { return mEntries.Count; } }
		public SoundTableEntry this[int index] { get { return mEntries[index]; } }

		public SoundTableCategory() {
			mEntries = new List<SoundTableEntry>();
		}

		public bool Add(SoundTableEntry entry) {
			if (entry == null || mEntries.Contains(entry)) {
				return false;
			}

			mEntries.Add(entry);
			return true;
		}
		public bool Remove(SoundTableEntry entry) {
			if (entry == null) {
				return false;
			}

			return mEntries.Remove(entry);
		}
		public bool RemoveAt(int index) {
			if (index < 0 || index > mEntries.Count) {
				return false;
			}

			mEntries.RemoveAt(index);
			return true;
		}
		public void Clear() { mEntries.Clear(); }

		public IEnumerator<SoundTableEntry> GetEnumerator() { return mEntries.GetEnumerator(); }
		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

	}

	class SoundTableEntry {

		string mName;
		bool mRandom;
		int mRandomChance;
		int mPriority;
		int mTrackNumber;

		public string Name {
			get { return mName; }
			set { mName = (value ?? ""); }
		}
		
		public bool Random {
			get { return mRandom; }
			set { mRandom = value; }
		}
		public int RandomChance {
			get { return mRandomChance; }
			set {
				if (value < 0 || value > 7) {
					throw new ArgumentOutOfRangeException("value");
				}

				mRandomChance = value;
			}
		}

		public int Priority {
			get { return mPriority; }
			set {
				if (value < 0 || value > 255) {
					throw new ArgumentOutOfRangeException("value");
				}

				mPriority = value;
			}
		}
		public int TrackNumber {
			get { return mTrackNumber; }
			set {
				if (value < 0 || value > 255) {
					throw new ArgumentOutOfRangeException("value");
				}

				mTrackNumber = value;
			}
		}

	}

}
