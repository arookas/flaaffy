
## shock

The _shock_ errand converts between binary and XML banks.
The binary format is the standard "IBNK" (or "bnk") format used in Super Mario Sunshine.
Both big endian and little endian are supported.
The XML format is a basic, XML-based representation of the binary format.
The arguments are as follows:

|Parameter|Description|
|---------|-----------|
|-input _&lt;file&gt;_ _&lt;format&gt;_|Specifies the path and filename to the input file. _&lt;format&gt;_ may be one of the values listed below.|
|-output _&lt;file&gt;_ _&lt;format&gt;_|Specifies the path and filename to the output file. _&lt;format&gt;_ may be one of the values as listed below.|

The available formats are as follows:

|Format|Description|
|------|-----------|
|_be_|Big-endian binary "IBNK" format.|
|_le_|Little-endian binary "IBNK" format.|
|_xml_|Basic XML text format.|

## XML format

The XML format corresponds to the binary format and designed to be extensible.
Any unknown elements and attribute are simply ignored, allowing one to embed extra data to the format for their own tools and purposes.
In general, the names of elements and attributes are lowercase and case sensitive.

#### Key numbers

Key numbers are used by several attributes in the format.
They are based on MIDI and are in the range of 0&#8209;127.
The value of key-number attribute may be either a key name or key number.
For key names, the key letter must be uppercase; the characters `-`, `#`, and `b` are used as natural and the sharp and flat accidentals, respectively.
Key names begin at C&#8209;0 and count upwards to G&#8209;10:

| |+0|+1|+2|+3|+4|+5|+6|+7|+8|+9|+10|+11|
|-|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
|**0**|C&#8209;0|C#0|D&#8209;0|Eb0|E&#8209;0|F&#8209;0|F#0|G&#8209;0|G#0|A&#8209;0|Bb0|B&#8209;0|
|**12**|C&#8209;1|C#1|D&#8209;1|Eb1|E&#8209;1|F&#8209;1|F#1|G&#8209;1|G#1|A&#8209;1|Bb1|B&#8209;1|
|**24**|C&#8209;2|C#2|D&#8209;2|Eb2|E&#8209;2|F&#8209;2|F#2|G&#8209;2|G#2|A&#8209;2|Bb2|B&#8209;2|
|**36**|C&#8209;3|C#3|D&#8209;3|Eb3|E&#8209;3|F&#8209;3|F#3|G&#8209;3|G#3|A&#8209;3|Bb3|B&#8209;3|
|**48**|C&#8209;4|C#4|D&#8209;4|Eb4|E&#8209;4|F&#8209;4|F#4|G&#8209;4|G#4|A&#8209;4|Bb4|B&#8209;4|
|**60**|C&#8209;5|C#5|D&#8209;5|Eb5|E&#8209;5|F&#8209;5|F#5|G&#8209;5|G#5|A&#8209;5|Bb5|B&#8209;5|
|**72**|C&#8209;6|C#6|D&#8209;6|Eb6|E&#8209;6|F&#8209;6|F#6|G&#8209;6|G#6|A&#8209;6|Bb6|B&#8209;6|
|**84**|C&#8209;7|C#7|D&#8209;7|Eb7|E&#8209;7|F&#8209;7|F#7|G&#8209;7|G#7|A&#8209;7|Bb7|B&#8209;7|
|**96**|C&#8209;8|C#8|D&#8209;8|Eb8|E&#8209;8|F&#8209;8|F#8|G&#8209;8|G#8|A&#8209;8|Bb8|B&#8209;8|
|**108**|C&#8209;9|C#9|D&#8209;9|Eb9|E&#8209;9|F&#8209;9|F#9|G&#8209;9|G#9|A&#8209;9|Bb9|B&#8209;9|
|**120**|C&#8209;10|C#10|D&#8209;10|Eb10|E&#8209;10|F&#8209;10|F#10|G&#8209;10|||||

### Banks

The format begins with a bank, represented by the `<bank>` element:

|Attribute|Description|
|---------|-----------|
|`virtual‑number`|Mandatory. Specifies the virtual bank number for this bank. This is the number used to select the bank in a sequence. Must be positive or zero.|

### Instruments

A bank has up to 256 programs, each assigned a program number used to reference it elsewhere.
An instrument may either be a _melodic instrument_ or _drum set_.
In the XML format, a bank's instruments are specified as child elements to the bank element.

#### Melodic

A melodic instrument is represented by the `<instrument>` element.
They have base properties and are made up of oscillators, effects, key regions, and velocity regions to modulate these properties.

