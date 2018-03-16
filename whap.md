
## whap

The _whap_ errand converts between binary and XML wave banks.
The binary format is the standard "WSYS" (or "ws") format used in Super Mario Sunshine.
Both big endian and little endian are supported.
The XML format is a basic, XML-based representation of the binary format.
The arguments are as follows:

|Parameter|Description|
|---------|-----------|
|-input _&lt;file&gt;_ _&lt;format&gt;_|Specifies the path and filename to the input file. _&lt;format&gt;_ may be one of the values listed below.|
|-output _&lt;file&gt;_ _&lt;format&gt;_|Specifies the path and filename to the output file. _&lt;format&gt;_ may be one of the values listed below.|
|-bank-dir _&lt;dir&gt;_|Used only when converting between binary and XML. Specifies the path, either absolute or relative to the output file, in which to import/export the banks. Defaults to a directory named _banks/_.|
|-wave-dir _&lt;dir&gt;_|Used only when converting between binary and XML. Specifies the path, either absolute or relative to the output file, in which to import/export the waves. Defaults to a directory named _waves/_.|
|-mix-mode _&lt;mode&gt;_|Specifies how to mix stereo waves when creating a wave archive. Available modes are listed below. If not specified, _mix_ will be used.|
|-extract-wav|Used only when converting binary to XML. Extracts all sounds to 16-bit mono LPCM .wav files.|

> **Note**: do _not_ use `-extract-wav` if you intend on repacking the wave archives. ADPCM encoding is lossy and sounds will suffer noticable deterioration through repeated decoding and encoding.

The available formats are as follows:

|Format|Description|
|------|-----------|
|_be_|Big-endian binary "WSYS" format.|
|_le_|Little-endian binary "WSYS" format.|
|_xml_|Basic XML text format.|

The available stereo-mixing modes are as follows:

|Mix Mode|Description|
|--------|-----------|
|mix|Mixes both channels together to create a mono wave.|
|left|Converts only the left stereo channel.|
|right|Converts only the right stereo channel.|

## XML format

The XML format corresponds to the binary format and designed to be extensible.
Any unknown elements and attributes are simply ignored, allowing one to embed extra data to the format for their own tools and purposes.
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

### Wave banks

The format begins with a _wave bank_, represented by the `<wave‑bank>` element.
There are no defined attributes for this element in this standard.

### Wave groups

A wave bank consists of any number of _wave groups_, represented by the `<wave‑group>` element.
Each wave group in a wave bank is specified as a child element of the root wave bank element.

|Attribute|Description|
|---------|-----------|
|`archive`|Mandatory. The path to the wave archive of this wave group, relative to the XML file.|

The wave-archive paths will be used to create the wave archive files (.aw files).
They will also be used to load them in-game;
when this happens, the game will load them relative to base wave-archive directory.
For example, in Super Mario Sunshine, this is the _AudioRes/Banks/_ directory.

### Waves

A wave group consists of any number of _waves_, represented by the `<wave>` element.
The properties of the wave are stored in the wave group and the waveform data is stored in the wave group's wave archive.

|Attribute|Description|
|---------|-----------|
|`id`|Mandatory. The id number used to identify this wave within this wave bank. Must be positive or zero.|
|`file`|Mandatory. The path and filename to this wave's audio data, relative to the XML file.|
|`format`|Mandatory. The format to which to encode the wave's audio data. For raw-audio files, this also specifies the format the raw audio itself is in. May be one of the values listed below.|
|`rate`|The sample rate, in hertz, of the wave's audio data. Mandatory for raw-audio files.|
|`key`|The key at which the wave will not offset in pitch. The value may be either a key name or key number. Defaults to 60 (C&#8209;5).|
|`loop-start`|The sample at which to wave's loop begins. If set, `loop-end` must be set, as well. If neither are set, the wave will not loop.|
|`loop-end`|The sample at which the wave's loop ends. If set, `loop-start` must be set, as well. If neither are set, the wave will not loop.|

The available raw-audio formats are as follows:

|Format|Frame Size|Description|
|------|:--------:|-----------|
|PCM8|16|Linear, 8-bit PCM, signed.|
|PCM16|32|Linear, 16-bit PCM, signed.|
|ADPCM2|5|Non-linear, 2-bit ADPCM.|
|ADPCM4|9|Non-linear, 4-bit ADPCM.|

The format of a wave's audio file is determined by the extension and must one of the following:

|Extension|Description|
|---------|-----------|
|.raw|Raw, mono audio data. Must be in one of the raw-audio formats listed above.|
|.wav|Microsoft audio-data container. Only mono or stereo LPCM of bit depths 8 or 16 are supported.|

If raw audio is used for a wave, the `rate` attribute is mandatory.
For .wav files, you may either use or override the file's sample rate depending on whether you omit or set the `rate` attribute.
