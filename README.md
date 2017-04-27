
## flaaffy v.0.1

### Summary

_flaaffy_ is a simple audio toolchain for Super Mario Sunshine.
At its core, it is a runtime library for loading, utilizing, and playing the various audio-related formats of the game. There are also a series of tools and utilities to convert and create these formats.

### Compiling

To compile _flaaffy_, you'll need to have the following libraries compiled and/or installed:

- [arookas library](http://github.com/arookas/arookas)

The repository contains a [premake5](https://premake.github.io/) [script](premake5.lua).
Simply run the script with premake5 and build the resulting solution.

> **Note:** You might need to fill in any unresolved-reference errors by supplying your IDE with the paths to the dependencies listed above.

## Usage

As of now, _flaaffy_ toolkit contains a swiss-army-knife utility program called _mareep_.

### mareep

_mareep_ is utility program able to convert many audio-related formats.
It is a command-line interface, where each feature is implemented as an "action".
The arguments follow this format:

```
mareep -action <name> [<arguments>]
```

The available actions are as follows:

|Action|Description|
|------|-----------|
|shock|Converts instrument banks ("IBNK" or "bnk") to&#8209;and&#8209;fro XML and binary formats. Little endian and big endian are supported.|
|whap|Converts wave banks ("WSYS" or "ws") to&#8209;and&#8209;fro XML and binary formats. Little endian and big endian are supported. Automatically extracts and repacks the wave archives (.aw files). Includes PCM&nbsp;⇄&nbsp;ADPCM conversion.|
|wave|Standalone action to convert raw audio data PCM&nbsp;⇄&nbsp;ADPCM. Any combination of input and output formats is supported.|
|cotton|A dedicated BMS assembler. Able to compile BMS files from no&#8209;holds&#8209;barred assembly text. Features relocation, named labels, variables, embedded POD, and various other directives.|

#### shock

The _shock_ action converts between binary and XML banks.
The binary format is the standard "IBNK" (or "bnk") format used in Super Mario Sunshine.
Both big endian and little endian are supported.
The XML format is a basic, XML-based representation of the binary format.
The arguments are as follows:

|Parameter|Description|
|---------|-----------|
|-input _&lt;file&gt;_ _&lt;format&gt;_|Specifies the path and filename to the input file. _&lt;format&gt;_ may be one of following: _xml_, _bigbinary_, or _littlebinary_.|
|-output _&lt;file&gt;_ _&lt;format&gt;_|Specifies the path and filename to the output file. _&lt;format&gt;_ may be one of the values as listed for -input.|

> **Note:** Documentation on the XML format is soon to come.

#### whap

The _whap_ action converts between binary and XML wave banks.
The binary format is the standard "WSYS" (or "ws") format used in Super Mario Sunshine.
Both big endian and little endian are supported.
The XML format is a basic, XML-based representation of the binary format.
The arguments are as follows:

|Parameter|Description|
|---------|-----------|
|-input _&lt;file&gt;_ _&lt;format&gt;_|Specifies the path and filename to the input file. _&lt;format&gt;_ may be one of following: _xml_, _bigbinary_, or _littlebinary_.|
|-output _&lt;file&gt;_ _&lt;format&gt;_|Specifies the path and filename to the output file. _&lt;format&gt;_ may be one of the values as listed for -input.|
|-wave-dir _&lt;dir&gt;_|Optional setting to specify the path in which to export all of the waves. Used only during binary&nbsp;⇒&nbsp;XML conversion. Defaults to a directory named "waves" in the same directory as the output file.|
|-mix-mode _&lt;mode&gt;_|Specifies how to mix stereo waves when creating a wave archive. Available modes are listed below. Defaults to _mix_.|

The available stereo-mixing modes are as follows:

|Mix Mode|Description|
|--------|-----------|
|mix|Mixes both channels together to create a mono wave.|
|left|Converts only the left stereo channel.|
|right|Converts only the right stereo channel.|

> **Note:** Documentation on the XML format is soon to come.

#### wave

The _wave_ action converts among the various audio formats used by Super Mario Sunshine.
The available raw-audio formats are:

|Format|Frame Size|Description|
|------|:--------:|-----------|
|PCM8|16|Linear, 8-bit PCM, signed.|
|PCM16|32|Linear, 16-bit PCM, signed.|
|ADPCM2|5|Non-linear, 2-bit ADPCM.|
|ADPCM4|9|Non-linear, 4-bit ADPCM.|

> **Note:** sizes are measured in bytes per frame (i.e. 16 samples). All formats are mono. The ADPCM formats use a hardcoded, two-dimensional coefficient table. If you are unsure which to use when importing a sound into the game, go with ADPCM4.

The command-line arguments depending on the input and output formats.

|Parameter|Description|
|---------|-----------|
|-input _&lt;file&gt;_ [_&lt;format&gt;_]|Specifies the input file. If the file is raw audio, _&lt;format&gt;_ specifies the format of the audio data; see below for the available values.|
|-output &_lt;file&gt;_ [_&lt;format&gt;_]|Specifies the output file. If the file is raw audio, _&lt;format&gt;_ specifies the format of the audio data; see below for the available values.|
|-sample-count _&lt;count&gt;_|Number of samples to convert. Used only if the input file is raw audio.|
|-sample-rate _&lt;rate&gt;_|Rate, in hertz, of the raw audio data. Used only when converting raw audio data to a WAV file.|
|-frame-rate _&lt;rate&gt;_|Specifies the frame rate of the stream. Used only when creating streams. If omitted, the frame rate defaults to 30.|
|-loop _&lt;start&gt;_|Enables looping and specifies which sample to which to loop back when hitting the end of the stream. Used only when creating streams. If omitted, the stream will not loop.|
|-mix-mode _&lt;mode&gt;_|Specifies how to mix stereo input down to mono. Used only when converting stereo WAV files to raw audio. Available modes are listed below. Defaults to _mix_.|

The available stereo-mixing modes are as follows:

|Mix Mode|Description|
|--------|-----------|
|mix|Mixes both channels together to create a mono wave.|
|left|Converts only the left stereo channel.|
|right|Converts only the right stereo channel.|

The formats of the input and output files are determined by the extension and must one of the following:

|Extension|Description|
|---------|-----------|
|.raw|Raw, mono audio data. Must be one of the raw-audio formats listed above. _&lt;format&gt;_ is required and is one of the raw-audio formats listed above.|
|.wav|Microsoft audio data container. Only mono or stereo LPCM of bitdepths 8 or 16 are supported. _&lt;format&gt;_ is ignored.|
|.afc|Stereo ADPCM audio stream. Supports loop points. _&lt;format&gt;_ is optional and either _pcm_ or _adpcm_. By default, streams are encoded to ADPCM.|

Only the following conversions are supported:

- .raw&nbsp;⇒&nbsp;.raw
- .raw&nbsp;⇒&nbsp;.wav
- .wav&nbsp;⇒&nbsp;.raw
- .wav&nbsp;⇒&nbsp;.afc
- .afc&nbsp;⇒&nbsp;.wav

> **Note:** when converting raw audio to a WAV file, the output format will always be 16-bit mono LPCM.
> When converting a stream to a WAV file, the output format will always be 16-bit stereo LPCM.

#### cotton

The _cotton_ action assembles a text-based assembler language into a BMS file.
The format is inspired by traditional languages like x86 and PPC and maps 1:1 to the byte code of the BMS format.
The arguments are as follows:

|Parameter|Description|
|---------|-----------|
|-input _&lt;file&gt;_|Specifies the path and filename to the input assembly text file.|
|-output _&lt;file&gt;_|Specifies the path and filename to the output BMS file.|

> **Note:** Documentation on the assembler language is soon to come.
