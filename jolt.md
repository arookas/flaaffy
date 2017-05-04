
## jolt

The _jolt_ errand converts a simple MIDI file to the cotton assembler language.
It supports format-0 and format-1 MIDI files and does not require any prior setup to the MIDI file.
The arguments are as follows:

|Parameter|Description|
|---------|-----------|
|-input _&lt;file&gt;_|Specifies the path and filename to the input MIDI file.|
|-output _&lt;file&gt;_|Specifies the path and filename to the output assembler text file.|

The MIDI is converted as follows:

- Each MIDI channel is output as a child track at the corresponding index.
- The polyphony is per MIDI channel and is a maximum of 7; any more notes playing simultaneously are ignored.
- The generated assembler text does not loop; this must be done by adding loops to the generated output.
- MIDI channel 10, a la the MIDI standard, is reserved for percussion instruments. Use program numbers 0&#8209;11 to select the corresponding drum set available in the selected bank.
- Aftertouch and channel pressure events are ignored.
- Only the MSB (coarse) controllers for volume and pan are used; the corresponding LSB (fine) controllers are ignored.

> **Note:** refer to the [_cotton_ errand documentation](cotton.md) for more information on compiling the assembler text to a BMS file.