|Attribute|Description|
|---------|-----------|
|`program`|Mandatory. Specifies the program number for this instrument. This is the number used to select the program in a sequence. Must be 0&#8209;255.|
|`volume`|The base volume of this instrument, given as a decimal. Defaults to one.|
|`pitch`|The base tuning of this instrument, given as a decimal. Values higher than one tune up in pitch, whereas values lower than one tune lower in pitch. Defaults to one.|

#### Key region

A key region is represented by the `<key‑region>` element.
They modulate properties of a given region of key numbers and are made up of one or more velocity regions.

Each key region is assigned a _last key_ which specifies the highest key number governed by the key region.
The first key is implicit: if the key region is the first in the instrument, the first key is assumed to be C&#8209;0 (or zero); otherwise, it is assumed to be the key after the last key of the previous key region in the instrument.

The key regions must be given in ascending order with no duplicates.
The only valid keys are C&#8209;0 to G&#8209;10 (or 0&#8209;127).

|Attribute|Description|
|---------|-----------|
|`key`|Specifies the last key in the key region. The value may be either an integer or key name. Defaults to G&#8209;10 (127).|

The velocity regions of the key region are specified as child elements of the key region element.

> **Note:** if the melodic instrument has only one key region, you may omit the key attribute to imply all key numbers. Otherwise, you may omit the key attribute for the last key region to imply the remaining key numbers.

#### Drum set

A drum set is represented by the `<drum‑set>` element.
Rather than work on regions of keys, a drum set assigns a percussion to each key.
No oscillators may be applied: a drum set's oscillators are implicit to the format.

|Attribute|Description|
|---------|-----------|
|`program`|Mandatory. Specifies the program number for this instrument. This is the number used to select the program in a sequence. Must be 0&#8209;255.|

Each percussion of the drum set is specified as a child element of the drum set element.
Only one percussion may be assigned to any given key number; any duplicates is considered erroneous.

#### Percussion

A percussion is a sound applied to a single key in a drum set and is represented by the `<percussion>` element.
They consist of effects and velocity regions.

|Attribute|Description|
|---------|-----------|
|`key`|Mandatory. Specifies the key to which to assign the percussion. The value may be either an integer or key name.|

#### Velocity regions

A velocity region is represented by the `<velocity‑region>` element.
They are assigned a _wave id_ and modulate properties of a given key region or percussion based on the velocity of the note.

Each velocity is assigned a _last velocity_ which specifies the highest velocity governed by the velocity region.
The first velocity is implicit: if the velocity region is the first in the instrument, the first velocity is assumed to be zero; otherwise, it is assumed to be one more than the last velocity of the previous velocity region in the instrument.

The velocity regions must be given in ascending order with no duplicates.
The only valid velocities are 0&#8209;127.

|Attribute|Description|
|---------|-----------|
|`velocity`|Specifies the last velocity in the velocity region. The value must be an integer in the range of 0&#8209;127. Defaults to 127.|
|`wave‑id`|Mandatory. Specifies the wave id of the wave to play for this velocity region. Must be positive or zero.|
|`volume`|The volume modifier, specified as a decimal. Values higher than one will louden the note, whereas values lower than one will soften the note. Defaults to one.|
|`pitch`|The fine&#8209;tuning modifier, specified as a decimal. Values higher than one will tune upwards in pitch, whereas values lower than one will tune downwards in pitch. Defaults to one.|

> **Note:** if the key region or percussion has only one velocity region, you may omit the velocity attribute to imply all velocities. Otherwise, you may omit the velocity attribute for the last velocity region to imply the remaining velocities.

### Oscillators

An oscillator in this case represents an envelope that modulates a specific property over time.
There are rate, width, and base properties, as well as the actual envelope tables.
In the XML format, oscillators are represented by the `<oscillator>` element:

|Attribute|Description|
|---------|-----------|
|`target`|Mandatory. Specifies the target property of the oscillator. May be one of the following values: _volume_, _pitch_, _pan_, _fxmix_, _dolby_.|
|`rate`|Specifies the rate, or speed, of the oscillator. Larger values speed it up, whereas lower values slow it down. Defaults to one.|
|`width`|Specifies the width of the oscillator. All amounts on the oscillator's tables are a factor of this width value. Defaults to one.|
|`base`|Specifies the base of the oscillator. The modulation will center around this value. Defaults to zero.|

An oscillator may have up to two tables: a _start table_ and a _release table_.
Tables set up how the oscillator modulates over time.
A start table is represented by the `<start‑table>` element.
A release table is represent by the `<release‑table>` element.

A table consists of one or more points, each controlling either the flow of the table or the envelope.
For envelope points, the following interpolation styles are as follows:

