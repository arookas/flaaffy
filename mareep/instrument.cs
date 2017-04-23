
using arookas.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace arookas {

	enum InstrumentType {

		Melodic,
		DrumSet,

	}

	interface IInstrument {

		InstrumentType Type { get; }

	}

	class MelodicInstrument : IInstrument, IEnumerable<MelodicKeyRegion> {

		float mVolume, mPitch;
		List<MelodicKeyRegion> mKeyRegions;
		List<InstrumentEffect> mEffects;

		public InstrumentType Type { get { return InstrumentType.Melodic; } }

		public float Volume {
			get { return mVolume; }
			set { mVolume = value; }
		}
		public float Pitch {
			get { return mPitch; }
			set { mPitch = value; }
		}

		public int Count { get { return mKeyRegions.Count; } }

		public MelodicKeyRegion this[int index] { get { return GetRegionAt(index); } }

		public IEnumerable<InstrumentEffect> Effects { get { return mEffects; } }
		public int EffectCount { get { return mEffects.Count; } }

		public MelodicInstrument() : this(1.0f, 1.0f) { }
		public MelodicInstrument(float volume, float pitch) {
			mVolume = volume;
			mPitch = pitch;

			mKeyRegions = new List<MelodicKeyRegion>(8);
			mEffects = new List<InstrumentEffect>(4);
		}

		public MelodicKeyRegion AddRegion(int key) {
			if (key < 0 || key > 127) {
				throw new ArgumentOutOfRangeException("key");
			}

			int i;

			for (i = 0; i < mKeyRegions.Count; ++i) {
				if (mKeyRegions[i].Key == key) {
					return mKeyRegions[i];
				}

				if (mKeyRegions[i].Key > key) {
					break;
				}
			}

			var region = new MelodicKeyRegion(key);
			mKeyRegions.Insert(i, region);
			return region;
		}
		public MelodicKeyRegion GetRegionAt(int index) {
			if (index < 0 || index >= mKeyRegions.Count) {
				return null;
			}

			return mKeyRegions[index];
		}
		public int GetIndexOfKey(int key) {
			if (key < 0 || key > 127) {
				throw new ArgumentOutOfRangeException("key");
			}

			for (var i = 0; i < mKeyRegions.Count; ++i) {
				if (mKeyRegions[i].Key >= key) {
					return i;
				}
			}

			return -1;
		}
		public MelodicKeyRegion GetRegionOfKey(int key) {
			if (key < 0 || key > 127) {
				throw new ArgumentOutOfRangeException("key");
			}

			var index = GetIndexOfKey(key);

			return (index < 0 ? null : mKeyRegions[index]);
		}
		public void RemoveRegionAt(int index) {
			mKeyRegions.RemoveAt(index);
		}
		public void ClearRegions() { mKeyRegions.Clear(); }

		public bool AddEffect(InstrumentEffect effect) {
			if (effect == null) {
				return false;
			}

			if (mEffects.Contains(effect)) {
				return false;
			}

			mEffects.Add(effect);

			return true;
		}
		public InstrumentEffect GetEffectAt(int index) {
			if (index < 0 || index >= mEffects.Count) {
				return null;
			}

			return mEffects[index];
		}
		public bool RemoveEffect(InstrumentEffect effect) {
			if (effect == null) {
				return false;
			}

			return mEffects.Remove(effect);
		}
		public bool RemoveEffectAt(int index) {
			if (index < 0 || index > mEffects.Count) {
				return false;
			}

			mEffects.RemoveAt(index);
			return true;
		}
		public void ClearEffects() { mEffects.Clear(); }

		public IEnumerator<MelodicKeyRegion> GetEnumerator() { return mKeyRegions.GetEnumerator(); }
		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

	}

	class MelodicKeyRegion : IEnumerable<InstrumentVelocityRegion> {

		int mKey;
		List<InstrumentVelocityRegion> mVelocityRegions;

		public int Key { get { return mKey; } }

		public int Count { get { return mVelocityRegions.Count; } }

		public InstrumentVelocityRegion this[int index] { get { return GetRegionAt(index); } }

		public MelodicKeyRegion(int key) {
			if (key < 0 || key > 127) {
				throw new ArgumentOutOfRangeException("key");
			}

			mKey = key;

			mVelocityRegions = new List<InstrumentVelocityRegion>(4);
		}

		public InstrumentVelocityRegion AddRegion(int velocity, int waveid) {
			return AddRegion(velocity, waveid, 1.0f, 1.0f);
		}
		public InstrumentVelocityRegion AddRegion(int velocity, int waveid, float volume, float pitch) {
			if (velocity < 0 || velocity > 127) {
				throw new ArgumentOutOfRangeException("velocity");
			}

			if (waveid < 0) {
				throw new ArgumentOutOfRangeException("waveid");
			}

			if (volume < 0.0f) {
				throw new ArgumentOutOfRangeException("volume");
			}

			int i;

			for (i = 0; i < mVelocityRegions.Count; ++i) {
				if (mVelocityRegions[i].Velocity == velocity) {
					throw new ArgumentOutOfRangeException("velocity");
				}

				if (mVelocityRegions[i].Velocity > velocity) {
					break;
				}
			}

			var region = new InstrumentVelocityRegion(velocity, waveid, volume, pitch);
			mVelocityRegions.Insert(i, region);
			return region;
		}
		public InstrumentVelocityRegion GetRegionAt(int index) {
			if (index < 0 || index >= mVelocityRegions.Count) {
				return null;
			}

			return mVelocityRegions[index];
		}
		public int GetIndexOfVelocity(int velocity) {
			if (velocity < 0 || velocity > 127) {
				throw new ArgumentOutOfRangeException("velocity");
			}

			for (var i = 0; i < mVelocityRegions.Count; ++i) {
				if (mVelocityRegions[i].Velocity >= velocity) {
					return i;
				}
			}

			return -1;
		}
		public InstrumentVelocityRegion GetRegionOfVelocity(int velocity) {
			if (velocity < 0 || velocity > 127) {
				throw new ArgumentOutOfRangeException("velocity");
			}

			var index = GetIndexOfVelocity(velocity);

			return (index < 0 ? null : mVelocityRegions[index]);
		}
		public bool RemoveRegionAt(int index) {
			if (index < 0 || index >= mVelocityRegions.Count) {
				return false;
			}

			mVelocityRegions.RemoveAt(index);
			return true;
		}
		public void ClearRegions() { mVelocityRegions.Clear(); }

		public IEnumerator<InstrumentVelocityRegion> GetEnumerator() { return mVelocityRegions.GetEnumerator(); }
		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

	}

	class InstrumentVelocityRegion {

		int mVelocity;
		int mWaveId;
		float mVolume, mPitch;

		public int Velocity { get { return mVelocity; } }
		public int WaveId {
			get { return mWaveId; }
			set {
				if (value < 0) {
					throw new ArgumentOutOfRangeException("value");
				}

				mWaveId = value;
			}
		}
		public float Volume {
			get { return mVolume; }
			set { mVolume = value; }
		}
		public float Pitch {
			get { return mPitch; }
			set { mPitch = value; }
		}

		public InstrumentVelocityRegion(int velocity, int waveid) : this(velocity, waveid, 1.0f, 1.0f) { }
		public InstrumentVelocityRegion(int velocity, int waveid, float volume, float pitch) {
			if (velocity < 0 || velocity > 127) {
				throw new ArgumentOutOfRangeException("velocity");
			}

			if (waveid < 0) {
				throw new ArgumentOutOfRangeException("waveid");
			}

			mVelocity = velocity;
			mWaveId = waveid;
			mVolume = volume;
			mPitch = pitch;
		}

	}

	class DrumSet : IInstrument, IEnumerable<Percussion> {

		Percussion[] mPercussions;

		public InstrumentType Type { get { return InstrumentType.DrumSet; } }

		public Percussion this[int key] { get { return GetPercussionAt(key); } }

		public int Capacity { get { return 128; } }
		public int Count {
			get { return mPercussions.Count(percussion => percussion != null); }
		}

		public IEnumerable<Percussion> Percussions { get { return mPercussions; } }

		public DrumSet() {
			mPercussions = new Percussion[128];
		}

		public Percussion AddPercussion(int key) {
			if (key < 0 || key > 127) {
				return null;
			}

			if (mPercussions[key] == null) {
				mPercussions[key] = new Percussion(key);
			}

			return mPercussions[key];
		}
		public Percussion GetPercussionAt(int key) {
			if (key < 0 || key > 127) {
				return null;
			}

			return mPercussions[key];
		}
		public bool RemovePercussionAt(int key) {
			if (key < 0 || key > 127) {
				return false;
			}

			mPercussions[key] = null;
			return true;
		}
		public void ClearPercussion() {
			for (var i = 0; i < 128; ++i) {
				mPercussions[i] = null;
			}
		}

		public IEnumerator<Percussion> GetEnumerator() { return mPercussions.GetArrayEnumerator(); }
		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

	}

	class Percussion : IEnumerable<InstrumentVelocityRegion> {

		int mKey;
		List<InstrumentVelocityRegion> mVelocityRegions;
		List<InstrumentEffect> mEffects;
		float mVolume, mPitch, mPan;
		int mRelease;

		public int Key { get { return mKey; } }

		public float Volume {
			get { return mVolume; }
			set { mVolume = value; }
		}
		public float Pitch {
			get { return mPitch; }
			set { mPitch = value; }
		}
		public float Pan {
			get { return mPan; }
			set { mPan = value; }
		}
		public int Release {
			get { return mRelease; }
			set {
				if (value < 0 || value > 65535) {
					throw new ArgumentOutOfRangeException("value");
				}

				mRelease = value;
			}
		}

		public int Count { get { return mVelocityRegions.Count; } }

		public InstrumentVelocityRegion this[int index] { get { return GetRegionAt(index); } }

		public int EffectCount { get { return mEffects.Count; } }
		public IEnumerable<InstrumentEffect> Effects { get { return mEffects; } }

		public Percussion(int key) {
			if (key < 0 || key > 127) {
				throw new ArgumentOutOfRangeException("key");
			}

			mKey = key;
			mVolume = 1.0f;
			mPitch = 1.0f;
			mPan = 0.5f;
			mRelease = 1000;

			mVelocityRegions = new List<InstrumentVelocityRegion>(4);
			mEffects = new List<InstrumentEffect>(4);
		}

		public InstrumentVelocityRegion AddRegion(int velocity, int waveid) {
			return AddRegion(velocity, waveid, 1.0f, 1.0f);
		}
		public InstrumentVelocityRegion AddRegion(int velocity, int waveid, float volume, float pitch) {
			if (velocity < 0 || velocity > 127) {
				throw new ArgumentOutOfRangeException("velocity");
			}

			if (waveid < 0) {
				throw new ArgumentOutOfRangeException("waveid");
			}

			if (volume < 0.0f) {
				throw new ArgumentOutOfRangeException("volume");
			}

			int i;

			for (i = 0; i < mVelocityRegions.Count; ++i) {
				if (mVelocityRegions[i].Velocity == velocity) {
					throw new ArgumentOutOfRangeException("velocity");
				}

				if (mVelocityRegions[i].Velocity > velocity) {
					break;
				}
			}

			var region = new InstrumentVelocityRegion(velocity, waveid, volume, pitch);
			mVelocityRegions.Insert(i, region);
			return region;
		}
		public InstrumentVelocityRegion GetRegionAt(int index) {
			if (index < 0 || index >= mVelocityRegions.Count) {
				return null;
			}

			return mVelocityRegions[index];
		}
		public int GetIndexOfVelocity(int velocity) {
			if (velocity < 0 || velocity > 127) {
				throw new ArgumentOutOfRangeException("velocity");
			}

			for (var i = 0; i < mVelocityRegions.Count; ++i) {
				if (mVelocityRegions[i].Velocity >= velocity) {
					return i;
				}
			}

			return -1;
		}
		public InstrumentVelocityRegion GetRegionOfVelocity(int velocity) {
			if (velocity < 0 || velocity > 127) {
				throw new ArgumentOutOfRangeException("velocity");
			}

			var index = GetIndexOfVelocity(velocity);

			return (index < 0 ? null : mVelocityRegions[index]);
		}
		public bool RemoveRegionAt(int index) {
			if (index < 0 || index >= mVelocityRegions.Count) {
				return false;
			}

			mVelocityRegions.RemoveAt(index);
			return true;
		}
		public void ClearRegions() { mVelocityRegions.Clear(); }

		public bool AddEffect(InstrumentEffect effect) {
			if (effect == null) {
				return false;
			}

			if (mEffects.Contains(effect)) {
				return false;
			}

			mEffects.Add(effect);

			return true;
		}
		public InstrumentEffect GetEffectAt(int index) {
			if (index < 0 || index >= mEffects.Count) {
				return null;
			}

			return mEffects[index];
		}
		public bool RemoveEffect(InstrumentEffect effect) {
			if (effect == null) {
				return false;
			}

			return mEffects.Remove(effect);
		}
		public bool RemoveEffectAt(int index) {
			if (index < 0 || index >= mEffects.Count) {
				return false;
			}

			mEffects.RemoveAt(index);
			return true;
		}
		public void ClearEffects() { mEffects.Clear(); }

		public IEnumerator<InstrumentVelocityRegion> GetEnumerator() { return mVelocityRegions.GetEnumerator(); }
		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

	}

	class InstrumentBank : IEnumerable<IInstrument> {

		string mName;
		WaveBank mWaveBank;
		int mVirtualNumber;
		IInstrument[] mInstruments;

		public string Name {
			get { return mName; }
			set { mName = (value ?? ""); }
		}
		public WaveBank WaveBank {
			get { return mWaveBank; }
			set { mWaveBank = value; }
		}
		public int VirtualNumber { get { return mVirtualNumber; } }
		public int Count { get { return mInstruments.Count(i => i != null); } }
		public int Capacity { get { return mInstruments.Length; } }

		public IInstrument this[int index] {
			get { return mInstruments[index]; }
		}

		public InstrumentBank(int virtualNumber, int count) {
			if (virtualNumber < 0 || virtualNumber > 65535) {
				throw new ArgumentOutOfRangeException("virtualNumber");
			}

			if (count < 0) {
				throw new ArgumentOutOfRangeException("count");
			}

			mName = "";
			mVirtualNumber = virtualNumber;
			mInstruments = new IInstrument[count];
		}

		public void Add(int index, IInstrument instrument) {
			if (index < 0 || index >= mInstruments.Length) {
				throw new ArgumentOutOfRangeException("index");
			}

			mInstruments[index] = instrument;
		}
		public bool RemoveAt(int index) {
			if (index < 0 || index >= mInstruments.Length) {
				return false;
			}

			mInstruments[index] = null;
			return true;
		}

		public IEnumerator<IInstrument> GetEnumerator() { return mInstruments.GetArrayEnumerator(); }
		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

	}

	enum InstrumentEffectTarget {

		Volume,
		Pitch,
		Pan,
		Fxmix,
		Dolby,

	}

	abstract class InstrumentEffect {

		protected InstrumentEffectTarget mTarget;

		public InstrumentEffectTarget Target {
			get { return mTarget; }
			set {
				if (!value.IsDefined()) {
					throw new ArgumentOutOfRangeException("value");
				}

				mTarget = value;
			}
		}

		public InstrumentEffect() : this(InstrumentEffectTarget.Volume) { }
		public InstrumentEffect(InstrumentEffectTarget target) {
			if (!target.IsDefined()) {
				throw new ArgumentOutOfRangeException("target");
			}

			mTarget = target;
		}

		public abstract float GetValue(int key, int velocity);

	}

	class RandomInstrumentEffect : InstrumentEffect {

		protected float mRandomBase, mRandomDistance;

		protected static readonly Random sRandom = new Random(0);

		public float RandomBase {
			get { return mRandomBase; }
			set { mRandomBase = value; }
		}
		public float RandomDistance {
			get { return mRandomDistance; }
			set { mRandomDistance = value; }
		}

		public RandomInstrumentEffect() : this(InstrumentEffectTarget.Volume) { }
		public RandomInstrumentEffect(InstrumentEffectTarget target) : this(target, 1.0f, 0.0f) { }
		public RandomInstrumentEffect(InstrumentEffectTarget target, float randomBase, float randomDistance)
			: base(target) {
			mRandomBase = randomBase;
			mRandomDistance = randomDistance;
		}

		public override float GetValue(int key, int velocity) {
			if (key < 0 || key > 127) {
				throw new ArgumentOutOfRangeException("key");
			}

			if (velocity < 0 || velocity > 127) {
				throw new ArgumentOutOfRangeException("velocity");
			}

			return (mRandomBase + (2.0f * (float)sRandom.NextDouble() - 1.0f) * mRandomDistance);
		}

	}

	enum SenseInstrumentEffectSource {

		None,
		Key,
		Velocity,

	}

	class SenseInstrumentEffect : InstrumentEffect {

		protected SenseInstrumentEffectSource mSource;
		protected int mWeight;
		protected float mRangeLo, mRangeHi;

		public SenseInstrumentEffectSource Source {
			get { return mSource; }
			set {
				if (!value.IsDefined()) {
					throw new ArgumentOutOfRangeException("source");
				}

				mSource = value;
			}
		}
		public int Weight {
			get { return mWeight; }
			set {
				if (value < 0 || value > 127) {
					throw new ArgumentOutOfRangeException("value");
				}

				mWeight = value;
			}
		}
		public float RangeLo {
			get { return mRangeLo; }
			set { mRangeHi = value; }
		}
		public float RangeHi {
			get { return mRangeHi; }
			set { mRangeHi = value; }
		}

		public SenseInstrumentEffect() : this(InstrumentEffectTarget.Volume) { }
		public SenseInstrumentEffect(InstrumentEffectTarget target) : this(target, SenseInstrumentEffectSource.Key, 127, 0.0f, 1.0f) { }
		public SenseInstrumentEffect(InstrumentEffectTarget target, SenseInstrumentEffectSource source, int weight, float rangeLo, float rangeHi)
			: base(target) {
			if (!source.IsDefined()) {
				throw new ArgumentOutOfRangeException("source");
			}

			if (weight < 0 || weight > 127) {
				throw new ArgumentOutOfRangeException("limit");
			}

			mSource = source;
			mWeight = weight;
			mRangeLo = rangeLo;
			mRangeHi = rangeHi;
		}

		public override float GetValue(int key, int velocity) {
			if (key < 0 || key > 127) {
				throw new ArgumentOutOfRangeException("key");
			}

			if (velocity < 0 || velocity > 127) {
				throw new ArgumentOutOfRangeException("velocity");
			}

			int value = 0;

			switch (mSource) {
				case SenseInstrumentEffectSource.Key: value = key; break;
				case SenseInstrumentEffectSource.Velocity: value = velocity; break;
			}

			if (mWeight == 127 || mWeight == 0) {
				return (mRangeLo + value * (mRangeHi - mRangeLo) / 127.0f);
			} else if (value < mWeight) {
				return (mRangeLo + (1.0f - mRangeLo) * ((float)value / (float)mWeight));
			} else {
				return (1.0f + (mRangeHi - 1.0f) * ((float)(value - mWeight) / (float)(127 - mWeight)));
			}
		}

	}

}
