
## wave

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
|-output _&lt;file&gt;_ [_&lt;format&gt;_]|Specifies the output file. If the file is raw audio, _&lt;format&gt;_ specifies the format of the audio data; see below for the available values.|
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
|.wav|Microsoft audio-data container. Only mono or stereo LPCM of bit depths 8 or 16 are supported. _&lt;format&gt;_ is ignored.|
|.afc|Stereo ADPCM audio stream. Supports loop points. _&lt;format&gt;_ is optional and either _pcm_ or _adpcm_. By default, streams are encoded to ADPCM.|

Only the following conversions are supported:

- .raw&nbsp;⇒&nbsp;.raw
- .raw&nbsp;⇒&nbsp;.wav
- .wav&nbsp;⇒&nbsp;.raw
- .wav&nbsp;⇒&nbsp;.afc
- .afc&nbsp;⇒&nbsp;.wav

> **Note:** when converting raw audio to a WAV file, the output format will always be 16-bit mono LPCM.
> When converting a stream to a WAV file, the output format will always be 16-bit stereo LPCM.