|Type|Graph|Description|
|----|-----|-----------|
|`linear`||Simple linear interpolation between values.|
|`square`|<img src="http://i.imgur.com/9UfyZPy.png" height="48">|A sinusoidal interpolation easing out from the previous point.|
|`square‑root`|<img src="http://i.imgur.com/AcwaM9b.png" height="48">|A sinusoidal interpolation easing into the next point.|
|`sample‑cell`|<img src="http://i.imgur.com/ZAq7QE2.png" height="48">|An interpolation that has a sharp drop and eases out at the end. This best simulates actual sound attenuation.|

Each modulation point takes the following attributes:

|Attribute|Description|
|---------|-----------|
|`time`|The number of oscillator ticks to interpolate from the current oscillator offset to this point's offset. There are approximately 600 oscillator ticks per second. A time of zero indicates an instant change of value. Defaults to zero.|
|`offset`|The offset of this point, ranging from -32,768&#8209;32,767. This is later normalized to range of ±1 and scaled by the oscillator's width to calculate the actual offset of this point. Defaults to zero.|

Flow&#8209;control points must be the last point on either table.
A table which does not end with a flow&#8209;control point or has more than one is considered invalid.
The available flow&#8209;control points are as follows:

|Type|Attributes|Description|
|----|----------|-----------|
|`loop`|`dest`|Loops the oscillator back to the point at the zero-based index specified by `dest`. Defaults to zero.|
|`hold`||Halts the oscillator at its current offset until it is released. Should not be used in the release table.|
|`stop`||Stops the oscillator completely. Usually this is found in the release table.|

An example of a simple ASDR envelope with an attack of 100 milliseconds and a release of 2 seconds:

```xml
<oscillator target="volume" width="1" base="0">
	<start-table>
		<linear time="0" offset="32767" />
		<linear time="100" offset="29000" />
		<hold />
	</start-table>
	<release-table>
		<sample-cell time="1200" offset="0" />
		<stop />
	</release-table>
</oscillator>
```

An example of a simple vibrato:

```xml
<oscillator target="pitch" width="0.1" base="1">
	<start-table>
		<linear time="0" offset="0" />
		<square-root time="100" offset="32767" />
		<square time="100" offset="0" />
		<square-root time="100" offset="-32768" />
		<square time="100" offset="0" />
		<loop />
	</start-table>
</oscillator>
```

### Effects
A property of an instrument can be further modulated by use of effects.
A melodic instrument or percussion may have any number of effects applied.
Effects are calculated once per note and are afterwards static the entire duration of the note.
Effects are written as child elements to the parent element and end with `-effect`.
There are two kinds of effects: _random_ and _sense_.

> **Note:** Any unknown effect types are simply ignored.

#### Random

Random effects modulate a property to a random offset and are represented by the `<random‑effect>` element.

|Attribute|Description|
|---------|-----------|
|`target`|Mandatory. Specifies the target property of the effect. May be one of the following values: _volume_, _pitch_, _pan_, _fxmix_, _dolby_.|
|`base`|Base offset of the effect, specified as a decimal. The random value will randomize a random distance away from this value in either direction. Defaults to one.|
|`distance`|Maximum distance of the effect, specified as a decimal. The random value will not go further from the base than this in either direction. Defaults to zero.|

#### Sense

Sense effects modulate a property linearly based on the specified trigger and are represented by the `<sense‑effect>` element.

|Attribute|Description|
|---------|-----------|
|`target`|Mandatory. Specifies the target property of the effect. May be one of the following values: _volume_, _pitch_, _pan_, _fxmix_, _dolby_.|
|`trigger`|Specifies what value to use as the trigger. May be one of the following values: _key_, _velocity_.|
|`center‑key`|Specifies the center key. The target property is modulated based on whether the input from the trigger is above or below this value. The value may be either a key number, key name, or velocity. Defaults to 127.|
|`range‑lo`|Specifies the bottom range of the effect, specified in decimal. Defaults to zero.|
|`range‑hi`|Specifies the top range of the effect, specified in decimal. Defaults to one.|

How the property is modulated depends on the _trigger value_ and the _center key_:

- If the _center key_ is zero or 127, the result is a linear interpolation from _range lo_ to _range hi_ as the _trigger value_ goes from zero to 127.
- If the _trigger value_ is below the _center key_, the result is a linear interpolation from _range lo_ to one as the _trigger value_ goes from zero to _center key_.
- If the _trigger value is above the _center key_, the result is a linear interpolation from one to _range hi_ as the _trigger value_ goes from _center key_ to 127.
