
## jolt

The _jolt_ errand converts a simple MIDI file to the cotton assembler language.
It supports format-0 and format-1 MIDI files and does not require any prior setup to the MIDI file.
The arguments are as follows:

|Parameter|Description|
|---------|-----------|
|-input _&lt;file&gt;_|Specifies the path and filename to the input MIDI file.|
|-output _&lt;file&gt;_|Specifies the path and filename to the output assembler text file.|
|-loop _&lt;amount&gt; &lt;unit&gt;_|Enables basic looping. _amount_ and _unit_ specify the unit and time at which to loop.|

#### Looping

Optionally, the output assembly can be automatically set up to loop.
If the `-loop` parameter is not specified, no looping will occur.
_unit_ specifies the time unit _amount_ is of and may be one of the following:

|Unit|Description|
|----|-----------|
|_ticks_ / _pulses_|_amount_ is in absolute MIDI ticks or pulses.|
|_beats_ / _quarters_|_amount_ is in number of quarter notes or beats.|
|_measures_ / _bars_|_amount_ is in number of measures or bars. [Common time](https://en.wikipedia.org/wiki/Common_time) is assumed.|

If both arguments are omitted, the entire song is looped front&#8209;to&#8209;back.
If _unit_ is ommited, _amount_ is measured in absolute MIDI ticks or pulses.

#### Conversion

The MIDI is converted as follows:

- Each MIDI channel is output as a child track at the corresponding index.
- The polyphony is per MIDI channel and is a maximum of 7; any more notes playing simultaneously are ignored.
- MIDI channel 10, a la the MIDI standard, is reserved for percussion instruments. Use program numbers 0&#8209;11 to select the corresponding drum set available in the selected bank.
- Aftertouch and channel pressure events are ignored.
- Only the MSB (coarse) controllers for volume and pan are used; the corresponding LSB (fine) controllers are ignored.

> **Note:** refer to the [_cotton_ errand documentation](cotton.md) for more information on compiling the assembler text to a BMS file.
